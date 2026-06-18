using UnityEngine;

public class CloseCanvasTrigger : MonoBehaviour
{
    public void OnClick2DButton()
    {
        if (RealisingMessageController.Instance != null)
        {
            RealisingMessageController.Instance.CloseCanvas2From2DButton();
        }
    }
}