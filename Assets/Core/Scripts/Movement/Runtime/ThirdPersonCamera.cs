using UnityEngine;

/// <summary>
/// 独立处理第三人称镜头的环绕、缩放、角色朝向和障碍物避让。
/// 它只读取跟随目标的位置，不读取 CharacterMotor 的速度，因此角色加减速不会改变镜头距离。
/// </summary>
public sealed class ThirdPersonCamera : MonoBehaviour
{
    // 使用固定数组接收无分配球形检测结果，容量足以覆盖镜头路径上的常见碰撞体。
    private const int CollisionHitCapacity = 16;

    [Header("目标")]
    // target 通常是挂载 PlayerCharacterController 的角色根节点。
    [SerializeField] private Transform target;

    // 从角色根节点抬高观察焦点，例如对准胸口或头部。
    [SerializeField] private Vector3 targetOffset;

    [Header("环绕")]
    // distance 是用户期望距离，碰撞避让只临时缩短实际距离，不改写该值。
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float scrollSpeed = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 80f;

    // smoothSpeed 同时控制位置偏移和旋转的追随速度。
    [SerializeField, Min(0.01f)] private float smoothSpeed = 10f;
    [SerializeField] private bool lockCursor = true;

    [Header("角色朝向")]
    // 开启后，存在移动输入时让角色逐渐朝向镜头的水平朝向。
    [SerializeField] private bool rotateTarget = true;
    [SerializeField, Min(0f)] private float targetRotateSmooth = 15f;

    [Header("避让")]
    // minHeightY 防止低俯角时镜头落到角色脚下。
    [SerializeField] private float minHeightY = 2f;

    // collisionRadius 大于零时使用 SphereCast，避免细小障碍穿过镜头中心射线。
    [SerializeField, Min(0f)] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private float yaw;
    private float pitch;
    private bool cursorLocked;

    // 输入统一走 Input System；Alt 按住期间由 freeCursorHeld 表示相机把鼠标交还给系统。
    private readonly CharacterInputReader input = new();
    private bool freeCursorHeld;
    private bool lockStateBeforeFree;
    private bool skipLookThisFrame;

    // 只平滑“焦点到镜头”的相对偏移，不平滑角色世界坐标。
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

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = NormalizePitch(angles.x);

        // 用场景中的初始镜头位置建立偏移，避免启用脚本时突然跳到预设距离。
        smoothedOffset = transform.position - (target.position + targetOffset);
        SetCursorLock(lockCursor);
    }

    /// <summary>
    /// 网络模型运行时替换跟随目标，但保留摄像头在本地模型 Prefab 内的层级。
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
        {
            return;
        }

        target = newTarget;
    }

    private void LateUpdate()
    {
        // 角色完成 Update 位移后再计算镜头，保证焦点使用本帧最终位置。
        HandleCursor();
        ReadOrbitInput();

        if (rotateTarget && input.HasMoveInput())
        {
            // 只同步 yaw，角色不会继承镜头俯仰。
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

        // 停用或退出播放时清空 Alt 状态，重新启用后按 lockCursor 重新判断。
        freeCursorHeld = false;
        lockStateBeforeFree = false;
        skipLookThisFrame = false;
    }

    private void OnDestroy()
    {
        // 生成的包装类在终结时会断言动作表仍然启用，必须显式释放输入资源。
        input.Dispose();
    }

    private void HandleCursor()
    {
        // Alt 是“临时交出鼠标”的修饰键：按住时释放光标并暂停注视，松开后按进入前的意图恢复。
        bool freeHeld = input.FreeCursorHeld;
        if (freeHeld != freeCursorHeld)
        {
            if (freeHeld)
            {
                lockStateBeforeFree = cursorLocked;
                SetCursorLock(false);
                skipLookThisFrame = true;
            }
            else if (lockStateBeforeFree && lockCursor && Application.isFocused)
            {
                // 焦点不在本程序时不抢回鼠标，避免把光标从编辑器或其它窗口拉走。
                SetCursorLock(true);
                skipLookThisFrame = true;
            }

            freeCursorHeld = freeHeld;
        }

        // 按住 Alt 期间不处理 Escape 和左键：此时的左键用于操作 UI，不能重新锁定光标。
        if (freeCursorHeld)
        {
            return;
        }

        // Escape 释放鼠标；需要锁定时点击画面可重新捕获。
        if (input.ReleaseCursorPressed)
        {
            SetCursorLock(false);
        }
        else if (lockCursor && input.LockCursorPressed)
        {
            SetCursorLock(true);
        }
    }

    private void ReadOrbitInput()
    {
        // 锁定状态刚发生变化的这一帧鼠标增量不可靠（光标回中），跳过它避免镜头跳变。
        bool skipLook = skipLookThisFrame;
        skipLookThisFrame = false;

        // Alt 按住期间相机完全不响应鼠标：转动和缩放都让给 UI 或编辑器。
        if (cursorLocked && !freeCursorHeld && !skipLook)
        {
            // 鼠标只修改轨道角度，不直接累加世界位置。
            Vector2 look = input.ReadLook();
            yaw += look.x * sensitivity;
            pitch -= look.y * sensitivity;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (!freeCursorHeld)
        {
            distance = Mathf.Clamp(
                distance - input.ReadZoom() * scrollSpeed,
                minDistance,
                maxDistance);
        }
    }

    /// <summary>
    /// 从目标焦点、轨道旋转和碰撞距离计算镜头最终世界变换。
    /// </summary>
    private void UpdateTransform()
    {
        Vector3 focus = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backwards = -(rotation * Vector3.forward);

        // 先解决障碍距离，再应用最低高度，得到当前帧期望位置。
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

    /// <summary>
    /// 检测焦点到期望镜头位置之间的障碍物，并返回不会穿模的最近距离。
    /// </summary>
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

            // 角色自身碰撞体不应被当成镜头障碍物。
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

    /// <summary>
    /// 判断命中的碰撞体是否属于当前跟随角色。
    /// </summary>
    private bool IsTargetCollider(Transform hitTransform)
    {
        // 射线从角色内部发出，必须忽略角色自己的碰撞体。
        return hitTransform == target || hitTransform.IsChildOf(target);
    }

    // 统一更新内部状态和 Unity 光标状态，避免两者不同步。
    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // Unity 欧拉角使用 0-360 表示，转换后才能正确应用负俯角限制。
    private static float NormalizePitch(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
