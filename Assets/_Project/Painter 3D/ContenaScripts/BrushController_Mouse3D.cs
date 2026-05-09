using UnityEngine;
using Painter3D;

public class BrushController_Mouse3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Brush brush;

    [Header("3D Mouse Drawing")]
    [SerializeField] private float drawDepthFromCamera = 3f;
    [SerializeField] private bool allowDepthChangeByWheel = true;
    [SerializeField] private float wheelDepthSpeed = 1f;
    [SerializeField] private float minDepth = 0.2f;
    [SerializeField] private float maxDepth = 30f;

    [Header("Debug")]
    [SerializeField] private Transform debugBrushTipVisual;

    private Transform brushTip;
    private bool wasDrawing;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        GameObject tipObj = new GameObject("Mouse3D_BrushTip");
        brushTip = tipObj.transform;
    }

    private void Update()
    {
        if (targetCamera == null || brush == null) return;

        UpdateDepth();
        UpdateBrushTipPosition();

        bool isDrawing = Input.GetMouseButton(0);

        if (isDrawing && !wasDrawing)
        {
            brush.BeginStroke(brushTip);
        }
        else if (isDrawing && wasDrawing)
        {
            brush.UpdateStroke();
        }
        else if (!isDrawing && wasDrawing)
        {
            brush.EndStroke();
        }

        wasDrawing = isDrawing;
    }

    private void UpdateDepth()
    {
        if (!allowDepthChangeByWheel) return;

        float wheel = Input.mouseScrollDelta.y;

        if (Mathf.Abs(wheel) > 0.001f)
        {
            drawDepthFromCamera += wheel * wheelDepthSpeed;
            drawDepthFromCamera = Mathf.Clamp(drawDepthFromCamera, minDepth, maxDepth);
        }
    }

    private void UpdateBrushTipPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;

        // ここが重要。
        // mouse x/y は画面座標、z は「カメラから何m先か」
        mouseScreenPos.z = drawDepthFromCamera;

        Vector3 worldPos = targetCamera.ScreenToWorldPoint(mouseScreenPos);

        brushTip.position = worldPos;
        brushTip.rotation = targetCamera.transform.rotation;

        if (debugBrushTipVisual != null)
        {
            debugBrushTipVisual.position = worldPos;
            debugBrushTipVisual.rotation = brushTip.rotation;
        }
    }
}