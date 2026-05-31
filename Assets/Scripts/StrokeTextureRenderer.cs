using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DrawingData をテクスチャに描画するユーティリティ。
///
/// ✅【重要】筆は「楕円」で打つ。
///   2Dキャンバスの normX / normY は Screen.width / Screen.height で別々に正規化されているため
///   X方向とY方向の単位スケールが異なる（アスペクト比による歪み）。
///   そのため筆を「真円」で打つと、縦方向の太さが不足して隣接ストロークの間に隙間ができる。
///   筆を「縦長の楕円（landscape の場合）」にすることで、2Dと同じ筆運びを再現できる。
/// </summary>
public static class StrokeTextureRenderer
{
    public static Texture2D Render(DrawingData drawing, int texSize, int excludeIndex = -1)
    {
        Color32[] pixels = new Color32[texSize * texSize];

        // ✅ アスペクト比を取得（旧データや未設定の場合は 1.0 = 正方形扱い）
        float aspect = (drawing.aspectRatio > 0.001f) ? drawing.aspectRatio : 1.0f;

        for (int si = 0; si < drawing.strokes.Count; si++)
        {
            if (si == excludeIndex) continue;

            StrokeData stroke = drawing.strokes[si];
            Color32 c32 = ParseColor(stroke.colorHex);

            StampStroke(pixels, texSize, stroke, c32, aspect);
        }

        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    public static Texture2D RenderWithHighlight(DrawingData drawing, int texSize,
                                                 int highlightIndex, Color highlightColor)
    {
        Texture2D tex = Render(drawing, texSize);

        if (highlightIndex >= 0 && highlightIndex < drawing.strokes.Count)
        {
            Color32[] pixels = tex.GetPixels32();
            float aspect = (drawing.aspectRatio > 0.001f) ? drawing.aspectRatio : 1.0f;
            StampStroke(pixels, texSize, drawing.strokes[highlightIndex], (Color32)highlightColor, aspect);
            tex.SetPixels32(pixels);
            tex.Apply();
        }
        return tex;
    }

    /// <summary>
    /// 全ストロークを単一色で焼く（配置プレビュー用の黄色ゴースト等に使う）。
    /// 各ストロークの本来の色を無視し、uniformColor で塗りつぶす。
    /// </summary>
    public static Texture2D RenderWithUniformColor(DrawingData drawing, int texSize, Color uniformColor)
    {
        Color32[] pixels = new Color32[texSize * texSize];
        float aspect = (drawing.aspectRatio > 0.001f) ? drawing.aspectRatio : 1.0f;
        Color32 c32 = (Color32)uniformColor;

        foreach (var stroke in drawing.strokes)
        {
            StampStroke(pixels, texSize, stroke, c32, aspect);
        }

        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // ────────────────────────────────────────────
    // ストローク全体をテクスチャに焼き込む
    // ────────────────────────────────────────────

    private static void StampStroke(Color32[] pixels, int texSize, StrokeData stroke, Color32 c32, float aspect)
    {
        // ✅ ブラシ半径を X / Y で別計算
        //    radius_x : キャンバス幅基準（normalizedWidth × texSize / 2）
        //    radius_y : キャンバス高さ基準（radius_x × aspect）
        //
        //    landscape (aspect > 1) の場合、radius_y > radius_x → 縦長楕円
        int radiusX = Mathf.Max(1, Mathf.RoundToInt(stroke.normalizedWidth * texSize * 0.5f));
        int radiusY = Mathf.Max(1, Mathf.RoundToInt(radiusX * aspect));

        // 補間ステップは小さい方の半径基準（密に打つ）
        int stepRadius = Mathf.Min(radiusX, radiusY);

        for (int pi = 0; pi < stroke.points.Count; pi++)
        {
            StrokePointData pt = stroke.points[pi];
            int cx = Mathf.RoundToInt((pt.x * 0.5f + 0.5f) * texSize);
            int cy = Mathf.RoundToInt((pt.y * 0.5f + 0.5f) * texSize);

            if (pi == 0)
            {
                StampEllipse(pixels, texSize, cx, cy, radiusX, radiusY, c32);
            }
            else
            {
                StrokePointData prev = stroke.points[pi - 1];
                int px = Mathf.RoundToInt((prev.x * 0.5f + 0.5f) * texSize);
                int py = Mathf.RoundToInt((prev.y * 0.5f + 0.5f) * texSize);

                float dist = Mathf.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py));
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(1f, stepRadius * 0.25f)));

                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    int ix = Mathf.RoundToInt(Mathf.Lerp(px, cx, t));
                    int iy = Mathf.RoundToInt(Mathf.Lerp(py, cy, t));
                    StampEllipse(pixels, texSize, ix, iy, radiusX, radiusY, c32);
                }
            }
        }
    }

    // ────────────────────────────────────────────
    // 楕円スタンプ（真円ではなく X/Y で半径が異なる）
    //
    // 楕円の判定式: (x - cx)² / rX² + (y - cy)² / rY² ≤ 1
    // ────────────────────────────────────────────

    private static void StampEllipse(Color32[] pixels, int texSize, int cx, int cy,
                                     int rX, int rY, Color32 color)
    {
        int x0 = Mathf.Max(0, cx - rX);
        int x1 = Mathf.Min(texSize - 1, cx + rX);
        int y0 = Mathf.Max(0, cy - rY);
        int y1 = Mathf.Min(texSize - 1, cy + rY);

        float rX2 = rX * rX;
        float rY2 = rY * rY;
        bool opaqueColor = (color.a == 255);

        for (int y = y0; y <= y1; y++)
        {
            int rowStart = y * texSize;
            float dy = y - cy;
            float dyTerm = (dy * dy) / rY2;

            // この行で楕円内に入る可能性が無ければスキップ
            if (dyTerm > 1f) continue;

            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx;
                if ((dx * dx) / rX2 + dyTerm > 1f) continue; // 楕円の外

                int idx = rowStart + x;

                if (opaqueColor)
                {
                    pixels[idx] = color;
                }
                else
                {
                    BlendOver(pixels, idx, color, 1f);
                }
            }
        }
    }

    /// <summary>
    /// 標準アルファ合成（半透明色を描く場合のみ使用）。
    /// </summary>
    private static void BlendOver(Color32[] pixels, int idx, Color32 color, float coverage)
    {
        Color32 dst = pixels[idx];

        float srcA = coverage * (color.a / 255f);
        float dstA = dst.a / 255f;
        float invSrcA = 1f - srcA;
        float newA = srcA + dstA * invSrcA;

        if (newA < 0.0001f) return;

        float invNewA = 1f / newA;
        float rOut = (color.r * srcA + dst.r * dstA * invSrcA) * invNewA;
        float gOut = (color.g * srcA + dst.g * dstA * invSrcA) * invNewA;
        float bOut = (color.b * srcA + dst.b * dstA * invSrcA) * invNewA;

        pixels[idx] = new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(rOut), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(gOut), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(bOut), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(newA * 255f), 0, 255)
        );
    }

    private static Color32 ParseColor(string hex)
    {
        Color c = Color.white;
        ColorUtility.TryParseHtmlString(hex, out c);
        return (Color32)c;
    }
}