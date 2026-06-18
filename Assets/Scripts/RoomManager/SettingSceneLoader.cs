using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class SettingSeanceLoader : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Button SettingSceneButton; // 設定シーンのプレハブ
    [SerializeField] private Text ButtonText; // ロビーシーンのプレハブ
    [SerializeField] private InputField mapInputField; // ロビーシーンのプレハブ
    [SerializeField] private FadePopup CodePopup;

    void Start()
    {
        mapInputField.gameObject.SetActive(false);
        SettingSceneButton.onClick.AddListener(() =>
        {
            switch (LobbyManager.roomSceneName)
            {
                case "Main":
                    mapInputField.gameObject.SetActive(false);
                    LobbyManager.roomSceneName = "MapA";
                    ButtonText.text = "MapA will be loaded";
                    break;
                case "MapA":
                    mapInputField.gameObject.SetActive(true);
                    LobbyManager.roomSceneName = "OriginalScene";
                    ButtonText.text = "OriginalScene will be loaded";
                    if (CodePopup != null)
                    {
                        CodePopup.Show("Type The Code To Make Your Own Map!!");
                    }
                    return;
                    
                case "OriginalScene":
                    mapInputField.gameObject.SetActive(false);
                    LobbyManager.roomSceneName = "Main";
                    ButtonText.text = "Main will be loaded";
                    break;
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
    }
}
/*using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class SettingSceneLoader : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Button SettingSceneButton; // 設定シーンのプレハブ
    [SerializeField] private Text ButtonText; // ロビーシーンのプレハブ
    void Start()
    {
        SettingSceneButton.onClick.AddListener(() =>
        {
            switch (LobbyManager.roomSceneName)
            {
                case "Main":
                    LobbyManager.roomSceneName = "MapA";
                    ButtonText.text = "MapA will be loaded";
                    break;
                case "MapA":
                    LobbyManager.roomSceneName = "Original";
                    ButtonText.text = "Original will be loaded";
                    break;
                case "Original":
                    LobbyManager.roomSceneName = "Main";
                    ButtonText.text = "Main will be loaded";
                    break;
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
*/