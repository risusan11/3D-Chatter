using UnityEngine;

public class SimpleWASDCameraController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float fastMoveMultiplier = 3f;

    [Header("Look")]
    [SerializeField] private bool enableMouseLook = true;
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private KeyCode lookButton = KeyCode.Mouse1;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    private void Update()
    {
        MoveCamera();

        if (enableMouseLook)
        {
            LookCamera();
        }
    }

    private void MoveCamera()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed *= fastMoveMultiplier;
        }

        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;

        if (Input.GetKey(KeyCode.E)) move += transform.up;
        if (Input.GetKey(KeyCode.Q)) move -= transform.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }

    private void LookCamera()
    {
        if (!Input.GetKey(lookButton)) return;

        yaw += Input.GetAxis("Mouse X") * lookSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}