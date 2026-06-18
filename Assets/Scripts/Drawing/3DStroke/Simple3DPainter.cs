using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class Simple3DPainter : MonoBehaviourPun
{
    public static Simple3DPainter Instance { get; private set; }

    [Header("描画初期設定")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.cyan;
    [Range(0.01f, 0.2f)] [SerializeField] private float lineWidth = 0.05f;

    [Header("3D空間設定")]
    [SerializeField] private float drawDepthFromCamera = 3f;
    [SerializeField] private float minPointDistance = 0.03f;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("消しゴム設定")]
    [SerializeField] private float eraseRadius = 0.25f;
    [SerializeField] private Color highlightColor = Color.white; 

    public List<LineRenderer> ActiveLines => activeLines;
    private List<LineRenderer> activeLines = new List<LineRenderer>();
    private LineRenderer currentLine; 
    
    private bool is3DDrawingMode = false;
    private bool isEraserMode = false;

    private Vector3 brushTipPosition;
    private Quaternion brushTipRotation;
    private bool isNearWall;

    private LineRenderer lockedLine = null;
    private Color lockedLineOriginalColor;

    public bool Is3DDrawingMode => is3DDrawingMode;
    public bool IsEraserMode => isEraserMode;
    public Vector3 BrushTipPosition => brushTipPosition;
    public Quaternion BrushTipRotation => brushTipRotation;
    public bool IsNearWall => isNearWall;
    
    public float CurrentLineWidth => lineWidth;
    public Color CurrentLineColor => lineColor;

    void Awake()
    {
        if (photonView.IsMine)
        {
            Instance = this;
        }
    }

    void Start()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            enabled = false;
            return;
        }
        if (targetCamera == null) targetCamera = Camera.main;

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
        {
            raycastMask = raycastMask & ~(1 << playerLayer);
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (RealisingMessageController.isChatting) return;

        var mode = ModeManager.Instance.Current;
        if (mode != ModeManager.AppMode.Draw3D)
        {
            if (currentLine != null) EndStroke();
            return;
        }

        CalculateBrushPosition();

        if (Input.GetMouseButtonDown(0)) StartStroke(brushTipPosition);
        else if (Input.GetMouseButton(0) && currentLine != null) UpdateStroke(brushTipPosition);
        else if (Input.GetMouseButtonUp(0)) EndStroke();
    }

    public void SetColor(Color newColor)
    {
        lineColor = newColor;
    }

    public void SetWidth(float newWidth)
    {
        lineWidth = Mathf.Clamp(newWidth, 0.01f, 0.5f);
    }

    private void CalculateBrushPosition()
    {
        Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, drawDepthFromCamera, raycastMask))
        {
            brushTipPosition = hit.point;
            brushTipRotation = Quaternion.LookRotation(hit.normal);
            isNearWall = true;
        }
        else
        {
            brushTipPosition = targetCamera.transform.position + targetCamera.transform.forward * drawDepthFromCamera;
            brushTipRotation = targetCamera.transform.rotation;
            isNearWall = false;
        }
    }

    private void StartStroke(Vector3 startPos)
    {
        GameObject lineObj = new GameObject("3D_Stroke_Preview");
        lineObj.transform.SetParent(transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.material.color = lineColor;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        
        lr.positionCount = 1;
        lr.SetPosition(0, startPos);

        currentLine = lr;
    }

    private void UpdateStroke(Vector3 currentPos)
    {
        Vector3 lastPos = currentLine.GetPosition(currentLine.positionCount - 1);
        if (Vector3.Distance(lastPos, currentPos) >= minPointDistance)
        {
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, currentPos);
        }
    }

    private void EndStroke()
    {
        if (currentLine != null)
        {
            int count = currentLine.positionCount;
            if (count >= 1)
            {
                Vector3[] pts = new Vector3[count];
                currentLine.GetPositions(pts);

                string hexColor = "#" + ColorUtility.ToHtmlStringRGBA(lineColor);
                float width = currentLine.startWidth;

                Destroy(currentLine.gameObject);

                photonView.RPC("SyncSpawn3DStroke", RpcTarget.All, pts, hexColor, width);
            }
            currentLine = null;
        }
    }

    
    [PunRPC]
    private void SyncSpawn3DStroke(Vector3[] points, string hexColor, float width)
    {
        GameObject lineObj = new GameObject("3D_SyncedStroke");
        lineObj.transform.SetParent(transform); // 描いた本人のプレイヤーオブジェクトの子にする

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        
        Color parsedColor = Color.white;
        ColorUtility.TryParseHtmlString(hexColor, out parsedColor);
        lr.material.color = parsedColor;
        lr.startWidth = width;
        lr.endWidth = width;

        lr.positionCount = points.Length;
        lr.SetPositions(points);

        
        activeLines.Add(lr);
    }

    [PunRPC]
    private void Erase3DStrokeRemote(int strokeIndex)
    {
        if (strokeIndex >= 0 && strokeIndex < activeLines.Count)
        {
            LineRenderer targetLr = activeLines[strokeIndex];
            if (targetLr != null)
            {
                if (targetLr.material != null) Destroy(targetLr.material);
                Destroy(targetLr.gameObject);
                
                activeLines[strokeIndex] = null; 
                
                if (targetLr == lockedLine)
                {
                    lockedLine = null;
                }
                Debug.Log($"[3D消しゴム同期] インデックス {strokeIndex} の3D空中線を消去しました。");
            }
        }
    }

    private void ScanAndHighlightStroke(Vector3 eraserPos)
    {
        LineRenderer closestLine = null;
        float closestDistance = eraseRadius;


        Simple3DPainter[] allPainters = FindObjectsOfType<Simple3DPainter>();

        foreach (var painter in allPainters)
        {
            for (int i = painter.activeLines.Count - 1; i >= 0; i--)
            {
                LineRenderer lr = painter.activeLines[i];
                if (lr == null) continue; // 消去済みのヌル席はスルー

                int pointCount = lr.positionCount;
                for (int p = 0; p < pointCount; p++)
                {
                    float dist = Vector3.Distance(eraserPos, lr.GetPosition(p));
                    if (dist <= closestDistance)
                    {
                        closestDistance = dist;
                        closestLine = lr;
                    }
                }
            }
        }

        if (closestLine != lockedLine)
        {
            ResetHoverHighlight();

            if (closestLine != null)
            {
                lockedLine = closestLine;
                lockedLineOriginalColor = closestLine.material.color;
                lockedLine.material.color = highlightColor;
            }
        }
    }

    private void ResetHoverHighlight()
    {
        if (lockedLine != null)
        {
            if (lockedLine.material != null) 
            {
                lockedLine.material.color = lockedLineOriginalColor;
            }
            lockedLine = null;
        }
    }
}