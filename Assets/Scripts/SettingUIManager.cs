using UnityEngine;

// 🌟 シーンに1つだけ配置する、UI（設定画面）管理専用のスクリプト
public class SettingsUIManager : MonoBehaviour
{
    [Header("設定画面の親オブジェクト（Canvas2などのパネル）")]
    public GameObject settingsPanel;

    void Start()
    {
        // 1. ゲーム開始時は設定画面を閉じておく
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        
        // 2. 他のスクリプト（カメラ制御など）のフラグも false にリセット
        // ※RealisingMessageControllerがシーンに存在しなくてもエラーにならないよう配慮
        RealisingMessageController.isCanvas2Active = false;
    }

    void Update()
    {
        // 3. ESCキーが押された瞬間を検知して、開閉メソッドを呼ぶ
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettings();
        }
    }

    // 設定画面の「開く/閉じる」を切り替えるメソッド（ボタンからも呼べます）
    public void ToggleSettings()
    {
        if (settingsPanel == null) return;

        // 現在の状態（表示されているか）を取得し、反転させる
        bool isOpening = !settingsPanel.activeSelf;
        settingsPanel.SetActive(isOpening);

        // 🌟 最重要：ここでカメラ制御スクリプトのフラグと連動させる！
        RealisingMessageController.isCanvas2Active = isOpening;

        // カーソルの状態をUIに合わせて強制的に上書きする
        if (isOpening)
        {
            // 設定画面が開いた：カーソルを自由にして表示
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 設定画面が閉じた：カーソルを画面中央にロックして隠す
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}