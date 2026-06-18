using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PointerUIController : MonoBehaviourPun
{
    [Header("🎯 画面中央のクロスヘア（UI オブジェクト）")]
    [SerializeField] private GameObject crosshairUI;

    [Header("📦 3D ペイント用のキューブ（3D オブジェクト）")]
    [SerializeField] private Transform brushCubeVisual;

    [Header("📐 キューブのサイズ設定")]
    [SerializeField] private Vector3 normalScale   = new Vector3(0.08f, 0.08f, 0.08f);
    [SerializeField] private Vector3 nearWallScale = new Vector3(0.25f, 0.25f, 0.25f);

    [Header("🎨 消しゴム時のビジュアル変化")]
    [SerializeField] private Color eraserCubeColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    [Header("🧹 消しゴムキューブの距離設定")]
    [Tooltip("壁に当たらなかった場合にカメラ前方何mにキューブを浮かせるか（空中線の消去に使う）")]
    [SerializeField] private float eraserFloatDistance = 3f;
    [Tooltip("消しゴムレイキャストの最大距離（Infinity だと空中線の背後が遠すぎてキューブが吹っ飛ぶ）")]
    [SerializeField] private float eraserMaxRayDistance = 50f;

    // ────────────────────────────────────────────
    // 内部参照
    // ────────────────────────────────────────────

    private DrawingPlacer   drawingPlacer;
    private Simple3DPainter simple3DPainter;
    private Renderer        cubeRenderer;
    private Color           originalCubeColor;
    private Camera          playerCamera;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void Start()
    {
        // 他プレイヤーのオブジェクトは UI を非表示にして無効化
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            if (crosshairUI     != null) crosshairUI.SetActive(false);
            if (brushCubeVisual != null) brushCubeVisual.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        drawingPlacer   = GetComponent<DrawingPlacer>();
        simple3DPainter = GetComponent<Simple3DPainter>();

        playerCamera = Camera.main;
        if (playerCamera == null)
            Debug.LogError("[PointerUIController] playerCamera が見つかりません。MainCamera タグを確認してください。");

        if (brushCubeVisual != null)
        {
            cubeRenderer = brushCubeVisual.GetComponent<Renderer>();
            if (cubeRenderer != null)
                originalCubeColor = cubeRenderer.material.color;
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (RealisingMessageController.isChatting) return;
        if (drawingPlacer == null) return;

        switch (drawingPlacer.CurrentMode)
        {
            // ────────────────────────────────────
            // 1. 2D 配置モード
            //    配置プレビューに集中させるため、全 UI 非表示
            // ────────────────────────────────────
            case DrawingPlacer.PlayerMode.TabPlacement:
                SetCrosshair(false);
                SetCube(false);
                break;

            // ────────────────────────────────────
            // 2. 消しゴムモード
            //    クロスヘア表示 + 赤いキューブをマウス先に追従
            // ────────────────────────────────────
            case DrawingPlacer.PlayerMode.Eraser:
                SetCrosshair(true);

                if (brushCubeVisual != null && playerCamera != null)
                {
                    Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

                    if (Physics.Raycast(ray, out RaycastHit hit, eraserMaxRayDistance))
                    {
                        // ✅ 壁・床に当たった → 表面にピタッと吸い付かせる
                        brushCubeVisual.position = hit.point;

                        // 壁面の法線方向にキューブを回転させる
                        Vector3 worldRef = (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) > 0.99f)
                            ? Vector3.forward
                            : Vector3.up;
                        Vector3 right   = Vector3.Cross(worldRef, hit.normal).normalized;
                        Vector3 forward = Vector3.Cross(hit.normal, right).normalized;
                        brushCubeVisual.rotation = Quaternion.LookRotation(forward, hit.normal);
                    }
                    else
                    {
                        // ✅ 【修正】壁に当たらなかった（空中の線を狙っている）
                        //    Infinity を使っていたため、背後に壁がない方向でキューブが
                        //    遥か彼方に飛んでいた。カメラ前方の固定距離に浮かせることで解決。
                        brushCubeVisual.position = playerCamera.transform.position
                                                 + playerCamera.transform.forward * eraserFloatDistance;
                        brushCubeVisual.rotation = Quaternion.LookRotation(
                            playerCamera.transform.forward,
                            playerCamera.transform.up
                        );
                    }

                    // サイズと色は常にセット（表示/非表示に関わらず値を維持）
                    brushCubeVisual.localScale = normalScale;
                    if (cubeRenderer != null)
                        cubeRenderer.material.color = eraserCubeColor;

                    // ✅ 常に表示（空中でも赤いキューブで「狙い中」を示す）
                    SetCube(true);
                }
                break;

            // ────────────────────────────────────
            // 3. デフォルト 3D 直接描画モード
            //    描画中 → ブラシキューブを筆先に追従
            //    非描画中 → クロスヘアのみ表示
            // ────────────────────────────────────
            case DrawingPlacer.PlayerMode.Default3DDraw:
            bool is3DMode   = (ModeManager.Instance.Current == ModeManager.AppMode.Draw3D);
            bool is3DEraser = (ModeManager.Instance.Current == ModeManager.AppMode.Erase);
                if (is3DMode)
                {
                    SetCrosshair(false);
                    SetCube(true);

                    // Simple3DPainter が公開している筆先座標・回転に完全追従
                    brushCubeVisual.position   = simple3DPainter.BrushTipPosition;
                    brushCubeVisual.rotation   = simple3DPainter.BrushTipRotation;
                    brushCubeVisual.localScale = simple3DPainter.IsNearWall ? nearWallScale : normalScale;

                    if (cubeRenderer != null)
                        cubeRenderer.material.color = is3DEraser ? eraserCubeColor : originalCubeColor;
                }
                else
                {
                    SetCrosshair(true);
                    SetCube(false);

                    // キューブ色を元に戻す（消しゴムモードから帰還した直後の残色リセット）
                    if (cubeRenderer != null)
                        cubeRenderer.material.color = originalCubeColor;
                }
                break;
        }
    }

    // ────────────────────────────────────────────
    // ユーティリティ（SetActive のラッパー）
    // ────────────────────────────────────────────

    private void SetCrosshair(bool active)
    {
        if (crosshairUI != null) crosshairUI.SetActive(active);
    }

    private void SetCube(bool active)
    {
        if (brushCubeVisual != null) brushCubeVisual.gameObject.SetActive(active);
    }
}