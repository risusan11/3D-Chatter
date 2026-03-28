using Photon.Pun;
using UnityEditor;
using UnityEngine;

// MonoBehaviourPunCallbacksを継承して、photonViewプロパティを使えるようにする
public class AvatarController : MonoBehaviourPunCallbacks
{
    private Vector3 velocity; 
    private float moveSpeed = 5.0f; 
    private void Update() {

        if (photonView.IsMine) {

        velocity = Vector3.zero;
        if(Input.GetKey(KeyCode.W))
            velocity.z += 1;
        if(Input.GetKey(KeyCode.A))
            velocity.x -= 1;
        if(Input.GetKey(KeyCode.S))
            velocity.z -= 1;
       if(Input.GetKey(KeyCode.D))
            velocity.x += 1;
        if(Input.GetKey(KeyCode.Space))
            velocity.y += 1;
        if(Input.GetKey(KeyCode.S)){
            velocity = velocity.normalized * moveSpeed/2 * Time.deltaTime;
            }else{
            velocity = velocity.normalized * moveSpeed * Time.deltaTime;
            }
        

        if(velocity.magnitude > 0)
        {

            transform.position += velocity;
        }
            
        }
        if (transform.position.y < -20) {
            transform.position = new Vector3(0, 1, 0);
        }
    }
}