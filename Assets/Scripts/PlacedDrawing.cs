using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlacedDrawing : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    // ✅ シーン内の全お絵描きオブジェクトを静的リストで一元管理
    public static List<PlacedDrawing> AllDrawingsInScene = new List<PlacedDrawing>();

    [Header("レンダリング設定")]
    [SerializeField] private float lineWidthFactor = 0.012f;

    [Header("マテリアル")]
    [Tooltip("null の場合は Sprites/Default を自動使用")]
    [SerializeField] private Material lineMaterialTemplate;

    public List<LineRenderer> LineRenderers => lineRenderers;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

    // 各ストロークの「元の色」を保持するリスト
    // ✅ 重要: lineRenderers と常に同じ長さ・同じ順序を保つ
    private List<Color> originalColors = new List<Color>();

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void OnEnable()
    {
        // ✅ Start()ではなくOnEnable/OnDisableで管理することで、
        //    非アクティブ化・再アクティブ化にも安全に対応
        if (!AllDrawingsInScene.Contains(this))
            AllDrawingsInScene.Add(this);
    }

    void OnDisable()
    {
        AllDrawingsInScene.Remove(this);
    }

    // ────────────────────────────────────────────
    // Photon 初期化
    // ────────────────────────────────────────────

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = info.photonView.InstantiationData;

        if (data == null || data.Length < 4)
        {
            Debug.LogError("[PlacedDrawing] InstantiationData が不足しています。");
            return;
        }

        int   chunkCount    = (int)  data[0];
        float worldSize     = (float)data[1];
        float surfaceOffset = (float)data[2];

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < chunkCount; i++)
            sb.Append((string)data[3 + i]);

        string json = sb.ToString();
        DrawingData drawing = DrawingData.FromJson(json);

        if (drawing == null || drawing.IsEmpty)
        {
            Debug.LogError("[PlacedDrawing] JSON のパースに失敗、またはストロークが空です。");
            return;
        }

        BuildLines(drawing, worldSize, surfaceOffset);
    }

    // ────────────────────────────────────────────
    // 線の構築
    // ────────────────────────────────────────────

private void BuildLines(DrawingData drawing, float worldSize, float surfaceOffset)
    {
        float halfSize = worldSize * 0.5f;

        foreach (var stroke in drawing.strokes)
        {
            if (stroke.points.Count < 1) continue;

            GameObject go = new GameObject("Stroke");
            go.transform.SetParent(transform, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace       = false;
            lr.positionCount       = stroke.points.Count;
            lr.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows      = false;
            lr.generateLightingData = false;
            lr.material            = CreateMaterial(stroke.colorHex);

            Color parsedColor = Color.white;
            ColorUtility.TryParseHtmlString(stroke.colorHex, out parsedColor);
            lr.material.color = parsedColor;
            lr.startColor = parsedColor;
            lr.endColor   = parsedColor;

            // ────────────────────────────────────────────────────────
            // 🛠️ 【太さのズレを完全修正】
            // 謎の係数（lineWidthFactorや50f）をすべてゴミ箱にポイします。
            // 2Dで保存されたスライダーの太さ数値を、3D空間でも1ミリの歪みもなくダイレクトに適用！
            // ────────────────────────────────────────────────────────
            float w = stroke.normalizedWidth;
            
            // 安全装置（最小保証）も、0.001f などの極小値にしてスライダーの邪魔をさせない
            lr.startWidth = lr.endWidth = Mathf.Max(w, 0.001f);

            // 頂点の設定
            for (int i = 0; i < stroke.points.Count; i++)
            {
                var pt = stroke.points[i];
                lr.SetPosition(i, new Vector3(pt.x * halfSize, surfaceOffset, pt.y * halfSize));
            }

            lineRenderers.Add(lr);
            originalColors.Add(parsedColor);
        }
    }
    // ────────────────────────────────────────────
    // ハイライト / ハイライト解除
    // ────────────────────────────────────────────

    /// <summary>
    /// 指定インデックスの1本だけをハイライト色に変える。
    /// 呼ぶ前に必ず ClearHighlight() を内部で実行してリセットする。
    /// </summary>
    public void HighlightStroke(int strokeIndex, Color highlightColor)
    {
        ClearHighlight();

        // ✅ null チェック: 既に消去済みのインデックスは Unity-null になっているのでスキップ
        if (strokeIndex >= 0 && strokeIndex < lineRenderers.Count &&
            lineRenderers[strokeIndex] != null)
        {
            lineRenderers[strokeIndex].material.color = highlightColor;
        }
    }

    /// <summary>
    /// 全ての線の色を元の状態に戻す。
    /// </summary>
    public void ClearHighlight()
    {
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            // ✅ 【最重要修正】
            // EraseStrokeRemote で Destroy した後、lineRenderers[i] は Unity-null になる。
            // null チェックなしで .material.color にアクセスすると NullReferenceException が
            // 毎フレーム発生し、ハイライト機能全体がクラッシュしていた。
            if (lineRenderers[i] == null) continue;

            lineRenderers[i].material.color = originalColors[i];
        }
    }

    // ────────────────────────────────────────────
    // RPC: ストローク部分消し
    // ────────────────────────────────────────────

    [PunRPC]
    public void EraseStrokeRemote(int strokeIndex)
    {
        if (strokeIndex < 0 || strokeIndex >= lineRenderers.Count)
        {
            Debug.LogWarning($"[PlacedDrawing] 無効なインデックス {strokeIndex}（リスト長: {lineRenderers.Count}）");
            return;
        }

        LineRenderer targetLr = lineRenderers[strokeIndex];

        if (targetLr == null)
        {
            Debug.LogWarning($"[PlacedDrawing] インデックス {strokeIndex} は既に消去済みです。");
            return;
        }

        // ✅ 【設計上の重要な判断】
        // リストから Remove/RemoveAt してはいけない！
        //
        // 理由: マルチプレイでは複数のクライアントが同じリストを持ち、
        //       RPC で同じ「インデックス番号」を共有している。
        //       RemoveAt すると、それ以降の全てのインデックスが1ずつズレ、
        //       別のクライアントでは全く違う線が消されるバグが起きる。
        //
        // 正解: GameObject を Destroy するだけ。
        //       Unity は参照先のコンポーネントを Unity-null にするので、
        //       lineRenderers[strokeIndex] は自動的に null と判定されるようになる。
        //       リストの「席」は残したまま、中身だけ空にする方式。

        if (targetLr.material != null) Destroy(targetLr.material);
        Destroy(targetLr.gameObject);

        Debug.Log($"[PlacedDrawing] ストローク {strokeIndex} を消去しました。");
    }

    // ────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────

    private Material CreateMaterial(string colorHex)
    {
        if (lineMaterialTemplate != null)
            return new Material(lineMaterialTemplate);

        return new Material(Shader.Find("Sprites/Default"));
    }

    void OnDestroy()
    {
        // ✅ OnDisable が先に呼ばれてリストから除外されるが、念のため二重除去防止
        AllDrawingsInScene.Remove(this);

        foreach (var lr in lineRenderers)
        {
            if (lr != null && lr.material != null)
                Destroy(lr.material);
        }
    }
}