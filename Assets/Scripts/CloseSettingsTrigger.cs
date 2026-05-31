using UnityEngine;

public class CloseCanvasTrigger : MonoBehaviour
{
    // 💡 プレハブボタンの On Click () から呼び出す関数
    public void OnClick2DButton()
    {
        // 世界の唯一の窓口（Instance）を直接叩いて、設定画面のクローンを消し去る！
        if (RealisingMessageController.Instance != null)
        {
            RealisingMessageController.Instance.CloseCanvas2From2DButton();
        }
    }
}