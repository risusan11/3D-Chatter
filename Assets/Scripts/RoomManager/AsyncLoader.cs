using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;          

public class AsyncLoader : MonoBehaviourPunCallbacks
{
    [SerializeField] private Slider loadSlider;

    void Start()
    {
        loadSlider.value = 0f;
        PhotonNetwork.AutomaticallySyncScene = true; 
        PhotonNetwork.ConnectUsingSettings();
    }
            
    public override void OnConnectedToMaster() {
        loadSlider.value = 0.5f;
        //PhotonNetwork.JoinOrCreateRoom("Room", new RoomOptions(), TypedLobby.Default);
        SceneManager.LoadScene("Lobby");
    }

    public override void OnJoinedRoom() {
        loadSlider.value = 1.0f;
        
        
        if (PhotonNetwork.IsMasterClient) {
            StartCoroutine(LoadingWaiter());
        }
    }

    private IEnumerator LoadingWaiter()
    {
        Debug.Log("MasterClient is starting level load...");
        yield return new WaitForSeconds(1);
        
        PhotonNetwork.LoadLevel("Main");
    }
}