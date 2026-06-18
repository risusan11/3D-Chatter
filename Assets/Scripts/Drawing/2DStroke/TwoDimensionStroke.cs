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
    [SerializeField] Color lineColor = Color.white;
    [Range(0.1f, 0.5f)] float lineWidth = 0.1f;

    bool isDrawing = false;

    [Header("UI一括管理パネル（MenuPanelをハメる）")]
    [SerializeField] private GameObject menuPanel;

    [SerializeField] Slider WeightSlider;
    [SerializeField] Button ExitButton;
    [SerializeField] Button SavingButton;
    [SerializeField] Button ClearButton;

    [Header("カラーピッカーアセットの設定")]
    [SerializeField] private FlexibleColorPicker colorPicker;

    List<LineRenderer> lineRenderers;

    private DrawingData drawingData = new DrawingData();
    private StrokeData currentStrokeData;

    // PlayerPrefs キー定数
    private const string PREF_COLOR_R = "Brush_R";
    private const string PREF_COLOR_G = "Brush_G";
    private const string PREF_COLOR_B = "Brush_B";
    private const string PREF_COLOR_A = "Brush_A";
    private const string PREF_WIDTH   = "Brush_Width";

    void Start()
    {
        isDrawing = true;

        if (menuPanel != null) menuPanel.SetActive(false);

        lineRenderers = new List<LineRenderer>();

        if (DrawingManager.Instance == null)
            new GameObject("DrawingManager").AddComponent<DrawingManager>();

 
        if (PlayerPrefs.HasKey(PREF_COLOR_R))
        {
            // 前回保存した色を復元
            lineColor = new Color(
                PlayerPrefs.GetFloat(PREF_COLOR_R),
                PlayerPrefs.GetFloat(PREF_COLOR_G),
                PlayerPrefs.GetFloat(PREF_COLOR_B),
                PlayerPrefs.GetFloat(PREF_COLOR_A, 1f)
            );
        }
        else if (colorPicker != null)
        {
            lineColor = colorPicker.color;
        }

        if (colorPicker != null)
        {
            colorPicker.color = lineColor;

            colorPicker.onColorChange.AddListener((newColor) =>
            {
                lineColor = newColor;

                PlayerPrefs.SetFloat(PREF_COLOR_R, newColor.r);
                PlayerPrefs.SetFloat(PREF_COLOR_G, newColor.g);
                PlayerPrefs.SetFloat(PREF_COLOR_B, newColor.b);
                PlayerPrefs.SetFloat(PREF_COLOR_A, newColor.a);
                PlayerPrefs.Save();
            });
        }

 
        lineWidth = PlayerPrefs.GetFloat(PREF_WIDTH, lineWidth);
        WeightSlider.value = lineWidth;

        WeightSlider.onValueChanged.AddListener((val) =>
        {
            lineWidth = val;
            PlayerPrefs.SetFloat(PREF_WIDTH, val);
            PlayerPrefs.Save();
        });

        SavingButton.onClick.AddListener(OnSaveAndClick);
        ClearButton.onClick.AddListener(ClearAllLines);
        ExitButton.onClick.AddListener(CloseMenu);
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
            ClearAllLines();

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            isDrawing = false;
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        if (!isDrawing) return;

        if (Input.GetMouseButtonDown(0))
            _addLineObject();

        if (Input.GetMouseButton(0) && lineRenderers.Count > 0)
            _addPositionDataToLineRendererList();
    }

    void ClearAllLines()
    {
        foreach (var line in lineRenderers)
            if (line != null) Destroy(line.gameObject);
        lineRenderers.Clear();
        drawingData = new DrawingData();
    }

    void CloseMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        isDrawing = true;
    }

    void OnSaveAndClick()
    {
        if (drawingData != null && !drawingData.IsEmpty)
            DrawingManager.Instance.SaveDrawing(drawingData);

        StartCoroutine(SaveCanvasAsImageAndLoad());
    }

    IEnumerator SaveCanvasAsImageAndLoad()
    {
        if (menuPanel != null) menuPanel.SetActive(false);

        yield return new WaitForEndOfFrame();

        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        Destroy(tex);

        string directoryPath = Path.Combine(Application.persistentDataPath, "SavedDrawings");
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

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

        if (drawingData.strokes.Count == 0 && drawingCamera != null)
        {
            drawingData.aspectRatio = drawingCamera.aspect;
        }

      
        float canvasWorldWidth = GetCanvasWorldWidth();
        float normalized = (canvasWorldWidth > 0f) ? (lineWidth / canvasWorldWidth) : lineWidth;

        currentStrokeData = new StrokeData
        {
            colorHex       = "#" + ColorUtility.ToHtmlStringRGBA(lineColor),
            normalizedWidth = normalized
        };
        drawingData.strokes.Add(currentStrokeData);
    }

   
    private float GetCanvasWorldWidth()
    {
        if (drawingCamera == null) return 1f;

        if (drawingCamera.orthographic)
        {
            // 平行投影：ワールド幅 = 2 × orthographicSize × アスペクト比
            return 2f * drawingCamera.orthographicSize * drawingCamera.aspect;
        }
        else
        {
            // 透視投影：描画 z 深度でのワールド幅
            // _addPositionDataToLineRendererList と同じ z = 5.0f を使用
            const float drawDepth = 5.0f;
            return 2f * Mathf.Tan(drawingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                   * drawDepth * drawingCamera.aspect;
        }
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
        lastLine.sortingOrder = lineRenderers.Count;
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

        float normX = (Input.mousePosition.x / Screen.width)  * 2f - 1f;
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