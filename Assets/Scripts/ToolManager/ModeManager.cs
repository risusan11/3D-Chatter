using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class ModeManager : MonoBehaviour
{
    // Start is called before the first frame update
public enum AppMode { Free, Draw3D, Erase, TabPlace, ObjPlace, ObjDelete, ObjMove }
    public static ModeManager Instance { get; private set; }
    public AppMode Current { get; private set; } = AppMode.Free;
    void Awake()
    {

        Instance = this;
        
    }

    public void RequestMode(AppMode mode)
    {
        if (Current == mode)
        {
            Current = AppMode.Free;
        }
        else
        {
            Current = mode;
        }
    }
    public void SetMode(AppMode mode)
{
    Current = mode;
    Debug.Log("モード変更: " + Current);
}
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))   RequestMode(AppMode.Draw3D);
        if (Input.GetKeyDown(KeyCode.E))   RequestMode(AppMode.Erase);
        if (Input.GetKeyDown(KeyCode.Tab)) RequestMode(AppMode.TabPlace);
        if (Input.GetKeyDown(KeyCode.H))   RequestMode(AppMode.ObjDelete);
        if (Input.GetKeyDown(KeyCode.J))   RequestMode(AppMode.ObjMove);
    }
}
