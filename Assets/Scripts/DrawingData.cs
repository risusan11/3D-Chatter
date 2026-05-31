using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StrokePointData
{
    public float x;
    public float y;
    public StrokePointData() { }
    public StrokePointData(float x, float y) { this.x = x; this.y = y; }
    public Vector3 ToLocalVector3(float halfSize) =>
        new Vector3(x * halfSize, 0f, y * halfSize);
}

[Serializable]
public class StrokeData
{
    public List<StrokePointData> points = new List<StrokePointData>();
    public string colorHex = "#FFFFFFFF";
    public float normalizedWidth = 0.015f;
}

[Serializable]
public class DrawingData
{
    public List<StrokeData> strokes = new List<StrokeData>();

    // ✅ 【追加】2Dキャンバスのアスペクト比（Screen.width / Screen.height）
    //    画面が16:9なら 1.78。これがないと縦方向のブラシ太さが不足して隙間ができる。
    //    旧データとの互換性のため、0 や未設定の場合は 1.0（正方形）扱い。
    public float aspectRatio = 1.0f;

    public string ToJson(bool prettyPrint = false) =>
        JsonUtility.ToJson(this, prettyPrint);
    public static DrawingData FromJson(string json) =>
        JsonUtility.FromJson<DrawingData>(json);
    public bool IsEmpty => strokes == null || strokes.Count == 0;
    public int TotalPoints
    {
        get
        {
            int count = 0;
            foreach (var s in strokes) count += s.points.Count;
            return count;
        }
    }
}