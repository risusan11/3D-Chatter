using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update
    [SerializeField] private Button ListButton;
    [SerializeField] private Button IndexButton;
    [SerializeField] private Transform ListPanel;
    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }else
        {
            PhotonNetwork.JoinLobby();
        }



    }
    
    override public void OnConnectedToMaster()
    {
        Debug.Log("✅ マスターサーバーに接続した");
        PhotonNetwork.JoinLobby();
    }
    override public void OnJoinedLobby()
    {
        Debug.Log("✅ 待機室に入った"); 
            IndexButton.onClick.AddListener(() => {
            PhotonNetwork.JoinOrCreateRoom(PhotonNetwork.NickName+"'s Room", new RoomOptions(), TypedLobby.Default);
        });
    }
    override public void OnJoinedRoom()
    {

        //if (!PhotonNetwork.IsMasterClient) return;
        PhotonNetwork.LoadLevel("Main");
        Debug.Log("✅ 部屋に入った");

    }
    override public void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (Transform child in ListPanel.transform)
        {
            if (child.gameObject != IndexButton.gameObject)
            {
                Destroy(child.gameObject);
            }
        }
        foreach (RoomInfo room in roomList)
        {
        var btn = Instantiate(ListButton, ListPanel.transform);
        btn.GetComponentInChildren<Text>().text = room.Name;
        string roomName = room.Name;
        
        btn.onClick.AddListener(() => {
            if (PhotonNetwork.InLobby){
                PhotonNetwork.JoinRoom(roomName);
                }else{
                    Debug.Log("ロビーにいないため、部屋に入れません");
                } 
            
        });
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }
}
