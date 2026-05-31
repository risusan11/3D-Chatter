using UnityEngine;
using UnityEngine.UI;

public class SettingMenuController : MonoBehaviour
{
    [Header("📏 太さ変更用のSliderバー")]
    [SerializeField] private Slider widthSlider;

    [Header("🎨 アセットのカラーピッカー")]
    [SerializeField] private FlexibleColorPicker colorPicker;

    private int openFrameCounter = 0; // 💡 UIの初期化が落ち着くのを待つカウンター
    private bool isSyncing = false;   // ループ防壁フラグ

    void Start()
    {
        // 起動時に1回だけイベントを登録
        if (widthSlider != null) widthSlider.onValueChanged.AddListener(OnWidthSliderChanged);
        if (colorPicker != null) colorPicker.onColorChange.AddListener(OnColorChanged);
    }

    void OnEnable()
    {
        // 💡 画面が開いた瞬間、5フレームのカウントダウンを開始
        openFrameCounter = 5;
    }

    void Update()
    {
        // ── 🧠 ライフサイクルの罠を完全に回避するディレイド・シンクロ ──
        // 画面が開いた直後はアセット内部のUI生成やレイアウト計算でバタバタしています。
        // アセットが勝手に初期色（赤）をバラ撒き終わるのを待ってから、安全に上書きします。
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
        // 🛡️ ループガード起動
        isSyncing = true; 

        // 💡 try-finally 構文を使用し、万が一アセット内部で予期せぬエラーが起きても
        // 確実にループガードを解除（falseに戻す）して操作不能になるのを防ぎます。
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
                // アバターの現在の色をアセットに逆流適用（5フレーム後なので安全！）
                colorPicker.color = Simple3DPainter.Instance.CurrentLineColor;
            }
        }
        catch (System.Exception e)
        {
            // エラーを検知してもログに残すだけで、ゲーム全体のクラッシュを防ぐ
            Debug.LogWarning($"[SettingMenu] アセット初期化の競合を安全にキャッチしました: {e.Message}");
        }
        finally
        {
            // 🛡️ 【最重要】何があっても、この中の中身は絶対に最後に実行される
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