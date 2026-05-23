using UnityEngine;
using UnityEngine.EventSystems; // Required for UI checks
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
        if (targetCamera == null) targetCamera = Camera.main;

        // Create the tip and parent it to this object to prevent hierarchy clutter and memory leaks
        GameObject tipObj = new GameObject("Mouse3D_BrushTip");
        brushTip = tipObj.transform;
        brushTip.SetParent(this.transform);
    }

    private void OnDestroy()
    {
        // Safety cleanup if the tip was unparented for any reason
        if (brushTip != null) Destroy(brushTip.gameObject);
    }

    private void Update()
    {
        if(RealisingMessageController.isidel) return; // ロード後はこのスクリプトの処理を止める
        if (targetCamera == null || brush == null) return;

        UpdateDepth();
        UpdateBrushTipPosition();

        // Check if mouse is down AND make sure we aren't clicking on a UI element
        bool isPointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool isDrawing = Input.GetMouseButton(0) && !isPointerOverUI && Application.isFocused;

        // Stroke Logic
        if (isDrawing && !wasDrawing)
        {
            brush.BeginStroke(brushTip);
            wasDrawing = true;
        }
        else if (isDrawing && wasDrawing)
        {
            brush.UpdateStroke();
        }
        else if (!Input.GetMouseButton(0) && wasDrawing) 
        {
            // Explicitly checking GetMouseButton(0) being false ensures the stroke ends 
            // even if the user dragged over UI mid-stroke.
            brush.EndStroke();
            wasDrawing = false;
        }
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