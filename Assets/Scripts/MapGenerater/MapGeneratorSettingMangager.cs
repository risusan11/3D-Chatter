using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapGeneratorSettingMangager : MonoBehaviour
{
    [SerializeField] private InputField mapInputFieldText;
    [SerializeField] private Button SavingButton;

    public static string mapData;

    // Start is called before the first frame update
    void Start()
    {
        SavingButton.onClick.AddListener(SaveMapData);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SaveMapData()
    {
        mapData = mapInputFieldText.text;
        Debug.Log("Map data saved: " + mapData);
    }
}