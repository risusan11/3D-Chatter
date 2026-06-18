using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PlayFab;
using PlayFab.ClientModels;
using Unity.VisualScripting;

public class StartSetup : MonoBehaviourPunCallbacks
{
    [SerializeField] private InputField InnerText;
    [SerializeField] private Button StartButton;

    [Header("PlayFab / Photon 設定")]
    [Tooltip("PhotonServerSettings の『App Id PUN』を貼り付ける")]
    [SerializeField] private string photonAppId = "";

    [Tooltip("ログイン完了後に進むシーン名")]
    [SerializeField] private string nextSceneName = "Loading";

    [Header("演出")]
    [Tooltip("名前を復元したときに出す『おかえり』ポップアップ（任意・未割当でも動く）")]
    [SerializeField] private FadePopup welcomePopup;

    // 匿名IDを保存するキー（PlayerPrefs側）
    private const string CustomIdKey = "PLAYFAB_CUSTOM_ID";
    // PlayFabユーザーデータ上で名前を保存するキー
    private const string NicknameDataKey = "Nickname";

    private string playFabId;        // ログイン成功後に入る
    private bool isLoggedIn = false; // 匿名ログインが完了したか
    private bool isProcessing = false; // 二重押し防止

    string allowed = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@_";

    private void Start()
    {
        StartButton.onClick.AddListener(OnClickStart);

        LoginAnonymously();
    }


    private void LoginAnonymously()
    {
        string customId = PlayerPrefs.GetString(CustomIdKey, "");
        if (string.IsNullOrEmpty(customId))
        {
            customId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString(CustomIdKey, customId);
            PlayerPrefs.Save();
        }

        var request = new LoginWithCustomIDRequest
        {
            CustomId = customId,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnPlayFabLoginSuccess, OnPlayFabError);
    }

    private void OnPlayFabLoginSuccess(LoginResult result)
    {
        playFabId = result.PlayFabId;
        isLoggedIn = true;
        Debug.Log("✅ PlayFab匿名ログイン完了: " + playFabId);

        LoadSavedNickname();
    }


    private void LoadSavedNickname()
    {
        var request = new GetUserDataRequest
        {
            Keys = new List<string> { NicknameDataKey }
        };

        PlayFabClientAPI.GetUserData(request, result =>
        {
            if (result.Data != null
                && result.Data.ContainsKey(NicknameDataKey)
                && string.IsNullOrEmpty(InnerText.text))
            {
                InnerText.text = result.Data[NicknameDataKey].Value;
                Debug.Log("前回の名前を復元: " + InnerText.text);

                if (welcomePopup != null)
                    welcomePopup.Show($"Name has been restored! Welcome back, {InnerText.text}!");
            }
        },
        error =>
        {
            // 名前の復元失敗は致命的ではないので、ログだけ出して続行
            Debug.LogWarning("[StartSetup] 名前の読み込みに失敗（初回なら正常）: " + error.GenerateErrorReport());
        });
    }

  
    void OnClickStart()
    {
        if (isProcessing) return;

        if (InnerText.text == "")
        {
            Debug.Log("You've got to Enter available name for your Nickname.");
            if (welcomePopup != null)
                welcomePopup.Show("You've got to Enter available name for your Nickname.");
            return;
        }

        if (InnerText.text.Length > 20)
        {
            Debug.Log("Nickname must be 20 characters or less.");
            if (welcomePopup != null)
                welcomePopup.Show("Nickname must be 20 characters or less.");
            return;
        }

        foreach (char c in InnerText.text)
        {
            if (!allowed.Contains(c))   // 許可リストに無い文字だったら
            {
                Debug.Log("Only letters, numbers, and some symbols are allowed.");
                if (welcomePopup != null)
                    welcomePopup.Show("Only letters, numbers, and some symbols are allowed.");
                return;
            }
        }

        if (!isLoggedIn)
        {
            Debug.Log("ログイン処理中です。少し待ってからもう一度押してください。");
            return;
        }

        isProcessing = true;
        StartButton.interactable = false;

        SaveNicknameThenContinue(InnerText.text);
    }

    private void SaveNicknameThenContinue(string nickname)
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> { { NicknameDataKey, nickname } }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result =>
            {
                Debug.Log("名前を保存しました: " + nickname);
                RequestPhotonToken(nickname);
            },
            OnPlayFabError);
    }


    private void RequestPhotonToken(string nickname)
    {
        var tokenRequest = new GetPhotonAuthenticationTokenRequest
        {
            PhotonApplicationId = photonAppId
        };

        PlayFabClientAPI.GetPhotonAuthenticationToken(
            tokenRequest,
            tokenResult => OnPhotonTokenReceived(nickname, tokenResult.PhotonCustomAuthenticationToken),
            OnPlayFabError);
    }


    private void OnPhotonTokenReceived(string nickname, string photonToken)
    {
        var authValues = new AuthenticationValues();
        authValues.AuthType = CustomAuthenticationType.Custom;
        authValues.AddAuthParameter("username", playFabId);
        authValues.AddAuthParameter("token", photonToken);

        PhotonNetwork.AuthValues = authValues;
        PhotonNetwork.NickName = nickname;


        SceneManager.LoadScene(nextSceneName);
    }

    private void OnPlayFabError(PlayFabError error)
    {
        string report = error.GenerateErrorReport();
        Debug.LogError("[StartSetup] PlayFab Error: " + report);

        isProcessing = false;
        if (StartButton != null) StartButton.interactable = true;
    }

    void Update()
    {

    }
}