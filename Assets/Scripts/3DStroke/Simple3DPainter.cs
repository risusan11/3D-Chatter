using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class Simple3DPainter : MonoBehaviourPun
{
    public static Simple3DPainter Instance { get; private set; }

    [Header("🎨 描画初期設定")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.cyan;
    [Range(0.01f, 0.2f)] [SerializeField] private float lineWidth = 0.05f;

    [Header("📏 3D空間設定")]
    [SerializeField] private float drawDepthFromCamera = 3f;
    [SerializeField] private float minPointDistance = 0.03f;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("🧹 消しゴム設定")]
    [SerializeField] private float eraseRadius = 0.25f;
    [SerializeField] private Color highlightColor = Color.white; 

    // 💡 外部（Placer）から覗き込めるようにリストを公開しておく
    public List<LineRenderer> ActiveLines => activeLines;
    private List<LineRenderer> activeLines = new List<LineRenderer>();
    private LineRenderer currentLine; // 描画中の「仮」の線
    
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

        if (Input.GetKeyDown(KeyCode.B))
        {
            is3DDrawingMode = !is3DDrawingMode;
            if (!is3DDrawingMode)
            {
                EndStroke();
                ResetHoverHighlight();
                isEraserMode = false;
            }
        }

        if (!is3DDrawingMode) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            isEraserMode = !isEraserMode;
            EndStroke();
            ResetHoverHighlight();
        }

        CalculateBrushPosition();

        if (isEraserMode)
        {
            ScanAndHighlightStroke(brushTipPosition);

            // 🖱️ 消しゴム確定処理の同期化
            if (lockedLine != null && Input.GetMouseButton(0))
            {
                // 💡【新設】狙った線が「誰のアバターに属しているか」を親から特定する
                Simple3DPainter ownerPainter = lockedLine.GetComponentInParent<Simple3DPainter>();
                if (ownerPainter != null)
                {
                    int strokeIdx = ownerPainter.activeLines.IndexOf(lockedLine);
                    if (strokeIdx != -1)
                    {
                        // その線の持ち主のアバターのPhotonViewを介して、全プレイヤーに消去RPCを要請！
                        ownerPainter.photonView.RPC("Erase3DStrokeRemote", RpcTarget.All, strokeIdx);
                    }
                }
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) StartStroke(brushTipPosition);
            else if (Input.GetMouseButton(0) && currentLine != null) UpdateStroke(brushTipPosition);
            else if (Input.GetMouseButtonUp(0)) EndStroke();
        }
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
        // 💡 描いている最中は、自分の画面だけに一時的な「仮の線」を作っておく
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
        // ※バグ防止のため、描いてる最中の仮の線はまだ activeLines リストには入れません。
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
                // 1. 今ローカルで描き終わった仮の線の「全頂点座標」を配列に引っこ抜く
                Vector3[] pts = new Vector3[count];
                currentLine.GetPositions(pts);

                // 2. 色をPhoton通信用にHTMLカラー文字列に変える
                string hexColor = "#" + ColorUtility.ToHtmlStringRGBA(lineColor);
                float width = currentLine.startWidth;

                // 3. 用済みのローカル仮オブジェクトは一旦消去
                Destroy(currentLine.gameObject);

                // 🚀 4. 【核心】全員の画面（自分含む）に向かって、確定生成のRPCをドカンと投げる！
                photonView.RPC("SyncSpawn3DStroke", RpcTarget.All, pts, hexColor, width);
            }
            currentLine = null;
        }
    }

    // ✨【新設】全員の画面で、全く同じ3Dの線を同じアバターの足元に完全復元するRPC
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

        // 💡 全員の画面で、全く同じ順番（インデックス）でリストに格納されるため、背番号が1ミリもズレない！
        activeLines.Add(lr);
    }

    // ✨【新設】指定された背番号の3D線だけを、全員の画面からピンポイント消去するRPC
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

        // シーン内のすべてのSimple3DPainterコンポーネント（全員分のお絵描きリスト）を網羅してサーチ
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
            // 💡 すでに消去されている場合は色を戻す処理をスキップ
            if (lockedLine.material != null) 
            {
                lockedLine.material.color = lockedLineOriginalColor;
            }
            lockedLine = null;
        }
    }
}