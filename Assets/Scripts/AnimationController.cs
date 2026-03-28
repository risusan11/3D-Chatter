using Photon.Pun;
using UnityEngine;

public class AnimationController : MonoBehaviourPunCallbacks
{
    private Animator animator;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            animator.SetTrigger("E");
        }
    }
}