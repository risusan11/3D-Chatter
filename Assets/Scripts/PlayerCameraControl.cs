using Photon.Pun;
using UnityEngine;

public class PlayerCameraControl : MonoBehaviourPun
{
    void Start()
    {
        if (!photonView.IsMine)
        {
            
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cam.enabled = false;

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
            
        }
    }
}