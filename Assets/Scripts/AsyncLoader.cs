using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AsyncLoader : MonoBehaviourPunCallbacks
{
    [SerializeField] private Slider loadSlider;

    void Start()
    {
        loadSlider.value = 0f;
        // シーン同期を有効にする（重要：マスターに合わせる設定）
        PhotonNetwork.AutomaticallySyncScene = true; 
        PhotonNetwork.ConnectUsingSettings();
    }
            
    public override void OnConnectedToMaster() {
        loadSlider.value = 0.5f;
        PhotonNetwork.JoinOrCreateRoom("Room", new RoomOptions(), TypedLobby.Default);
    }

    public override void OnJoinedRoom() {
        loadSlider.value = 1.0f;
        
        // 【修正ポイント】
        // マスタークライアント（ID=1）だけがロードを実行する。
        // 他のプレイヤーは AutomaticallySyncScene によって自動的に Main シーンへ飛びます。
        if (PhotonNetwork.IsMasterClient) {
            StartCoroutine(LoadingWaiter());
        }
    }

    private IEnumerator LoadingWaiter()
    {
        Debug.Log("MasterClient is starting level load...");
        yield return new WaitForSeconds(1);
        
        // これを呼ぶと、ルーム内にいる全員の画面が Main に切り替わります
        PhotonNetwork.LoadLevel("Main");
    }
}