using UnityEngine;



public class DrawingManager : MonoBehaviour
{

    public static DrawingManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public DrawingData CurrentDrawing { get; private set; }

    public string CurrentJson { get; private set; }

    public bool HasDrawing => CurrentDrawing != null && !CurrentDrawing.IsEmpty;


    public void SaveDrawing(DrawingData data)
    {
        CurrentDrawing = data;
        CurrentJson    = data.ToJson();

        Debug.Log($"[DrawingManager] 保存完了 — {data.strokes.Count} ストローク / {data.TotalPoints} 点");
    }

    public void Clear()
    {
        CurrentDrawing = null;
        CurrentJson    = null;
    }
}
