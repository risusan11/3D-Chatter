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
    [SerializeField] private float previewWidth = 0.012f;

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
    private List<LineRenderer> previewLines = new List<LineRenderer>();

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
                BuildPreviewLines();
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
            if (lr == null || !lr.gameObject.activeInHierarchy) continue;
            if (previewRoot != null && lr.transform.IsChildOf(previewRoot.transform)) continue;

            for (int p = 0; p < lr.positionCount; p++)
            {
                Vector3 worldPos = lr.useWorldSpace
                    ? lr.GetPosition(p)
                    : lr.transform.TransformPoint(lr.GetPosition(p));

                Vector3 screenPos3D = playerCamera.WorldToScreenPoint(worldPos);
                if (screenPos3D.z <= 0f) continue; 

                float pixelDist = Vector2.Distance(mousePos, new Vector2(screenPos3D.x, screenPos3D.y));

                if (pixelDist < minPixelDist)
                {
                    minPixelDist     = pixelDist;
                    closestLR        = lr;
                    closestDrawing   = lr.GetComponentInParent<PlacedDrawing>();
                    closestStrokeIdx = (closestDrawing != null)
                        ? closestDrawing.LineRenderers.IndexOf(lr)
                        : -1;
                }
            }
        }

        // ── ハイライトの更新 ──
        if (closestLR != hoveredLR)
        {
            RestoreHoveredColor();

            if (closestLR != null && closestLR.material != null)
            {
                hoveredOrigColor = closestLR.material.color;
                closestLR.material.color = eraserHighlightColor;
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
                    // ケース A: PlacedDrawing の線（変更なし）
                    // ────────────────────────────────────
                    PlacedDrawing target = hoveredDrawing;
                    int           idx    = hoveredStrokeIndex;

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
                    // ケース B: ✨【マルチ同期に完全修正！】Simple3DPainter の 3D 線
                    // ────────────────────────────────────
                    LineRenderer toErase = hoveredLR;

                    hoveredLR          = null;
                    hoveredDrawing     = null;
                    hoveredStrokeIndex = -1;

                    if (toErase != null)
                    {
                        // 💡 1. 狙った3D線が「誰のアバターオブジェクトに属しているか」を親から逆算特定する
                        Simple3DPainter ownerPainter = toErase.GetComponentInParent<Simple3DPainter>();
                        if (ownerPainter != null)
                        {
                            // 💡 2. そのアバターが持つ3D線リストから、狙った線のインデックス（背番号）を割り出す
                            int strokeIdx = ownerPainter.ActiveLines.IndexOf(toErase);
                            if (strokeIdx != -1)
                            {
                                // 🚀 3. 線の持ち主のアバターが持つ PhotonView を仲介して、全員の画面で一斉消去RPCを発動！
                                ownerPainter.GetComponent<PhotonView>().RPC("Erase3DStrokeRemote", RpcTarget.All, strokeIdx);
                                Debug.Log($"[3D消しゴム同期] 3D空中線番号 {strokeIdx} をネットワーク一斉消去要請しました。");
                            }
                        }
                    }
                }

                cachedAllLRs = null;
            }
        }
    }

    // ────────────────────────────────────────────
    // ハイライト色の復元ヘルパー
    // ────────────────────────────────────────────

    private void RestoreHoveredColor()
    {
        if (hoveredLR != null && hoveredLR.material != null)
            hoveredLR.material.color = hoveredOrigColor;

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
    // プレビュー（TabPlacement 用）
    // ────────────────────────────────────────────

private void BuildPreviewLines()
    {
        if (drawingData == null) return;

        Material mat = CreateMaterial(previewTint);
        
        // 💡 配置後（PlacedDrawing.cs）の設定（0.012f）と完全にシンクロさせる
        float lineWidth = drawingWorldSize * 0.012f; 

        for (int i = 0; i < drawingData.strokes.Count; i++)
        {
            var stroke = drawingData.strokes[i];
            if (stroke.points.Count < 1) continue;

            var go = new GameObject("PL_Stroke");
            go.transform.SetParent(previewRoot.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            
            // 🛠️【太さバグ修正】プレビュー生成の瞬間から、配置後と全く同じ数式で太さを計算して割り当てる！
            float w = Mathf.Max(lineWidth * stroke.normalizedWidth * 50f, lineWidth * 0.5f);
            lr.startWidth        = w;
            lr.endWidth          = w;
            
            lr.material          = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.positionCount     = stroke.points.Count;

            previewLines.Add(lr);
        }
    }

private void UpdatePreview()
    {
        if (previewRoot == null || drawingData == null) return;

        SetPreviewVisible(true);
        float halfSize = drawingWorldSize * 0.5f;
        Vector3 right  = hitRotation * Vector3.right;
        Vector3 fwd    = hitRotation * Vector3.forward;
        Vector3 up     = hitNormal;

        // 💡 配置後（PlacedDrawing.cs）の太さ計算ロジックと100%完全に一致させる
        float lineWidth = drawingWorldSize * 0.012f; 

        for (int si = 0; si < previewLines.Count && si < drawingData.strokes.Count; si++)
        {
            var stroke = drawingData.strokes[si];
            var lr     = previewLines[si];
            lr.positionCount = stroke.points.Count;

            // 🛠️【太さバグ修正】構えている間も、2D側から届いた太さを毎フレーム完全に適用！
            float w = Mathf.Max(lineWidth * stroke.normalizedWidth * 50f, lineWidth * 0.5f);
            lr.startWidth = lr.endWidth = w;

            for (int pi = 0; pi < stroke.points.Count; pi++)
            {
                var pt = stroke.points[pi];
                Vector3 worldPos = hitPoint
                    + right * (pt.x * halfSize)
                    + fwd   * (pt.y * halfSize)
                    + up    * surfaceOffset;

                lr.SetPosition(pi, worldPos);
            }
        }
    }

    private void SetPreviewVisible(bool visible)
    {
        foreach (var lr in previewLines)
            if (lr != null) lr.enabled = visible;
    }

    private void DestroyPreview()
    {
        previewLines.Clear();
        if (previewRoot != null) Destroy(previewRoot);
        previewRoot = null;
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