using Photon.Pun;
using UnityEngine;

public class PlayerCameraControl : MonoBehaviourPun
{
    public GameObject cam;
    
    // 🌟 変更: static にして「共有の設定値」にする（XとYを1つにまとめました）
    public static float sharedSensitivity = 3f;

    bool cursorLock = true;
    float xRotation = 0f;

    void Start()
    {
        if (!photonView.IsMine)
        {
            Camera cameraComponent = cam.GetComponent<Camera>();
            if (cameraComponent != null) cameraComponent.enabled = false;

            AudioListener listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            
            return;
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        
        // 🌟 追加: チャット中や設定画面を開いている時はカメラを動かさない
        if (RealisingMessageController.isChatting || RealisingMessageController.isCanvas2Active) return;

        // 🌟 変更: static な sharedSensitivity を使う
        float mouseX = Input.GetAxis("Mouse X") * sharedSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sharedSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); 
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);

        UpdateCursorLock();
    }

    public void UpdateCursorLock()
    {
        // 🌟 追加: 設定画面を開いている時は、左クリックによるロック処理を無視する
        if (RealisingMessageController.isCanvas2Active) return;

        if(Input.GetKeyDown(KeyCode.Escape))
        {
            cursorLock = false;
        }
        else if(Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return; 
            }
            cursorLock = true;
        }

        if (cursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}