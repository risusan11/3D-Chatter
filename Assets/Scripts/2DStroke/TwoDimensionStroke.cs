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
    [SerializeField] Color lineColor;
    [Range(0.1f, 0.5f)] float lineWidth = 0.1f;

    bool isDrawing = false;

    [SerializeField] Slider WeightSlider;
    [SerializeField] Button ExitButton;
    [SerializeField] Button SavingButton;
    [SerializeField] Button ClearButton;
    
    List<LineRenderer> lineRenderers;

    void Start()
    {
        isDrawing = true;
        WeightSlider.gameObject.SetActive(false);
        ExitButton.gameObject.SetActive(false);
        SavingButton.gameObject.SetActive(false);
        ClearButton.gameObject.SetActive(false);
        
        lineRenderers = new List<LineRenderer>();
        WeightSlider.value = lineWidth;

        SavingButton.onClick.AddListener(OnSaveAndClick);
        ClearButton.onClick.AddListener(ClearAllLines);
        ExitButton.onClick.AddListener(CloseMenu);

        WeightSlider.onValueChanged.AddListener((val) => {
            lineWidth = val;
        });
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
            WeightSlider.gameObject.SetActive(true);
            ExitButton.gameObject.SetActive(true);
            SavingButton.gameObject.SetActive(true);
            ClearButton.gameObject.SetActive(true);
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
    }

    void CloseMenu()
    {
        WeightSlider.gameObject.SetActive(false);
        ExitButton.gameObject.SetActive(false);
        SavingButton.gameObject.SetActive(false);
        ClearButton.gameObject.SetActive(false);
        isDrawing = true;
    }

    void OnSaveAndClick()
    {
        StartCoroutine(SaveCanvasAsImageAndLoad());
    }

    IEnumerator SaveCanvasAsImageAndLoad()
    {
        WeightSlider.gameObject.SetActive(false);
        ExitButton.gameObject.SetActive(false);
        SavingButton.gameObject.SetActive(false);
        ClearButton.gameObject.SetActive(false);

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

    // --- 描画ロジック群 ---
    void _addLineObject()
    {
        GameObject lineObj = new GameObject("Stroke");
        
        // 💡【修正】このスクリプト（2DCanvasシーン）の子供にする！
        // これでシーン破棄時に描いた線も連動して綺麗に削除されます
        lineObj.transform.SetParent(transform);
        
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lineRenderers.Add(lr);
        lineObj.transform.localPosition = Vector3.zero;

        _initRenderers();
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
    }

    void _addPositionDataToLineRendererList()
    {
        LineRenderer lastLine = lineRenderers.Last();
        Vector3 mousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, 5.0f);
        Vector3 worldPosition = drawingCamera.ScreenToWorldPoint(mousePosition);
        
        if (lastLine.positionCount > 0)
        {
            Vector3 lastPoint = lastLine.GetPosition(lastLine.positionCount - 1);
            if (Vector3.Distance(lastPoint, worldPosition) < 0.05f)
            {
                return;
            }
        }

        lastLine.positionCount += 1;
        lastLine.SetPosition(lastLine.positionCount - 1, worldPosition);
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
        SceneManager.UnloadSceneAsync(gameObject.scene); // 💡より安全な自身のシーン指定に変更
        RealisingMessageController.isidel = false;
    }
}