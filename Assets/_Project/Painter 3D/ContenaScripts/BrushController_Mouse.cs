using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Painter3D
{
    public class BrushController_Mouse : MonoBehaviour
    {
        public Camera m_Cam;        
        public Brush m_Brush;
        public Transform m_BrushTip;
        public bool m_UpdateFacing = false;
        float m_Depth = 3;
        public float m_Smoothing = 0f;

        public float maxReach = 10f; 

        public bool is3DMode = true; 

        // ★★★ ここを追加：Raycastから除外するレイヤーを指定する ★★★
        public LayerMask drawLayerMask = ~0; 
        // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

        private void Awake()
    {
        if (m_Cam == null)
        {
            m_Cam = GetComponentInChildren<Camera>();
        }

        if (m_Cam == null)
        {
            m_Cam = Camera.main;
        }

        if (m_Cam == null)
        {
            Debug.LogError("BrushController_Mouse: Camera が見つかりません。Playerプレハブ内のCameraを m_Cam に設定してください。", this);
        }
    }
        void Start()
        {
            // 強制上書きを削除し、未設定の場合のみ自分を代入する
            if (m_BrushTip == null)
            {
                m_BrushTip = transform;
            }
            
            // Brush.csの初期化に合わせてサイズ設定を同期
            if (m_Brush != null)
            {
                m_Brush.BrushSize = m_Brush.BrushSize; 
            }
        }

        // Update is called once per frame
        void Update()
        {    
            if(RealisingMessageController.isidel) return; // ロード後はこのスクリプトの処理を止める   

            if (RealisingMessageController.isCanvas2Active || RealisingMessageController.isChatting) return;

            m_Brush.m_InputOverUI = false; // No UI in project, so always allow input

            UpdateBrushTipTransformFromMouse();

            if (Input.GetMouseButtonDown(0))
            {
                m_Brush.BeginStroke(transform);
            }
            else if (Input.GetMouseButton(0) && m_Brush.Painting)
            {
                m_Brush.UpdateStroke();
            }
            else if (Input.GetMouseButtonUp(0) && m_Brush.Painting)
            {
                m_Brush.EndStroke();
            }


            // Move canvas
            if (Input.GetMouseButtonDown(1))
            {
                m_UpdateFacing = false;
                Painter3DManager.Instance.ActiveCanvas.BeginMoveCanvas(m_BrushTip);
            }
            else if(Input.GetMouseButton(1))
            {
                

            }
            else if (Input.GetMouseButtonUp(1))
            {
                m_UpdateFacing = true;
                Painter3DManager.Instance.ActiveCanvas.EndCanvasMove();
            }

            if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0 )
            {
                Painter3DManager.Instance.ActiveCanvas.Scale(Input.GetAxis("Mouse ScrollWheel"));
            }
        }

        void UpdateBrushTipTransformFromMouse()
        {
            Ray ray = m_Cam.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPos;
            string debugInfo = "";

            if (is3DMode)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, maxReach, drawLayerMask))
                {
                    debugInfo = $"命中: {hit.collider.name} | hit.point={hit.point} | distance={hit.distance}";
                    targetPos = hit.point + hit.normal * 0.01f;
                    m_BrushTip.rotation = Quaternion.LookRotation(hit.normal);
                }
                else
                {
                    debugInfo = "ハズレ（空中描画）";
                    targetPos = ray.GetPoint(m_Depth);
                    m_BrushTip.rotation = m_Cam.transform.rotation;
                }
            }
            else
            {
                debugInfo = "2Dモード";
                targetPos = ray.GetPoint(m_Depth);
                m_BrushTip.rotation = m_Cam.transform.rotation;
            }

            if (m_Smoothing > Mathf.Epsilon)
                targetPos = Vector3.Lerp(m_BrushTip.position, targetPos, Time.deltaTime * m_Smoothing);

            transform.position = targetPos;

            // クリック時だけログ出力（毎フレーム出ると多すぎるため）
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log($"[クリック時] {debugInfo} → 最終transform.position={transform.position}");
            }
        }
        
        void UpdateTipAngleOnXY(Vector3 currentPos, Vector3 targetPos)
        {
            var newRotation = Quaternion.LookRotation(currentPos - targetPos, Vector3.forward);
            newRotation.x = 0.0f;
            newRotation.y = 0.0f;
            m_BrushTip.rotation = Quaternion.Slerp(m_BrushTip.transform.rotation, newRotation, 1);
        }
    }
}