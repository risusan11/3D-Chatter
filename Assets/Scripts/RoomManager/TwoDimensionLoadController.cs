using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class TwoDimensionLoadController : MonoBehaviour
{
    [SerializeField] private Button LoadButton; // ロードボタンの参照
    // Start is called before the first frame update
    void Start()
    {
        RealisingMessageController.isidel = false; // ロード前はこのスクリプトの処理を有効にする
        LoadButton.onClick.AddListener(LoadDrawing); // ボタンにクリックイベントを追加
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void LoadDrawing()
    {
        SceneManager.LoadSceneAsync("2DCanvas", LoadSceneMode.Additive);
            RealisingMessageController.isidel = true;
    }
}
