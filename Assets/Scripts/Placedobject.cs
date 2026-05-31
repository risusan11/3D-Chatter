using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// ObjectPlacer で配置されたオブジェクトにアタッチする（Resources 内のプレハブに付ける）。
///
/// 役割：
///   - 生成時に InstantiationData からスケールを受け取って適用
///   - シーン内の全 PlacedObject を静的リストで管理（消しゴム探索用）
///   - ハイライト（消去ホバー時に赤く光る）
///   - 削除・移動の同期処理
///
/// セッション中のみ保持（シーン退出で消える）= 通常 Instantiate なので追加設定不要。
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PlacedObject : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    // ✅ シーン内の全配置オブジェクトを一元管理（ObjectPlacer の消しゴム探索用）
    public static List<PlacedObject> AllPlacedInScene = new List<PlacedObject>();

    private List<Renderer> renderers = new List<Renderer>();
    private List<Color[]>  originalColors = new List<Color[]>(); // 各Rendererの元色（マテリアルごと）
    private bool highlighted = false;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void OnEnable()  { if (!AllPlacedInScene.Contains(this)) AllPlacedInScene.Add(this); }
    void OnDisable() { AllPlacedInScene.Remove(this); }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // スケール適用
        object[] data = info.photonView.InstantiationData;
        if (data != null && data.Length >= 3)
        {
            transform.localScale = new Vector3((float)data[0], (float)data[1], (float)data[2]);
        }

        // ハイライト用に全Rendererと元色をキャッシュ
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        renderers.Clear();
        originalColors.Clear();

        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            renderers.Add(rend);

            // 各マテリアルの元色を保存
            Color[] cols = new Color[rend.materials.Length];
            for (int i = 0; i < rend.materials.Length; i++)
                cols[i] = rend.materials[i].HasProperty("_Color")
                          ? rend.materials[i].color : Color.white;
            originalColors.Add(cols);
        }
    }

    // ────────────────────────────────────────────
    // ハイライト（ローカル表示のみ。同期不要）
    // ────────────────────────────────────────────

    public void SetHighlight(bool on, Color highlightColor)
    {
        if (on == highlighted) return;
        highlighted = on;

        for (int r = 0; r < renderers.Count; r++)
        {
            if (renderers[r] == null) continue;
            var mats = renderers[r].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null || !mats[m].HasProperty("_Color")) continue;
                mats[m].color = on ? highlightColor : originalColors[r][m];
            }
        }
    }

    // ────────────────────────────────────────────
    // 削除（全員同期）
    // ────────────────────────────────────────────

    /// <summary>全員の画面からこのオブジェクトを削除。所有権を取得してから削除する。</summary>
    public void RequestDelete()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (!photonView.IsMine) photonView.RequestOwnership();
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject); // オフラインテスト用
        }
    }

    // ────────────────────────────────────────────
    // 移動（全員同期）
    //
    // ※ リアルタイム同期には PhotonTransformView が必要。
    //   プレハブに PhotonTransformView を付けて PhotonView の
    //   Observed Components に登録すると、transform 変更が自動同期される。
    // ────────────────────────────────────────────

    /// <summary>移動を開始（所有権を取得）。</summary>
    public void BeginMove()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
            photonView.RequestOwnership();
    }

    /// <summary>移動中（毎フレーム呼ぶ）。PhotonTransformView が同期を担う。</summary>
    public void UpdateMove(Vector3 newPosition, Quaternion newRotation)
    {
        transform.SetPositionAndRotation(newPosition, newRotation);
    }

    void OnDestroy()
    {
        AllPlacedInScene.Remove(this);
    }
}