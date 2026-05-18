using UnityEngine;

public class ThirdPersonMouseCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float scrollSpeed = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool rotateTarget = true;
    [SerializeField] private float targetRotateSmooth = 15f;

    [Header("Height")]
    [SerializeField] private float minHeightY = 2f;

    private float yaw;
    private float pitch;
    private bool cursorLocked;
    private Vector3 positionVelocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"[ThirdPersonMouseCamera] No target assigned on {name}.", this);
            enabled = false;
            return;
        }

        if (transform.IsChildOf(target))
        {
            transform.SetParent(null, true);
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
        SetCursorLock(lockCursor);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        HandleCursorToggle();
        UpdateOrbitInput();
        UpdateDistance();

        if (rotateTarget && HasMoveInput())
        {
            RotateTargetTowardsYaw();
        }

        UpdateCameraTransform();
    }

    private void OnDisable()
    {
        SetCursorLock(false);
    }

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(false);
        }
        else if (Input.GetMouseButtonDown(0) && lockCursor)
        {
            SetCursorLock(true);
        }
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void UpdateOrbitInput()
    {
        if (cursorLocked)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateDistance()
    {
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * scrollSpeed, minDistance, maxDistance);
    }

    private bool HasMoveInput()
    {
        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return moveInput.sqrMagnitude > 0.0001f;
    }

    private void RotateTargetTowardsYaw()
    {
        Quaternion targetYaw = Quaternion.Euler(0f, yaw, 0f);
        target.rotation = Quaternion.Slerp(target.rotation, targetYaw, Time.deltaTime * targetRotateSmooth);
    }

    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = target.position - (rotation * Vector3.forward * distance);
        desiredPosition = CameraFloorAvoidance.ResolvePosition(desiredPosition, target.position, minHeightY, minDistance);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, 1f / smoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * smoothSpeed);
    }
}
