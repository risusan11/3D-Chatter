using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class DrawingPlacer : MonoBehaviourPun
{
    public enum PlayerMode
    {
        Default3DDraw, // デフォルト：3D直接描画（Simple3DPainterが動く）
        TabPlacement,  // 2D配置モード（TABキー）
        Eraser         // 配置消しゴムモード（Eキー）
    }

    [Header("現在の操作モード (確認用)")]
    [SerializeField] private PlayerMode currentMode = PlayerMode.Default3DDraw;

    [Header("参照")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string placedDrawingPrefabName = "PlacedDrawing";

    [Header("レイキャスト設定")]
    [SerializeField] private float maxRayDistance = 25f;
    [SerializeField] private LayerMask placementMask = ~0;

    [Header("サイズ設定")]
    [SerializeField] private float drawingWorldSize = 1.5f;
    [SerializeField] private float surfaceOffset = 0.008f;

    [Header("プレビュー外観")]
    [SerializeField] private Color previewTint = new Color(1f, 0.95f, 0.3f, 0.85f);

    [Header("🧹 消しゴム詳細設定")]
    [SerializeField] private KeyCode eraserKey = KeyCode.E;
    [Tooltip("画面上（ピクセル単位）での消しゴムの反応判定半径")]
    [SerializeField] private float eraseSelectPixelRadius = 40f;
    [SerializeField] private Color eraserHighlightColor = Color.red;
    [Tooltip("シーン内の全LineRendererをスキャンし直す間隔（フレーム数）")]
    [SerializeField] private int lineRendererCacheInterval = 20;

    // ────────────────────────────────────────────
    // TabPlacement 用
    // ────────────────────────────────────────────
    private DrawingData drawingData;
    private GameObject previewRoot;

    // ✅ プレビューもテクスチャクワッド方式に変更（配置後と完全に同じ見た目）
    private GameObject   previewQuad;
    private MeshRenderer previewQuadRenderer;
    private Texture2D    previewTexture;

    [Header("プレビュー解像度")]
    [Tooltip("プレビューテクスチャ解像度（PlacedDrawing と同じ値推奨）")]
    [SerializeField] private int previewTextureSize = 1024;

    private Vector3 hitPoint;
    private Vector3 hitNormal;
    private Quaternion hitRotation;
    private bool hasHit;

    private Simple3DPainter custom3DPainter;

    // ────────────────────────────────────────────
    // 【汎用消しゴム】状態管理
    // ────────────────────────────────────────────

    // ホバー中の LineRenderer（PlacedDrawing でも 3D線 でも共通）
    private LineRenderer hoveredLR       = null;
    private Color        hoveredOrigColor = Color.white;

    // PlacedDrawing 経由の場合だけ使う補助情報
    private PlacedDrawing hoveredDrawing     = null;
    private int           hoveredStrokeIndex = -1;

    // FindObjectsOfType のキャッシュ（毎フレーム呼ぶとコストが高いため）
    private LineRenderer[] cachedAllLRs  = null;
    private int            lrCacheTimer  = 0;

    // ────────────────────────────────────────────
    // 公開プロパティ
    // ────────────────────────────────────────────
    public PlayerMode CurrentMode => currentMode;
    public bool IsPlacing => currentMode == PlayerMode.TabPlacement;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void Start()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) { enabled = false; return; }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                Debug.LogError("[DrawingPlacer] playerCamera が見つかりません。");
        }

        custom3DPainter = GetComponentInChildren<Simple3DPainter>();
        if (custom3DPainter == null)
            custom3DPainter = GetComponentInParent<Simple3DPainter>();

        ApplyModeSettings();
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (RealisingMessageController.isChatting) return;

        HandleModeSwitchInput();

        switch (currentMode)
        {
            case PlayerMode.Default3DDraw:
                break;
            case PlayerMode.TabPlacement:
                HandleTabPlacementMode();
                break;
            case PlayerMode.Eraser:
                HandleEraserModeScreenSpace();
                break;
        }
    }

    // ────────────────────────────────────────────
    // モード切り替え
    // ────────────────────────────────────────────

    private void HandleModeSwitchInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // 同じキーを押したらトグル解除してDefault3DDrawに戻る
            SwitchMode(currentMode == PlayerMode.TabPlacement
                ? PlayerMode.Default3DDraw
                : PlayerMode.TabPlacement);
        }
        else if (Input.GetKeyDown(eraserKey))
        {
            // 同じキーを押したらトグル解除してDefault3DDrawに戻る
            SwitchMode(currentMode == PlayerMode.Eraser
                ? PlayerMode.Default3DDraw
                : PlayerMode.Eraser);
        }
        // B キー / Escape キー、どちらでも描画モードに戻る
        else if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentMode != PlayerMode.Default3DDraw)
                SwitchMode(PlayerMode.Default3DDraw);
        }
    }

    private void SwitchMode(PlayerMode nextMode)
    {
        if (currentMode == nextMode) return;

        // ── 退去処理 ──
        switch (currentMode)
        {
            case PlayerMode.TabPlacement:
                DestroyPreview();
                break;

            case PlayerMode.Eraser:
                // ハイライト中の色を必ず元に戻す
                RestoreHoveredColor();
                cachedAllLRs = null; // キャッシュを破棄
                break;
        }

        currentMode = nextMode;

        // ── 入居処理 ──
        if (currentMode == PlayerMode.TabPlacement)
        {
            if (DrawingManager.Instance != null && DrawingManager.Instance.HasDrawing)
            {
                drawingData = DrawingManager.Instance.CurrentDrawing;
                previewRoot = new GameObject("DrawingPreview");
                BuildPreviewQuad();
            }
            else
            {
                Debug.LogWarning("[DrawingPlacer] 配置できる描画データがありません。");
            }
        }

        ApplyModeSettings();
        Debug.Log($"[DrawingPlacer] モード変更 ➡ {currentMode}");
    }

    private void ApplyModeSettings()
    {
        if (custom3DPainter == null) return;

        switch (currentMode)
        {
            case PlayerMode.Default3DDraw:
                custom3DPainter.enabled = true;
                SetPreviewVisible(false);
                break;
            case PlayerMode.TabPlacement:
                custom3DPainter.enabled = false;
                break;
            case PlayerMode.Eraser:
                custom3DPainter.enabled = false;
                SetPreviewVisible(false);
                break;
        }
    }

    // ────────────────────────────────────────────
    // TabPlacement モード
    // ────────────────────────────────────────────

    private void HandleTabPlacementMode()
    {
        UpdateRaycast();

        if (hasHit)
        {
            UpdatePreview();
            if (Input.GetMouseButtonDown(0) &&
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                ConfirmPlacement();
            }
        }
        else
        {
            SetPreviewVisible(false);
        }
    }

    // ────────────────────────────────────────────
    // 【汎用消しゴム】Eraser モード
    //
    // ✅ 設計の核心：
    //   PlacedDrawing だけでなく Simple3DPainter の 3D 線も含む
    //   シーン内の「全 LineRenderer」を Screen Space ピクセル距離でスキャンする。
    //
    //   見つけた線が PlacedDrawing 由来 → RPC で全員から消去（マルチ同期）
    //   見つけた線が 3D 描画線        → 直接 Destroy（ローカル描画の場合）
    // ────────────────────────────────────────────

    private void HandleEraserModeScreenSpace()
    {
        if (playerCamera == null) return;

        // ── キャッシュの更新（FindObjectsOfType は高コストなので N フレームに 1 回）──
        lrCacheTimer++;
        if (cachedAllLRs == null || lrCacheTimer >= lineRendererCacheInterval)
        {
            cachedAllLRs = FindObjectsOfType<LineRenderer>();
            lrCacheTimer = 0;
        }

        Vector2 mousePos = Input.mousePosition;

        LineRenderer  closestLR         = null;
        PlacedDrawing closestDrawing    = null;
        int           closestStrokeIdx  = -1;
        float         minPixelDist      = eraseSelectPixelRadius;

        foreach (LineRenderer lr in cachedAllLRs)
        {
            // ✅ Destroy 済み・非アクティブはスキップ
            if (lr == null || !lr.gameObject.activeInHierarchy) continue;

            // プレビュー用の LineRenderer は消去対象から除外
            if (previewRoot != null && lr.transform.IsChildOf(previewRoot.transform)) continue;

            for (int p = 0; p < lr.positionCount; p++)
            {
                // ✅ useWorldSpace を正しく判定して「確実にワールド座標」を取得する
                //    PlacedDrawing  → useWorldSpace = false（ローカル座標）
                //    Simple3DPainter → useWorldSpace = true（ワールド座標）
                Vector3 worldPos = lr.useWorldSpace
                    ? lr.GetPosition(p)
                    : lr.transform.TransformPoint(lr.GetPosition(p));

                Vector3 screenPos3D = playerCamera.WorldToScreenPoint(worldPos);
                if (screenPos3D.z <= 0f) continue; // カメラ背後は無視

                float pixelDist = Vector2.Distance(mousePos, new Vector2(screenPos3D.x, screenPos3D.y));

                if (pixelDist < minPixelDist)
                {
                    minPixelDist    = pixelDist;
                    closestLR       = lr;

                    // この LR が PlacedDrawing の子かどうかを判定
                    closestDrawing   = lr.GetComponentInParent<PlacedDrawing>();
                    closestStrokeIdx = (closestDrawing != null)
                        ? closestDrawing.LineRenderers.IndexOf(lr)
                        : -1;
                }
            }
        }

        // ── ハイライトの更新（前フレームから変化があった時だけ処理）──
        if (closestLR != hoveredLR)
        {
            // ── 前回のハイライトを解除 ──
            // ✅ PlacedDrawing 由来 → テクスチャ方式のハイライト解除
            //    Simple3DPainter 由来 → マテリアル色を元に戻す
            if (hoveredDrawing != null)
            {
                hoveredDrawing.ClearHighlight();
            }
            else if (hoveredLR != null && hoveredLR.material != null)
            {
                hoveredLR.material.color = hoveredOrigColor;
            }

            // ── 新しい対象をハイライト ──
            if (closestLR != null)
            {
                if (closestDrawing != null && closestStrokeIdx >= 0)
                {
                    // ✅ PlacedDrawing：テクスチャを再描画してハイライト
                    //    （透明な検出用 LR の material.color は触らない！）
                    closestDrawing.HighlightStroke(closestStrokeIdx, eraserHighlightColor);
                }
                else if (closestLR.material != null)
                {
                    // Simple3DPainter の 3D 線：従来通りマテリアル色を直接変更
                    hoveredOrigColor = closestLR.material.color;
                    closestLR.material.color = eraserHighlightColor;
                }
            }

            hoveredLR          = closestLR;
            hoveredDrawing     = closestDrawing;
            hoveredStrokeIndex = closestStrokeIdx;
        }

        // ── 左クリックで消去確定 ──
        if (Input.GetMouseButtonDown(0) &&
            !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (hoveredLR != null)
            {
                if (hoveredDrawing != null && hoveredStrokeIndex >= 0)
                {
                    // ────────────────────────────────────
                    // ケース A: PlacedDrawing の線
                    // RPC で全クライアントに消去を同期
                    // ────────────────────────────────────
                    PlacedDrawing target = hoveredDrawing;
                    int           idx    = hoveredStrokeIndex;

                    // 先に参照をクリア（二重処理防止）
                    hoveredLR          = null;
                    hoveredDrawing     = null;
                    hoveredStrokeIndex = -1;

                    target.GetComponent<PhotonView>().RPC(
                        nameof(PlacedDrawing.EraseStrokeRemote),
                        RpcTarget.All,
                        idx
                    );
                    Debug.Log($"[DrawingPlacer] PlacedDrawing のストローク {idx} を RPC で消去しました。");
                }
                else
                {
                    // ────────────────────────────────────
                    // ケース B: Simple3DPainter 等の 3D 線
                    // 直接 Destroy で消去
                    // ────────────────────────────────────
                    LineRenderer toErase = hoveredLR;

                    // 先に参照をクリア
                    hoveredLR          = null;
                    hoveredDrawing     = null;
                    hoveredStrokeIndex = -1;

                    if (toErase != null)
                    {
                        if (toErase.material != null) Destroy(toErase.material);
                        Destroy(toErase.gameObject);
                        Debug.Log("[DrawingPlacer] 3D 空中線を直接消去しました。");
                    }
                }

                // 消去後はキャッシュを即座に無効化して次フレームで再スキャン
                cachedAllLRs = null;
            }
        }
    }

    // ────────────────────────────────────────────
    // ハイライト色の復元ヘルパー
    // ────────────────────────────────────────────

    private void RestoreHoveredColor()
    {
        // ✅ PlacedDrawing 由来 → テクスチャ方式のハイライト解除
        if (hoveredDrawing != null)
        {
            hoveredDrawing.ClearHighlight();
        }
        // Simple3DPainter 由来 → マテリアル色を元に戻す
        else if (hoveredLR != null && hoveredLR.material != null)
        {
            hoveredLR.material.color = hoveredOrigColor;
        }

        hoveredLR          = null;
        hoveredDrawing     = null;
        hoveredStrokeIndex = -1;
    }

    // ────────────────────────────────────────────
    // レイキャスト（TabPlacement 用）
    // ────────────────────────────────────────────

    private void UpdateRaycast()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, placementMask))
        {
            hitPoint    = hit.point;
            hitNormal   = hit.normal;
            hitRotation = BuildSurfaceRotation(hit.normal);
            hasHit      = true;
        }
        else
        {
            hasHit = false;
        }
    }

    private Quaternion BuildSurfaceRotation(Vector3 normal)
    {
        Vector3 worldRef = (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f)
            ? Vector3.forward
            : Vector3.up;
        Vector3 right   = Vector3.Cross(worldRef, normal).normalized;
        Vector3 forward = Vector3.Cross(normal, right).normalized;
        return Quaternion.LookRotation(forward, normal);
    }

    // ────────────────────────────────────────────
    // プレビュー（TabPlacement 用）— テクスチャクワッド方式
    // ────────────────────────────────────────────

    private void BuildPreviewQuad()
    {
        if (drawingData == null || previewRoot == null) return;

        // ① テクスチャを焼く（全ストロークを previewTint 単色で）
        previewTexture = StrokeTextureRenderer.RenderWithUniformColor(
            drawingData, previewTextureSize, previewTint);

        // ② クワッドメッシュを生成
        float h = drawingWorldSize * 0.5f;
        Mesh mesh = new Mesh { name = "PreviewQuad" };
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
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1,   // 表
                                     0, 1, 2, 2, 1, 3 };  // 裏
        mesh.RecalculateNormals();

        previewQuad = new GameObject("PreviewQuad");
        previewQuad.transform.SetParent(previewRoot.transform, false);

        var mf = previewQuad.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = previewTexture;
        mat.color       = Color.white;

        previewQuadRenderer = previewQuad.AddComponent<MeshRenderer>();
        previewQuadRenderer.material            = mat;
        previewQuadRenderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        previewQuadRenderer.receiveShadows      = false;
    }

    private void UpdatePreview()
    {
        if (previewRoot == null || previewQuad == null) return;

        SetPreviewVisible(true);

        // クワッド全体を hitPoint に配置し、表面の法線に合わせて回転
        previewRoot.transform.position = hitPoint;
        previewRoot.transform.rotation = hitRotation;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewQuadRenderer != null) previewQuadRenderer.enabled = visible;
    }

    private void DestroyPreview()
    {
        if (previewTexture != null)
        {
            Destroy(previewTexture);
            previewTexture = null;
        }
        if (previewQuadRenderer != null && previewQuadRenderer.material != null)
        {
            Destroy(previewQuadRenderer.material);
        }
        if (previewRoot != null) Destroy(previewRoot);

        previewRoot         = null;
        previewQuad         = null;
        previewQuadRenderer = null;
    }

    // ────────────────────────────────────────────
    // 配置確定（TabPlacement 用）
    // ────────────────────────────────────────────

    private void ConfirmPlacement()
    {
        if (!hasHit) return;

        string json      = DrawingManager.Instance.CurrentJson;
        int chunkSize    = 20000;
        int chunkCount   = Mathf.CeilToInt((float)json.Length / chunkSize);

        object[] data = new object[3 + chunkCount];
        data[0] = chunkCount;
        data[1] = drawingWorldSize;
        data[2] = surfaceOffset;

        for (int i = 0; i < chunkCount; i++)
        {
            int startIdx = i * chunkSize;
            int length   = Mathf.Min(chunkSize, json.Length - startIdx);
            data[3 + i]  = json.Substring(startIdx, length);
        }

        PhotonNetwork.Instantiate(placedDrawingPrefabName, hitPoint, hitRotation, 0, data);
        Debug.Log($"[DrawingPlacer] 配置確定 @ {hitPoint}");
    }

    // ────────────────────────────────────────────
    // ユーティリティ
    // ────────────────────────────────────────────

    private Material CreateMaterial(Color color)
    {
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        return mat;
    }
}