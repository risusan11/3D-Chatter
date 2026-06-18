using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using Photon.Pun;
using UnityEngine.SceneManagement;
public class MapRoomManager : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update\
    string roomName;
    [SerializeField] private Button backButton;
    void Start()
    {

            backButton.onClick.AddListener(() =>PhotonNetwork.LeaveRoom());
    }       
    override public void OnLeftRoom()
    {
        Debug.Log("部屋から退出");
        StartCoroutine(GoToLobby());   // すぐ移動せず、少し待つ
    }

    IEnumerator GoToLobby()
    {
        yield return new WaitForSeconds(0.3f);   // 0.3秒待つ
        SceneManager.LoadScene("Lobby");
    }



    void Update()
    {

    }
}
