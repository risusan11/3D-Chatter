using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DimensionSwitcher : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button switchButton;  // ボタン本体
    [SerializeField] private Text modeText;        // ボタンのテキスト

    // インスペクターからの手動設定を廃止
    private Camera dimensionCamera;

    // 現在のモードを記憶する変数（最初は3Dモードと仮定）
    private bool is3DMode = true;

    void Awake()
    {
        // シーン内の「MainCamera」タグを持つカメラを自動で取得
        dimensionCamera = Camera.main;

        if (dimensionCamera == null)
        {
            Debug.LogError("MainCameraタグのついたカメラが見つかりません。");
        }
    }

    void Start()
    {
        // 最初は3Dモードの表示にしておく
        UpdateUIText();

        // ボタンを押した時の処理の登録
        switchButton.onClick.AddListener(OnSwitchButtonClicked);
    }

    // ボタンが押されたときに呼ばれるメソッド
    void OnSwitchButtonClicked()
    {
        // boolの値を反転させる
        is3DMode = !is3DMode;

        // テキストの表示を更新
        UpdateUIText();

        // 実際の切り替え処理を実行
        ApplyDimensionMode();
    }

    // テキスト表示を更新するメソッド
    void UpdateUIText()
    {
        if (is3DMode)
        {
            modeText.text = "Switch to 2D";
        }
        else
        {
            modeText.text = "Switch to 3D";
        }
    }

    // カメラやブラシの2D/3D切り替えを実際に適用するメソッド
// DimensionSwitcher.cs の ApplyDimensionMode メソッド内
// DimensionSwitcher.cs の ApplyDimensionMode メソッド内
    void ApplyDimensionMode()
    {
        if (dimensionCamera == null) return;

        if (is3DMode)
        {
            dimensionCamera.orthographic = false; 
        }
        else
        {
            dimensionCamera.orthographic = true; 
            
            // 【追加】2Dモード時はカメラの角度を強制的に正面（水平）に向ける
            // Y軸（左右の向き）は維持し、X軸とZ軸を0にする
            Vector3 currentEuler = dimensionCamera.transform.localEulerAngles;
            dimensionCamera.transform.localRotation = Quaternion.Euler(0, currentEuler.y, 0);
        }
    }
}