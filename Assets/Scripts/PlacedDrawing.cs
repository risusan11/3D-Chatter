using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// テクスチャ方式 PlacedDrawing。
///
/// 【視覚】StrokeTextureRenderer で描画データをテクスチャに焼いてクワッドに貼る。
///        → 2D の「円スタンプ塗りつぶし」を 3D で完全再現。
///
/// 【消しゴム検出】透明な LineRenderer を裏で保持し、DrawingPlacer の
///        スクリーン空間スキャンがそのまま動く。
///        → DrawingPlacer.cs の変更不要。
///
/// 【消去時】DrawingData から該当ストロークを除外して再テクスチャ描画。
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PlacedDrawing : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    public static List<PlacedDrawing> AllDrawingsInScene = new List<PlacedDrawing>();

    [Header("テクスチャ解像度（512=軽量、1024=標準、2048=高品質）")]
    [SerializeField] private int textureSize = 1024;

    [Header("マテリアル")]
    [Tooltip("null の場合は Sprites/Default を自動使用")]
    [SerializeField] private Material quadMaterialTemplate;

    // ── 公開: DrawingPlacer の消しゴムがそのまま使える ──
    public List<LineRenderer> LineRenderers => detectionLRs;

    // ── 内部 ──
    private List<LineRenderer> detectionLRs = new List<LineRenderer>(); // 消しゴム検出用（透明）
    private List<bool>         erasedFlags  = new List<bool>();          // 消去済みフラグ

    private DrawingData  cachedDrawing;
    private float        cachedWorldSize;
    private float        cachedSurfaceOffset;

    private GameObject   quadObject;
    private MeshRenderer quadRenderer;
    private Texture2D    currentTexture;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void OnEnable()  { if (!AllDrawingsInScene.Contains(this)) AllDrawingsInScene.Add(this); }
    void OnDisable() { AllDrawingsInScene.Remove(this); }

    // ────────────────────────────────────────────
    // Photon 初期化
    // ────────────────────────────────────────────

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;
        if (data == null || data.Length < 4)
        {
            Debug.LogError("[PlacedDrawing] InstantiationData が不足しています。"); return;
        }

        int   chunkCount    = (int)  data[0];
        float worldSize     = (float)data[1];
        float surfaceOffset = (float)data[2];

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < chunkCount; i++) sb.Append((string)data[3 + i]);

        DrawingData drawing = DrawingData.FromJson(sb.ToString());
        if (drawing == null || drawing.IsEmpty)
        {
            Debug.LogError("[PlacedDrawing] JSON パースに失敗しました。"); return;
        }

        cachedDrawing       = drawing;
        cachedWorldSize     = worldSize;
        cachedSurfaceOffset = surfaceOffset;

        BuildVisuals(drawing, worldSize, surfaceOffset);
    }

    // ────────────────────────────────────────────
    // ビジュアル構築
    // ────────────────────────────────────────────

    private void BuildVisuals(DrawingData drawing, float worldSize, float surfaceOffset)
    {
        // 1. テクスチャをレンダリングしてクワッドに貼る（見た目）
        BuildTextureQuad(drawing, worldSize, surfaceOffset);

        // 2. 消しゴム検出用の透明 LineRenderer を構築（当たり判定）
        BuildDetectionLines(drawing, worldSize, surfaceOffset);
    }

    // ────────────────────────────────────────────
    // テクスチャクワッド
    // ────────────────────────────────────────────

    private void BuildTextureQuad(DrawingData drawing, float worldSize, float surfaceOffset)
    {
        // テクスチャを焼く
        currentTexture = StrokeTextureRenderer.Render(drawing, textureSize);

        // クワッドメッシュを生成（サーフェスオフセット分だけ浮かせる）
        float h = worldSize * 0.5f;

        Mesh mesh = new Mesh { name = "DrawingQuad" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-h, surfaceOffset, -h),
            new Vector3( h, surfaceOffset, -h),
            new Vector3(-h, surfaceOffset,  h),
            new Vector3( h, surfaceOffset,  h),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };
        // 両面見えるよう表裏両方のトライアングルを設定
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1,   // 表
                                     0, 1, 2, 2, 1, 3 };  // 裏
        mesh.RecalculateNormals();

        quadObject = new GameObject("TextureQuad");
        quadObject.transform.SetParent(transform, false);

        MeshFilter mf = quadObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        Material mat = quadMaterialTemplate != null
            ? new Material(quadMaterialTemplate)
            : new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = currentTexture;
        mat.color       = Color.white;

        quadRenderer = quadObject.AddComponent<MeshRenderer>();
        quadRenderer.material            = mat;
        quadRenderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows      = false;
    }

    // ────────────────────────────────────────────
    // 消しゴム検出用の透明 LineRenderer（DrawingPlacer がそのまま使える）
    // ────────────────────────────────────────────

    private void BuildDetectionLines(DrawingData drawing, float worldSize, float surfaceOffset)
    {
        float halfSize  = worldSize * 0.5f;
        Material invis  = CreateInvisibleMaterial();

        foreach (var stroke in drawing.strokes)
        {
            if (stroke.points.Count < 1)
            {
                detectionLRs.Add(null);
                erasedFlags.Add(false);
                continue;
            }

            GameObject go = new GameObject("DetectionLR");
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace        = false;
            lr.positionCount        = stroke.points.Count;
            lr.startWidth           = Mathf.Max(stroke.normalizedWidth * worldSize, 0.01f);
            lr.endWidth             = lr.startWidth;
            lr.material             = invis;
            lr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows       = false;
            lr.generateLightingData = false;

            for (int i = 0; i < stroke.points.Count; i++)
            {
                var pt = stroke.points[i];
                lr.SetPosition(i, new Vector3(pt.x * halfSize, surfaceOffset, pt.y * halfSize));
            }

            detectionLRs.Add(lr);
            erasedFlags.Add(false);
        }
    }

    // ────────────────────────────────────────────
    // ハイライト（消しゴムホバー時）
    //
    // テクスチャ方式では「1本だけ色を変える」ことが難しいため、
    // ホバー中のストロークをハイライト色で重ね描きしたテクスチャを一時的に適用する。
    // ────────────────────────────────────────────

    // ハイライト用の使い捨てテクスチャ（メモリリーク防止のため毎回 Destroy）
    private Texture2D highlightTexture;

    public void HighlightStroke(int strokeIndex, Color highlightColor)
    {
        if (cachedDrawing == null) return;
        if (strokeIndex < 0 || strokeIndex >= cachedDrawing.strokes.Count) return;
        if (quadRenderer == null) return;

        // ✅ 前回のハイライトテクスチャを破棄（メモリリーク防止）
        if (highlightTexture != null) Destroy(highlightTexture);

        // 通常テクスチャをベースに、対象ストロークだけをハイライト色で再描画
        highlightTexture = StrokeTextureRenderer.RenderWithHighlight(
            cachedDrawing, textureSize, strokeIndex, highlightColor);

        quadRenderer.material.mainTexture = highlightTexture;
    }

    public void ClearHighlight()
    {
        // 元のテクスチャに戻す
        if (quadRenderer != null && currentTexture != null)
            quadRenderer.material.mainTexture = currentTexture;

        // ハイライトテクスチャを破棄
        if (highlightTexture != null)
        {
            Destroy(highlightTexture);
            highlightTexture = null;
        }
    }

    // ────────────────────────────────────────────
    // RPC: ストローク消し
    // ────────────────────────────────────────────

    [PunRPC]
    public void EraseStrokeRemote(int strokeIndex)
    {
        if (strokeIndex < 0 || strokeIndex >= detectionLRs.Count)
        {
            Debug.LogWarning($"[PlacedDrawing] 無効なインデックス {strokeIndex}"); return;
        }
        if (erasedFlags[strokeIndex])
        {
            Debug.LogWarning($"[PlacedDrawing] インデックス {strokeIndex} は既に消去済み"); return;
        }

        erasedFlags[strokeIndex] = true;

        // 消しゴム検出用 LR を破棄（DrawingPlacer がこの null を skip する）
        if (detectionLRs[strokeIndex] != null)
        {
            Destroy(detectionLRs[strokeIndex].gameObject);
            // リストの「席」は残す（インデックスを安定させるため）
        }

        // テクスチャを該当ストロークなしで再レンダリング
        RebuildTexture();

        Debug.Log($"[PlacedDrawing] ストローク {strokeIndex} を消去しました。");
    }

    /// <summary>
    /// 消去済みフラグを考慮してテクスチャを再ビルドする。
    /// </summary>
    private void RebuildTexture()
    {
        if (cachedDrawing == null) return;

        // erasedFlags に基づいて「有効なストロークだけ」の DrawingData を構築
        DrawingData active = new DrawingData();
        for (int i = 0; i < cachedDrawing.strokes.Count; i++)
        {
            if (!erasedFlags[i]) active.strokes.Add(cachedDrawing.strokes[i]);
        }

        if (currentTexture != null) Destroy(currentTexture);
        currentTexture = StrokeTextureRenderer.Render(active, textureSize);

        if (quadRenderer != null)
            quadRenderer.material.mainTexture = currentTexture;
    }

    // ────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────

    private Material CreateInvisibleMaterial()
    {
        var mat   = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0, 0, 0, 0); // 完全透明
        return mat;
    }

    void OnDestroy()
    {
        AllDrawingsInScene.Remove(this);
        if (currentTexture != null) Destroy(currentTexture);
        if (highlightTexture != null) Destroy(highlightTexture);
        if (quadRenderer != null && quadRenderer.material != null)
            Destroy(quadRenderer.material);
    }
}