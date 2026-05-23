using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Painter3D
{
    public class StrokeRenderer_LineRenderer : StrokeRenderer
    {
        public LineRenderer m_LineRenderer;

        public override void DrawStroke(bool forceRedraw)
        {
            base.DrawStroke(forceRedraw);

            m_LineRenderer.positionCount = m_Stroke.RawNodeCount;
            m_LineRenderer.startWidth = AdjustedScale;
            m_LineRenderer.endWidth = AdjustedScale;

            for (int i = 0; i < m_Stroke.RawNodeCount; i++)
            {
                // ローカル座標をワールド座標に変換してセット
                Vector3 worldPos = m_Stroke.transform.TransformPoint(m_Stroke.GetPositionAt(i));
                m_LineRenderer.SetPosition(i, worldPos);
            }
        }

        public override void SetMaterial(Material mat)
        {
            base.SetMaterial(mat);

            m_LineRenderer.material = mat;
        }

        public override void SetColour(Color col)
        {
            base.SetColour(col);

            m_LineRenderer.startColor = col;
            m_LineRenderer.endColor = col;
        }

        public override void SetRenderState(bool active)
        {
            m_LineRenderer.enabled = active;
        }
    }
}
