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
    
    // Added from AvatarController to store the current frame's movement vector
    private Vector3 velocity; 

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
        // 1. Reset input values each frame
        float x = 0;
        float y = 0;
        float z = 0;

        // 2. Gather key inputs (AvatarController style)
        if (Input.GetKey(KeyCode.W)) z += 1;
        if (Input.GetKey(KeyCode.S)) z -= 1;
        if (Input.GetKey(KeyCode.A)) x -= 1;
        if (Input.GetKey(KeyCode.D)) x += 1;
        
        // Vertical movement mapping
        if (Input.GetKey(KeyCode.E)) y += 1;
        if (Input.GetKey(KeyCode.Q)) y -= 1;

        // 3. Normalize to prevent faster diagonal movement
        Vector3 inputDir = new Vector3(x, y, z).normalized;

        // 4. Calculate current speed using the multiplier
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? (moveSpeed * fastMoveMultiplier) : moveSpeed;

        // 5. Calculate velocity relative to the camera's facing direction
        velocity = (transform.forward * inputDir.z) + (transform.right * inputDir.x) + (transform.up * inputDir.y);

        // 6. Apply movement if there is any input
        if (velocity.magnitude > 0)
        {
            transform.position += velocity * currentSpeed * Time.deltaTime;
        }
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
/*using UnityEngine;

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
}*/