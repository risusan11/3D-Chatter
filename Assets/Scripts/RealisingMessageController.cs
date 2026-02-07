using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System;

public class RealisingMessageController : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject prefab_A;
    [SerializeField] private Button sendButton; 

    [SerializeField] private InputField InnerText;
    [SerializeField] private Transform canvas;

    void Start()
    {

        sendButton.onClick.AddListener(OnClickSend);
    }

    void OnClickSend()
    {
        
        
        photonView.RPC(nameof(MakeText), RpcTarget.All,
        PhotonNetwork.LocalPlayer.NickName+PhotonNetwork.LocalPlayer.ActorNumber,
        InnerText.text
        );

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
