using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SampleScene : MonoBehaviourPunCallbacks
{
    void Awake()
    {
        // 【重要】シーンを跨いだ時に停止していたメッセージ処理をここで再開する
        PhotonNetwork.IsMessageQueueRunning = true;
    }

    void Start()
    {
        // もしルームに接続されているなら生成する
        if (PhotonNetwork.InRoom)
        {
            SpawnPlayer();
        }
    }

    // まれにロードのタイミングでルーム入室が遅れる場合があるため、コールバックでも予備で対応
    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        // 既に生成済みでないかチェック（二重生成防止）
        // 自分のPhotonViewがシーン内にない場合のみ生成する
        var position = new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f));
        PhotonNetwork.Instantiate("Avatar", position, Quaternion.identity);
    }
}