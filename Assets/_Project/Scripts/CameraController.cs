using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Camera")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;

    [Header("Look")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    private float yaw;
    private float pitch;

    private Vector2 lookInput;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        yaw += lookInput.x * sensitivity;
        pitch -= lookInput.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 플레이어 뒤쪽에 카메라 배치
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = target.position + Vector3.up * height + offset;

        // 플레이어 쪽을 바라봄
        transform.LookAt(target.position + Vector3.up * height);
    }

    public void SetLookInput(Vector2 input)
    {
        lookInput = input;
    }

    public Vector3 GetCameraForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public Vector3 GetCameraRight()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        return right.normalized;
    }
}