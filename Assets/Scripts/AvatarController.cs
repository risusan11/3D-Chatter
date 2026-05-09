using Photon.Pun;
using UnityEngine;

public class AvatarController : MonoBehaviourPunCallbacks
{
    private Vector3 velocity; 
    private float moveSpeed = 5.0f; 

    private void Update() 
    {
        if (!photonView.IsMine) return;
        if (RealisingMessageController.isChatting) return;

        // 一旦入力値をリセット
        
        float x = 0;
        float y = 0;
        float z = 0;
        moveSpeed = 5.0f; // デフォルトの移動速度

        // キー入力の取得
        if (Input.GetKey(KeyCode.W)) z += 1;
        if (Input.GetKey(KeyCode.S)) z -= 1;
        if (Input.GetKey(KeyCode.A)) x -= 1;
        if (Input.GetKey(KeyCode.D)) x += 1;
        if (Input.GetKey(KeyCode.Space)) y += 1;
        if (Input.GetKey(KeyCode.LeftShift)) y += -1;
        if (Input.GetKey(KeyCode.V)) moveSpeed += 1.5f;

        Vector3 inputDir = new Vector3(x, y, z).normalized;

        float currentSpeed = Input.GetKey(KeyCode.S) ? (moveSpeed / 2f) : moveSpeed;

        velocity = (transform.forward * inputDir.z) + (transform.right * inputDir.x) + (transform.up * inputDir.y);

        if (velocity.magnitude > 0)
        {
            transform.position += velocity * currentSpeed * Time.deltaTime;
        }
            
        if (transform.position.y < -20) {
            transform.position = new Vector3(0, 1, 0);
        }
    }
}