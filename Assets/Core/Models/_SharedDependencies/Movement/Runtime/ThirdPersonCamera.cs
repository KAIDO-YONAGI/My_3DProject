using UnityEngine;

public sealed class ThirdPersonCamera : MonoBehaviour
{
    private const int CollisionHitCapacity = 16;

    [Header("目标")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset;

    [Header("环绕")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float scrollSpeed = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField, Min(0.01f)] private float smoothSpeed = 10f;
    [SerializeField] private bool lockCursor = true;

    [Header("角色朝向")]
    [SerializeField] private bool rotateTarget = true;
    [SerializeField, Min(0f)] private float targetRotateSmooth = 15f;

    [Header("避让")]
    [SerializeField] private float minHeightY = 2f;
    [SerializeField, Min(0f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float yaw;
    private float pitch;
    private bool cursorLocked;
    private Vector3 smoothedOffset;
    private Vector3 offsetVelocity;
    private readonly RaycastHit[] collisionHits = new RaycastHit[CollisionHitCapacity];

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning($"{name} 的相机没有指定跟随目标。", this);
            enabled = false;
            return;
        }

        if (transform.IsChildOf(target))
        {
            transform.SetParent(null, true);
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = NormalizePitch(angles.x);
        smoothedOffset = transform.position - (target.position + targetOffset);
        SetCursorLock(lockCursor);
    }

    private void LateUpdate()
    {
        HandleCursor();
        ReadOrbitInput();

        if (rotateTarget && HasMoveInput())
        {
            Quaternion targetRotation = Quaternion.Euler(0f, yaw, 0f);
            target.rotation = Quaternion.Slerp(
                target.rotation,
                targetRotation,
                Time.deltaTime * targetRotateSmooth);
        }

        UpdateTransform();
    }

    private void OnDisable()
    {
        SetCursorLock(false);
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(false);
        }
        else if (lockCursor && Input.GetMouseButtonDown(0))
        {
            SetCursorLock(true);
        }
    }

    private void ReadOrbitInput()
    {
        if (cursorLocked)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(
            distance - Input.GetAxis("Mouse ScrollWheel") * scrollSpeed,
            minDistance,
            maxDistance);
    }

    private void UpdateTransform()
    {
        Vector3 focus = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backwards = -(rotation * Vector3.forward);
        float resolvedDistance = ResolveDistance(focus, backwards);
        Vector3 desiredPosition = focus + backwards * resolvedDistance;
        desiredPosition.y = Mathf.Max(desiredPosition.y, target.position.y + minHeightY);

        // 只平滑相对偏移，避免角色速度改变镜头与角色之间的距离。
        Vector3 desiredOffset = desiredPosition - focus;
        smoothedOffset = Vector3.SmoothDamp(
            smoothedOffset,
            desiredOffset,
            ref offsetVelocity,
            1f / smoothSpeed);
        transform.position = focus + smoothedOffset;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            rotation,
            Time.deltaTime * smoothSpeed);
    }

    private float ResolveDistance(Vector3 focus, Vector3 backwards)
    {
        if (collisionRadius <= 0f)
        {
            return distance;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            focus,
            collisionRadius,
            backwards,
            collisionHits,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        float resolvedDistance = distance;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = collisionHits[index];
            if (hit.collider == null || IsTargetCollider(hit.collider.transform))
            {
                continue;
            }

            resolvedDistance = Mathf.Min(
                resolvedDistance,
                hit.distance - collisionRadius);
        }

        return Mathf.Max(minDistance, resolvedDistance);
    }

    private bool IsTargetCollider(Transform hitTransform)
    {
        // 射线从角色内部发出，必须忽略角色自己的碰撞体。
        return hitTransform == target || hitTransform.IsChildOf(target);
    }

    private static bool HasMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        return horizontal * horizontal + vertical * vertical > 0.0001f;
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private static float NormalizePitch(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
