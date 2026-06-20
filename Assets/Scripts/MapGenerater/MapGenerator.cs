using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;   // 🌟 追加：コールバックを使うため

public class MapGenerator : MonoBehaviourPunCallbacks   
{
    [SerializeField] private GameObject LandprefabA;
    [SerializeField] private GameObject LandprefabB;

    private float blockWidth = 10.0f; // x軸方向の幅 10m
    private float blockLength = 10.0f; // z軸方向の幅 10m
    private float stepHeight = 1.0f;   // 厚み 1m
    private string inputText;
    private string testMapData;

    private bool hasBuilt = false;   

    void Start()
    {
        Debug.Log($"[MapGen] Start呼ばれた / InRoom={PhotonNetwork.InRoom}");

        if (PhotonNetwork.InRoom)
        {
            TryBuildMap();
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("[MapGen] OnJoinedRoom呼ばれた");
        TryBuildMap();
    }

    private void TryBuildMap()
    {
        if (hasBuilt) return;   

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapData", out object value))
        {
            Debug.Log($"[MapGen] 🌟 mapData読めた = '{value as string}'");
            GenerateMap(value as string);
            hasBuilt = true;
        }
        else
        {
            Debug.LogWarning("[MapGen] ⚠️ mapDataがまだ読めない（プロパティ未着 or キー無し）");
        }
    }

    public void GenerateMap(string mapString)
    {
        if (string.IsNullOrEmpty(mapString))
        {
            Debug.LogWarning("[MapGen] ⚠️ Map string is empty or null.");
            return;
        }

        string[] rows = mapString.Split('\n');

        for (int z = 0; z < rows.Length; z++)
        {
            string[] columns = rows[z].Split(',');

            for (int x = 0; x < columns.Length; x++)
            {
                string tileData = columns[x].Trim();
                if (string.IsNullOrEmpty(tileData) || tileData.Length < 2) continue;

                char type = tileData[0];

                if (int.TryParse(tileData.Substring(1), out int heightLevel))
                {
                    SpawnColumn(type, x, z, heightLevel);
                }
            }
        }
    }

    private void SpawnColumn(char type, int x, int z, int heightLevel)
    {
        GameObject prefab = (type == 'a') ? LandprefabA
                        : (type == 'b') ? LandprefabB
                        : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[MapGen] ⚠️ prefabがnull（type='{type}'）。Inspectorの割り当てを確認");
            return;
        }

        int   blocks       = heightLevel + 1;
        float pillarHeight = blocks * stepHeight;

        float posX = x * blockWidth;
        float posZ = -z * blockLength;
        float posY = pillarHeight * 0.5f;

        GameObject col = Instantiate(prefab, new Vector3(posX, posY, posZ), Quaternion.identity);

        Vector3 s = col.transform.localScale;
        s.y *= blocks;
        col.transform.localScale = s;
    }

    void Update()
    {

    }
}