using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using Photon.Pun;
public class MapRoomManager : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update\
    string roomName;
    [SerializeField] private Button mapA;
    void Start()
    {
        if (roomName != "MapA")
        {
            mapA.onClick.AddListener(() => GoToMap("MapA"));
        }
    }       
    void GoToMap(string mapName)
        {
            roomName = mapName;
            PhotonNetwork.LeaveRoom();        
            }
  public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions(), TypedLobby.Default);
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("✅ 入った部屋: " + PhotonNetwork.CurrentRoom.Name);
        // ここでマップシーンに遷移する
        PhotonNetwork.LoadLevel(roomName);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
