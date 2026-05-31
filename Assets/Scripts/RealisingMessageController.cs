using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.EventSystems;

public class RealisingMessageController : MonoBehaviourPunCallbacks
{
    // 💡【マルチ対応】ホスト・ゲスト全員が、自分のプレハブボタンから直接このスクリプトを叩くための唯一の窓口
    public static RealisingMessageController Instance { get; private set; }

    public static bool isidel = false;
    
    [Header("📋 チャット用プレハブ素材")]
    [SerializeField] private GameObject prefab_A;
    [SerializeField] private Button sendButton; 
    [SerializeField] private InputField InnerText;

    [Header("📂 メッセージの受け皿")]
    [SerializeField] private RectTransform content; 

    [Header("⚙️ 設定画面の設定")]
    [SerializeField] private GameObject canvas2Prefab;
    
    private GameObject canvas2Instance;

    public static bool isChatting = false;
    public static bool isCanvas2Active = false; // カメラ制御などが状態を覗き込むための共有フラグ

    void Awake()
    {
        // 🛠️【バグ根絶】アバターではないので、ホスト・ゲスト全員の画面でこのオブジェクトを Instance に登録する！
        Instance = this;
    }

    void Start()
    {
        isChatting = false;
        isCanvas2Active = false;

        // 🛠️【バグ根絶】ホストかゲストかに関わらず、全員が自分のローカル画面にあるUIを自動で繋ぎ直す！
        if (InnerText == null) InnerText = FindObjectOfType<InputField>();
        
        if (sendButton == null)
        {
            GameObject btnObj = GameObject.Find("SendButton");
            if (btnObj != null) sendButton = btnObj.GetComponent<Button>();
            else sendButton = FindObjectOfType<Button>();
        }

        if (content == null)
        {
            GameObject contentObj = GameObject.Find("Content");
            if (contentObj != null) content = contentObj.GetComponent<RectTransform>();
        }

        if (sendButton != null) sendButton.onClick.AddListener(OnClickSend);
    }

    void Update()
    {
        if (isidel)
        {
            if (canvas2Instance != null)
            {
                Destroy(canvas2Instance);
                isCanvas2Active = false;
                Cursor.lockState = CursorLockMode.None;
            }
            return;
        }

        // ❌【諸悪の根源を完全抹殺！】
        // if (photonView != null && !photonView.IsMine) return; 
        // ⬆️ これが残っていたせいが、同時に1人しか使えない原因の100%すべてでした。

        // ── ⚙️ 1. 設定画面（Canvas2プレハブ）の生成判定 ──
        // 誰の画面であっても、完全に独立して自分のPC上でESCキーの開閉が走るようになります！
        if (Input.GetKeyDown(KeyCode.Escape) && !isChatting)
        {
            ToggleSettingsCanvas2();
        }

        // ── ⌨️ 2. チャット（文字入力）の処理 ──
        if (InnerText != null)
        {
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
        }
    }

    // ⚙️ 設定画面の生成・破棄を完璧に統治する関数
    private void ToggleSettingsCanvas2()
    {
        if (!isCanvas2Active)
        {
            if (canvas2Prefab == null)
            {
                canvas2Prefab = Resources.Load<GameObject>("Canvas2");
            }

            if (canvas2Prefab == null)
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject go in allObjects)
                {
                    if (go.name == "Canvas2" && go.transform.parent == null)
                    {
                        canvas2Prefab = go;
                        break;
                    }
                }
            }

            if (canvas2Prefab != null)
            {
                canvas2Instance = Instantiate(canvas2Prefab);
                canvas2Instance.SetActive(true);

                canvas2Instance.transform.SetParent(null); 
                
                Canvas canvas = canvas2Instance.GetComponent<Canvas>();
                if (canvas == null) canvas = canvas2Instance.GetComponentInChildren<Canvas>();
                
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 999; 
                }

                RectTransform rt = canvas2Instance.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = Vector3.one;       
                    rt.anchoredPosition = Vector2.zero; 
                    rt.localPosition = Vector3.zero;
                }

                isCanvas2Active = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("[Realising完結システム] 画面最前面に設定画面を強制実体化しました。");
            }
            else
            {
                Debug.LogError("[Realising致命的エラー] 'Canvas2' のプレハブ素材が世界のどこにも見つかりません！");
            }
        }
        else
        {
            if (canvas2Instance != null)
            {
                Destroy(canvas2Instance);
            }
            isCanvas2Active = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[Realising完結システム] 設定画面を安全に Destroy しました。");
        }
    }

    // 🌟【2Dボタン連動用窓口】
    public void CloseCanvas2From2DButton()
    {
        if (canvas2Instance != null)
        {
            Destroy(canvas2Instance);
        }
        
        isCanvas2Active = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[Realising完結システム] 2Dモード移行のため設定画面を Destroy しました。");
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
        
        if (photonView == null) return;
        if (PhotonNetwork.LocalPlayer == null) return;
        
        // 💡 シーンに1つしかない共有オブジェクトのPhotonViewを介して、誰のPCからでも全員の画面へチャットを送信！
        photonView.RPC(nameof(MakeText), RpcTarget.All,
            PhotonNetwork.LocalPlayer.NickName + PhotonNetwork.LocalPlayer.ActorNumber,
            InnerText.text
        );
    }

    [PunRPC]
    void MakeText(string name, string message)
    {
        if (prefab_A == null || content == null) return;

        GameObject othersMessage = Instantiate(prefab_A, content, false);
        othersMessage.SetActive(true);

        Transform nameTransform = othersMessage.transform.Find("PlayerPanel/PlayerName");
        Transform msgTransform = othersMessage.transform.Find("player-message");

        if (nameTransform != null) nameTransform.GetComponent<Text>().text = name;
        if (msgTransform != null) msgTransform.GetComponent<Text>().text = message;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
}