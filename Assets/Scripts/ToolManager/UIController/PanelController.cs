using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject panelScroll;

    [SerializeField] private Button SizeAppButton;   // 
    [SerializeField] private Button SizeDisAppButton; // 

    private bool wasIdel = false; // 
    private bool isMinimized = false; // 

    void Start()
    {
        // 
        panel.SetActive(true);
        panelScroll.SetActive(true);
        SizeAppButton.gameObject.SetActive(true);
        SizeDisAppButton.gameObject.SetActive(false);

        SizeAppButton.onClick.AddListener(OnSizeButtonClick);
        SizeDisAppButton.onClick.AddListener(OnSizeDisButtonClick);

        wasIdel = RealisingMessageController.isidel;
    }

    void Update()
    {
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
        isMinimized = true; // 

        panel.SetActive(false);
        panelScroll.SetActive(false);
        SizeAppButton.gameObject.SetActive(false);
        SizeDisAppButton.gameObject.SetActive(true);
    }

    // パネルを開く（再表示）ボタンを押したとき
    void OnSizeDisButtonClick()
    {
        isMinimized = false; 

        panel.SetActive(true);
        panelScroll.SetActive(true);
        SizeAppButton.gameObject.SetActive(true);
        SizeDisAppButton.gameObject.SetActive(false);
    }
}