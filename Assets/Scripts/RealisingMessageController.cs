using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.EventSystems;

public class RealisingMessageController : MonoBehaviourPunCallbacks
{
    public static bool isidel = false;
    [SerializeField] private GameObject prefab_A;
    [SerializeField] private Button sendButton; 
    [SerializeField] private InputField InnerText;
    [SerializeField] private Transform canvas;

    [SerializeField] private GameObject canvas2Prefab;
    private GameObject canvas2Instance;

    public static bool isChatting = false;
    public static bool isCanvas2Active = false;

    void Start()
    {
        isChatting = false;
        isCanvas2Active = false;
        if (sendButton != null) sendButton.onClick.AddListener(OnClickSend);
    }

    void Update()
    {
        // 💡【修正】お絵描きロードが入った時の処理
        if (isidel)
        {
            // 設定画面（メニュー）が開いたままだったら、1回だけ閉じる
            if (canvas2Instance != null)
            {
                Destroy(canvas2Instance);
                isCanvas2Active = false;
                Cursor.lockState = CursorLockMode.None; // お絵描き用にカーソルを出す
            }
            return; // 💡処理をここで止める（毎フレーム暴発を防ぐ）
        }

        // 🌟 自分のアバター以外は入力を受け付けない
        if (photonView != null && !photonView.IsMine) return;

        // --- 1. チャットの判定 ---
        if (InnerText.isFocused && !isChatting)
        {
            isChatting = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (isChatting)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnClickSend();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelChat();
                return;
            }
        }

        if (isChatting && !InnerText.isFocused)
        {
            if (Input.GetMouseButtonDown(0) && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
            {
                CancelChat();
            }
            else if (Input.GetMouseButtonDown(0))
            {
                isChatting = false;
            }
        }

        // --- 2. 設定画面の判定 ---
        if (Input.GetKeyDown(KeyCode.Escape) && !isChatting)
        {
            if (!isCanvas2Active)
            {
                if (canvas2Prefab != null)
                {
                    canvas2Instance = Instantiate(canvas2Prefab, Vector3.zero, Quaternion.identity);
                    isCanvas2Active = true;
                    Cursor.lockState = CursorLockMode.None;
                }
            }
            else
            {
                if (canvas2Instance != null) Destroy(canvas2Instance);
                isCanvas2Active = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    void CancelChat()
    {
        isChatting = false;
        if (InnerText != null) InnerText.text = ""; 

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (!isCanvas2Active)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void OnClickSend()
    {
        if (InnerText == null || string.IsNullOrEmpty(InnerText.text))
        {
            CancelChat();
            return;
        }
        
        photonView.RPC(nameof(MakeText), RpcTarget.All,
            PhotonNetwork.LocalPlayer.NickName + PhotonNetwork.LocalPlayer.ActorNumber,
            InnerText.text
        );

        CancelChat(); 
    }

    [PunRPC]
    void MakeText(string name, string message)
    {
        if (prefab_A == null || canvas == null) return;
        GameObject othersMessage = Instantiate(prefab_A, canvas, false);

        RectTransform rt = othersMessage.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = new Vector2(286f, 132f);

        Transform nameTransform = othersMessage.transform.Find("PlayerPanel/PlayerName");
        Transform msgTransform = othersMessage.transform.Find("player-message");

        if (nameTransform != null) nameTransform.GetComponent<Text>().text = name;
        if (msgTransform != null) msgTransform.GetComponent<Text>().text = message;
    }
}