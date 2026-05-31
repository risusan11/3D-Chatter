using UnityEngine;

// ============================================================
//  DrawingManager.cs
//  シーン跨ぎでお絵描きデータを保持するシングルトン
//
//  【配置】
//   - 空の GameObject "DrawingManager" にアタッチするか、
//     CanvasDrawer.cs が自動生成する（どちらでもよい）
// ============================================================

public class DrawingManager : MonoBehaviour
{
    // ── シングルトン ──────────────────────────────────────────

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

    // ── データ保持 ────────────────────────────────────────────

    /// <summary>現在保存されているお絵描きデータ</summary>
    public DrawingData CurrentDrawing { get; private set; }

    /// <summary>JSON 文字列（Photon RPC / Instantiate 用）</summary>
    public string CurrentJson { get; private set; }

    /// <summary>有効なデータが存在するか</summary>
    public bool HasDrawing => CurrentDrawing != null && !CurrentDrawing.IsEmpty;

    // ── API ───────────────────────────────────────────────────

    /// <summary>2D キャンバスから呼ぶ。データを JSON に変換して保存。</summary>
    public void SaveDrawing(DrawingData data)
    {
        CurrentDrawing = data;
        CurrentJson    = data.ToJson();

        Debug.Log($"[DrawingManager] 保存完了 — {data.strokes.Count} ストローク / {data.TotalPoints} 点");
    }

    /// <summary>データをクリアする</summary>
    public void Clear()
    {
        CurrentDrawing = null;
        CurrentJson    = null;
    }
}
