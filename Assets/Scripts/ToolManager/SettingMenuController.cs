using UnityEngine;
using UnityEngine.UI;

public class SettingMenuController : MonoBehaviour
{
    [Header("太さ変更用のSliderバー")]
    [SerializeField] private Slider widthSlider;


    [SerializeField] private FlexibleColorPicker colorPicker;

    private int openFrameCounter = 0; // 
    private bool isSyncing = false;   // 

    void Start()
    {
        if (widthSlider != null) widthSlider.onValueChanged.AddListener(OnWidthSliderChanged);
        if (colorPicker != null) colorPicker.onColorChange.AddListener(OnColorChanged);
    }

    void OnEnable()
    {
        openFrameCounter = 5;
    }

    void Update()
    {
        if (openFrameCounter > 0)
        {
            openFrameCounter--;
            if (openFrameCounter == 0 && Simple3DPainter.Instance != null)
            {
                SyncUIFromAvatar();
            }
        }
    }

    private void SyncUIFromAvatar()
    {
        isSyncing = true; 

        try
        {
            if (widthSlider != null)
            {
                widthSlider.minValue = 0.01f;
                widthSlider.maxValue = 0.20f;
                widthSlider.value = Simple3DPainter.Instance.CurrentLineWidth;
            }

            if (colorPicker != null)
            {
                colorPicker.color = Simple3DPainter.Instance.CurrentLineColor;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SettingMenu] アセット初期化の競合を安全にキャッチしました: {e.Message}");
        }
        finally
        {
            isSyncing = false; 
        }
    }

    private void OnWidthSliderChanged(float value)
    {
        if (isSyncing) return;

        if (Simple3DPainter.Instance != null)
        {
            Simple3DPainter.Instance.SetWidth(value);
        }
    }

    private void OnColorChanged(Color color)
    {
        if (isSyncing) return;

        if (Simple3DPainter.Instance != null)
        {
            Simple3DPainter.Instance.SetColor(color);
        }
    }
}