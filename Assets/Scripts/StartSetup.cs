using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class StartSetup : MonoBehaviourPunCallbacks
{
    [SerializeField] private InputField InnerText;
    [SerializeField] private Button StartButton;
    // Start is called before the first frame update


    private void Start() {
        StartButton.onClick.AddListener(OnClickStart);
        
        

    }
    void OnClickStart()
    {   if (InnerText.text == "")
        {
            Debug.Log("You've got to Enter available name for your Nickname.");
        }
        else
        {
        PhotonNetwork.NickName = InnerText.text;
        Debug.Log(PhotonNetwork.NickName);
        
        SceneManager.LoadScene("Loading");
        }

        
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
