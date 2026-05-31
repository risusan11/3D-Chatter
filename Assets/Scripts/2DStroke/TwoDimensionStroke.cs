using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TwoDimensionStroke : MonoBehaviour
{
    [SerializeField] Camera drawingCamera;
    [SerializeField] Material lineMaterial;
    [SerializeField] Color lineColor = Color.white; // ブラシの現在色
    [Range(0.1f, 0.5f)] float lineWidth = 0.1f;

    bool isDrawing = false;

    [Header("🏗️ UI一括管理パネル（MenuPanelをハメる）")]
    [SerializeField] private GameObject menuPanel; // 💡これ1つで背景ごと全部管理する！

    [SerializeField] Slider WeightSlider;
    [SerializeField] Button ExitButton;
    [SerializeField] Button SavingButton;
    [SerializeField] Button ClearButton;

    [Header("🎨 カラーピッカーアセットの設定")]
    [SerializeField] private FlexibleColorPicker colorPicker; 
    
    List<LineRenderer> lineRenderers;

    // ── 3D配置システムと連動する内部データ ──
    private DrawingData drawingData = new DrawingData();
    private StrokeData currentStrokeData;

    void Start()
    {
        isDrawing = true;
        
        // 💡 起動時はメニューパネル（背景と中のUI全部）を眠らせておく
        if (menuPanel != null) menuPanel.SetActive(false);

        lineRenderers = new List<LineRenderer>();
        WeightSlider.value = lineWidth;

        if (DrawingManager.Instance == null)
        {
            new GameObject("DrawingManager").AddComponent<DrawingManager>();
        }

        SavingButton.onClick.AddListener(OnSaveAndClick);
        ClearButton.onClick.AddListener(ClearAllLines);
        ExitButton.onClick.AddListener(CloseMenu);

        WeightSlider.onValueChanged.AddListener((val) => {
            lineWidth = val;
        });

        if (colorPicker != null)
        {
            colorPicker.onColorChange.AddListener((newColor) => {
                lineColor = newColor; 
            });
        }
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            ClearAllLines();
        }

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            isDrawing = false;
            
            // 💡 Escapeを押したら背景パネルごと一発で全UIを表示！
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        if (!isDrawing) return;

        if (Input.GetMouseButtonDown(0))
        {
            _addLineObject();
        }

        if (Input.GetMouseButton(0) && lineRenderers.Count > 0)
        {
            _addPositionDataToLineRendererList();
        }
    }

    void ClearAllLines()
    {
        foreach (var line in lineRenderers)
        {
            if (line != null) Destroy(line.gameObject);
        }
        lineRenderers.Clear();
        drawingData = new DrawingData();
    }

    void CloseMenu()
    {
        // 💡 閉じる時もパネルごと一発で非表示にするだけ
        if (menuPanel != null) menuPanel.SetActive(false);
        
        isDrawing = true;
    }

    void OnSaveAndClick()
    {
        if (drawingData != null && !drawingData.IsEmpty)
        {
            DrawingManager.Instance.SaveDrawing(drawingData);
        }

        StartCoroutine(SaveCanvasAsImageAndLoad());
    }

    IEnumerator SaveCanvasAsImageAndLoad()
    {
        // 💡 写真を撮る時はメニューパネル（背景含め全部）を隠す
        if (menuPanel != null) menuPanel.SetActive(false);

        yield return new WaitForEndOfFrame();

        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        string directoryPath = Path.Combine(Application.persistentDataPath, "SavedDrawings");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        string filePath = Path.Combine(directoryPath, $"drawing_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");

        File.WriteAllBytes(filePath, bytes);
        Debug.Log("お絵描きを保存しました: " + filePath);

        LoadingScene();
    }

    void _addLineObject()
    {
        GameObject lineObj = new GameObject("Stroke");
        lineObj.transform.SetParent(transform);
        
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lineRenderers.Add(lr);
        lineObj.transform.localPosition = Vector3.zero;

        _initRenderers();

        currentStrokeData = new StrokeData
        {
            colorHex = "#" + ColorUtility.ToHtmlStringRGBA(lineColor),
            normalizedWidth = lineWidth * 0.05f 
        };
        drawingData.strokes.Add(currentStrokeData);
    }

void _initRenderers()
    {
        LineRenderer lastLine = lineRenderers.Last();
        lastLine.positionCount = 0;
        lastLine.material = lineMaterial;
        lastLine.material.color = lineColor; 
        lastLine.startWidth = lineWidth;
        lastLine.endWidth = lineWidth;
        lastLine.useWorldSpace = true;
        
        // 💡 犯人はここ！sortingOrderは線同士の重なり順を管理していますが、
        // 別のレイヤー（UI）との前後関係までは制御できていませんでした。
        lastLine.sortingOrder = lineRenderers.Count;

        // ✨【解決策】この線をお絵描き専用のSorting Layer「Drawing」に割り当てる！
        // ※スペルミスに注意してね！ステップ1で作った名前と完全に一致させます。
        lastLine.sortingLayerName = "Drawing"; 
    }

    void _addPositionDataToLineRendererList()
    {
        LineRenderer lastLine = lineRenderers.Last();
        Vector3 mousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 5.0f);
        Vector3 worldPosition = drawingCamera.ScreenToWorldPoint(mousePosition);
        
        if (lastLine.positionCount > 0)
        {
            Vector3 lastPoint = lastLine.GetPosition(lastLine.positionCount - 1);
            if (Vector3.Distance(lastPoint, worldPosition) < 0.05f) return;
        }

        lastLine.positionCount += 1;
        lastLine.SetPosition(lastLine.positionCount - 1, worldPosition);

        float normX = (Input.mousePosition.x / Screen.width) * 2f - 1f;
        float normY = (Input.mousePosition.y / Screen.height) * 2f - 1f;
        currentStrokeData.points.Add(new StrokePointData(normX, normY));
    }

    void LoadingScene()
    {
        if (!string.IsNullOrEmpty(PhotonNetwork.NickName))
        {
            ClearAllLines();
            BackToChatScene();
        }
        else
        {
            SceneManager.LoadScene("Start");
            Debug.Log("You've got to Enter available name for your Nickname.");
        }
    }

    void BackToChatScene()
    {
        SceneManager.UnloadSceneAsync(gameObject.scene); 
        RealisingMessageController.isidel = false;
    }
}