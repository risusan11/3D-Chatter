using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class MapGenerator : MonoBehaviour
{
    
    // Start is called before the first frame update
    [SerializeField] private GameObject LandprefabA;
    [SerializeField] private GameObject LandprefabB;

    private float blockWidth = 10.0f; // x軸方向の幅 10m
    private float blockLength = 10.0f; // z軸方向の幅 10m
    private float stepHeight = 1.0f;   // 厚み 1m
    private string inputText;
    private string testMapData;
    void Start()
    {
        testMapData = MapGeneratorSettingMangager.mapData;
        GenerateMap(testMapData);
    }


public void GenerateMap(string mapString)
    {
        string[] rows = mapString.Split('\n');// z座標の行ごとに分割してる処理

        for (int z = 0; z < rows.Length; z++)// z座標の行ごとにループしてる処理
        {
            string[] columns = rows[z].Split(',');// x座標の列ごとに分割してる処理

            for (int x = 0; x < columns.Length; x++)// x座標の列ごとにループしてる処理
            {
                string tileData = columns[x].Trim(); 
                if (string.IsNullOrEmpty(tileData) || tileData.Length < 2) continue;

                char type = tileData[0];//一つ目の文字をタイプとして取得してる処理
                
                if (int.TryParse(tileData.Substring(1), out int heightLevel))
                {
                    float posX = x * blockWidth;// x座標の位置を計算してる処理
                    float posZ = -z * blockLength;// z座標の位置を計算してる処理
                    
                   
                    float posY = (heightLevel * stepHeight) + (stepHeight / 2.0f);

                    Vector3 spawnPosition = new Vector3(posX, posY, posZ);

                    if (type == 'a')
                    {
                        Instantiate(LandprefabA, spawnPosition, Quaternion.identity);
                    }
                    else if (type == 'b')
                    {
                        Instantiate(LandprefabB, spawnPosition, Quaternion.identity);
                    }
                }
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
