using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject panelScroll;

    [SerializeField] private Button SizeAppButton;   // パネルを閉じる（最小化）ボタン
    [SerializeField] private Button SizeDisAppButton; // パネルを開く（再表示）ボタン

    private bool wasIdel = false; // 💡前回のフレームのお絵描き状態を記憶する変数
    private bool isMinimized = false; // 💡手動で最小化しているかどうかのフラグ

    void Start()
    {
        // 初期の表示設定（通常状態）
        panel.SetActive(true);
        panelScroll.SetActive(true);
        SizeAppButton.gameObject.SetActive(true);
        SizeDisAppButton.gameObject.SetActive(false);

        // ボタンのイベント登録
        SizeAppButton.onClick.AddListener(OnSizeButtonClick);
        SizeDisAppButton.onClick.AddListener(OnSizeDisButtonClick);

        // 初期状態を同期
        wasIdel = RealisingMessageController.isidel;
    }

    void Update()
    {
        // 💡重要：お絵描き状態（isidel）が「変化した瞬間」だけ処理を走らせる
        if (RealisingMessageController.isidel != wasIdel)
        {
            wasIdel = RealisingMessageController.isidel; // 状態を更新

            if (RealisingMessageController.isidel)
            {
                // 【お絵描き開始時】強制的に全部非表示にする
                panel.SetActive(false);
                panelScroll.SetActive(false);
                SizeAppButton.gameObject.SetActive(false);
                SizeDisAppButton.gameObject.SetActive(false);
            }
            else
            {
                // 【お絵描き終了時】通常の画面に戻る
                // 💡お絵描き前に「手動で最小化していたか」に合わせて戻し方を変える
                if (isMinimized)
                {
                    // 最小化していたなら、最小化した状態に戻す
                    panel.SetActive(false);
                    panelScroll.SetActive(false);
                    SizeAppButton.gameObject.SetActive(false);
                    SizeDisAppButton.gameObject.SetActive(true);
                }
                else
                {
                    // 通常表示だったなら、普通に表示する
                    panel.SetActive(true);
                    panelScroll.SetActive(true);
                    SizeAppButton.gameObject.SetActive(true);
                    SizeDisAppButton.gameObject.SetActive(false);
                }
            }
        }
    }

    // パネルを閉じる（最小化）ボタンを押したとき
    void OnSizeButtonClick()
    {
        isMinimized = true; // 💡最小化フラグをONにする

        panel.SetActive(false);
        panelScroll.SetActive(false);
        SizeAppButton.gameObject.SetActive(false);
        SizeDisAppButton.gameObject.SetActive(true);
    }

    // パネルを開く（再表示）ボタンを押したとき
    void OnSizeDisButtonClick()
    {
        isMinimized = false; // 💡最小化フラグをOFFにする

        panel.SetActive(true);
        panelScroll.SetActive(true);
        SizeAppButton.gameObject.SetActive(true);
        SizeDisAppButton.gameObject.SetActive(false);
    }
}