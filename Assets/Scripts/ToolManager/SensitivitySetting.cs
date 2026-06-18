using UnityEngine;
using UnityEngine.UI;

public class SensitivitySetting : MonoBehaviour
{
    private Slider slider;

    void Start()
    {
        slider = GetComponent<Slider>();
        
        // メニューが開かれた時、現在の感度に合わせてスライダーのツマミの位置を合わせる
        slider.value = PlayerCameraControl.sharedSensitivity;
        
        // スライダーが動かされた時に呼ばれる処理を登録
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    // スライダーの値が変更された時に自動で呼ばれる
    void OnSliderValueChanged(float newValue)
    {
        // 共有の感度（static変数）を、スライダーの新しい値で上書きする！
        PlayerCameraControl.sharedSensitivity = newValue;
    }
}