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

        animator.SetBool("f",false);
        animator.SetBool("b",false);
        animator.SetBool("r",false);
        animator.SetBool("l",false);
        animator.SetBool("j",false);
        if (Input.GetKey(KeyCode.W))
        {
            animator.SetBool("f",true);
        }
        if (Input.GetKey(KeyCode.S))
        {
            animator.SetBool("b",true);
        }
        if (Input.GetKey(KeyCode.A))
        {
            animator.SetBool("l",true);
        }
        if (Input.GetKey(KeyCode.D))
        {
            animator.SetBool("r",true);
        }
        if(Input.GetKey(KeyCode.Space))
        {
            animator.SetBool("j",true);
        }
    }
}