using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.EventSystems;
using UnityEditor;

public class RealisingMessageController : MonoBehaviourPunCallbacks
{
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
        sendButton.onClick.AddListener(OnClickSend);
    }

    void Update()
    {
        if (InnerText.isFocused && !isChatting)
        {
            isChatting = true;
            Cursor.lockState = CursorLockMode.None; // カーソルを表示して動かせるようにする
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
            }
        }

        if (isChatting && !InnerText.isFocused)
        {
            if (Input.GetMouseButtonDown(0) && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
            {
                CancelChat();
            }
        }
if (Input.GetKeyDown(KeyCode.Escape) && !isChatting) // チャット中でない時だけ判定
        {
            if (!isCanvas2Active)
            {
                canvas2Instance = Instantiate(canvas2Prefab, Vector3.zero, Quaternion.identity);
                
                isCanvas2Active = true;
            }
            else
            {
                Destroy(canvas2Instance);
                isCanvas2Active = false;
            }
        }
    }

    void CancelChat()
    {
        isChatting = false;
        
        InnerText.text = ""; 

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Cursor.lockState = CursorLockMode.Locked;
    }

    void OnClickSend()
    {
        if (string.IsNullOrEmpty(InnerText.text))
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
        GameObject othersMessage = Instantiate(prefab_A, canvas, false);

        RectTransform rt = othersMessage.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(286f, 132f);

        Text nameText = othersMessage.transform
            .Find("PlayerPanel/PlayerName")
            .GetComponent<Text>();

        Text msgText  = othersMessage.transform
            .Find("player-message")
            .GetComponent<Text>();

        nameText.text = name;
        msgText.text  = message;
    }
}