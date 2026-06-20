using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using ExitGames.Client.Photon;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private Button ListButton;
    [SerializeField] private Button IndexButton;
    [SerializeField] private Transform ListPanel;
    public static string roomSceneName = "Main";

    void Start()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinLobby();
        }
        else if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    override public void OnConnectedToMaster()
    {
        Debug.Log("マスターサーバーに接続したようです");
        PhotonNetwork.JoinLobby();
    }

    override public void OnJoinedLobby()
    {
        Debug.Log("待機室に入ったようです");
        IndexButton.onClick.AddListener(() => {
            PhotonNetwork.JoinOrCreateRoom(PhotonNetwork.NickName + "'s Room", new RoomOptions(), TypedLobby.Default);
        });
    }

    override public void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 🌟 作成者：プロパティを保存「だけ」する（LoadLevelはまだしない）
            Hashtable props = new Hashtable();
            props["sceneName"] = roomSceneName;

            if (roomSceneName == "OriginalScene")
            {
                props["mapData"] = string.IsNullOrEmpty(MapGeneratorSettingMangager.mapData)
                    ? "a0"
                    : MapGeneratorSettingMangager.mapData;

                Debug.Log($"[Lobby] 🌟 保存する mapData = '{props["mapData"]}'");
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            // 🌟 ここではLoadLevelしない。保存が反映されたOnRoomPropertiesUpdateで切り替える
        }
        else
        {
            // 🌟 入室者：すでに保存済みのはずなので、その場で読んで遷移
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("sceneName", out object scene))
            {
                Debug.Log($"[Lobby] 🌟 入室者がsceneName読めた = '{scene as string}'");
                PhotonNetwork.LoadLevel(scene as string);
            }
            else
            {
                Debug.LogWarning("[Lobby] ⚠️ sceneNameが読めず Main へフォールバック");
                PhotonNetwork.LoadLevel("Main");
            }
        }

        Debug.Log("部屋に入ったようです");
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        // 作成者だけがここでシーン遷移する
        if (!PhotonNetwork.IsMasterClient) return;

        if (changedProps.TryGetValue("sceneName", out object scene))
        {
            Debug.Log($"[Lobby] 🌟 保存反映を確認。シーン遷移 = '{scene as string}'");
            PhotonNetwork.LoadLevel(scene as string);
        }
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
            if (room.RemovedFromList) continue;
            var btn = Instantiate(ListButton, ListPanel.transform);
            btn.GetComponentInChildren<Text>().text = room.Name;
            string roomName = room.Name;

            btn.onClick.AddListener(() => {
                if (PhotonNetwork.InLobby)
                {
                    PhotonNetwork.JoinRoom(roomName);
                }
                else
                {
                    Debug.Log("ロビーにいないため、部屋に入れません");
                }
            });
        }
    }

    void Update()
    {

    }
}