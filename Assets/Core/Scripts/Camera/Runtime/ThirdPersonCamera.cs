using UnityEngine;

/// <summary>
/// 独立处理第三人称镜头的环绕、缩放、角色朝向和障碍物避让。
/// 它只读取跟随目标的位置，不读取 CharacterMotor 的速度，因此角色加减速不会改变镜头距离。
/// 可配置字段按运行期能否生效分组，结论写在 Header 上（运行时可改 / 运行时不可改），Tooltip 只描述字段作用。
/// </summary>
public sealed class ThirdPersonCamera : MonoBehaviour
{
    [Header("目标（运行时可改）")]
    // target 通常是挂载 PlayerCharacterController 的角色根节点。
    [Tooltip("相机跟随的目标根节点，通常是挂载 PlayerCharacterController 的网络根。焦点会立即切到新目标、镜头位置随之跳变；切换玩家由 NetworkCharacterManager 通过 SetTarget 完成。")]
    [SerializeField] private Transform target;

    // 从角色根节点抬高观察焦点，例如对准胸口或头部。
    [Tooltip("观察焦点相对目标原点的偏移，例如抬高到胸口或头部。")]
    [SerializeField] private Vector3 targetOffset;

    [Header("环绕（运行时可改）")]
    // distance 是用户期望距离，碰撞避让只临时缩短实际距离，不改写该值。
    [Tooltip("期望的镜头距离；碰撞避让只临时缩短实际距离，不会改写该值。")]
    [SerializeField] private float distance = 4f;
    [Tooltip("用户缩放的最近距离下限。近处障碍避让优先，实际距离允许低于它。")]
    [SerializeField] private float minDistance = 1.5f;
    [Tooltip("用户缩放的最远距离上限；未按住 Alt 时每帧夹取期望距离，不需要等待滚轮输入。")]
    [SerializeField] private float maxDistance = 10f;
    [Tooltip("鼠标灵敏度，乘在 Input System 鼠标增量换算出的角度上。")]
    [SerializeField] private float sensitivity = 2f;
    [Tooltip("滚轮每格的缩放速度。")]
    [SerializeField] private float scrollSpeed = 2f;
    [Tooltip("最低俯角，负值表示向下看；每帧夹取当前俯角。")]
    [SerializeField] private float minPitch = -60f;
    [Tooltip("最高仰角；每帧夹取当前俯角。")]
    [SerializeField] private float maxPitch = 80f;

    // smoothSpeed 同时控制位置偏移和旋转的追随速度。
    [Tooltip("镜头位置偏移与旋转的追随速度，越小越柔和。")]
    [SerializeField, Min(0.01f)] private float smoothSpeed = 10f;
    [Tooltip("初始化及重新启用时是否锁定并隐藏光标；锁定后按 Escape 释放、点击画面重新捕获。关闭该开关不会释放已经锁定的光标，需要按 Escape 或松一次 Alt。")]
    [SerializeField] private bool lockCursor = true;

    [Header("角色朝向（运行时可改）")]
    // 开启后，存在移动输入时让角色逐渐朝向镜头的水平朝向。
    [Tooltip("开启后，存在移动输入时让角色朝向镜头的水平朝向。")]
    [SerializeField] private bool rotateTarget = true;

    // 指数收敛速率（1/秒）：角差越大单帧转得越多，收尾自然减速，不会在最后一帧把剩余角差一次转完。
    [Tooltip("角色朝向的收敛速率（1/秒）：越大跟得越紧、收尾越快。它决定小角差时的转速，大角差由最大角速度兜住。")]
    [SerializeField, Min(0f)] private float targetRotateSmooth = 15f;

    // 最大角速度（度/秒），与 CharacterMovementSettings.rotationSpeed 对齐，只限制指数收敛在大角差时的峰值。
    [Tooltip("纠正角色朝向的最大角速度（度/秒），与 CharacterMovementSettings.rotationSpeed 对齐；它只限制大角差时的峰值转速，过大时大角差会明显甩头。")]
    [SerializeField, Min(0f)] private float targetRotateSpeed = 360f;

    [Header("避让（运行时可改）")]
    // minHeightY 防止低俯角时镜头落到角色脚下。
    [Tooltip("镜头相对目标原点的期望最低高度，防止低俯角时镜头落到角色脚下；障碍避让优先，受阻时允许低于它。")]
    [SerializeField] private float minHeightY = 2f;

    // collisionRadius 大于零时使用 SphereCast，避免细小障碍穿过镜头中心射线。
    [Tooltip("镜头避让的检测半径，大于 0 时使用 SphereCast；设为 0 关闭避让。")]
    [SerializeField, Min(0f)] private float collisionRadius = 0.2f;
    [Tooltip("参与镜头避让的层，未勾选的层不会阻挡镜头。")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    // 输入统一走 Input System；Alt 按住期间由 freeCursorHeld 表示相机把鼠标交还给系统。
    private readonly CharacterInputReader input = new();
    private readonly CameraOrbitState orbit = new();
    private readonly CameraCursorController cursor = new();
    private readonly CameraCollisionResolver collision = new();
    private bool started;
    private bool initialized;

    private void Start()
    {
        started = true;
        if (target == null)
        {
            Debug.LogWarning($"{name} 的相机没有指定跟随目标。", this);
        }
        Initialize();
    }

    private void OnEnable()
    {
        if (started)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        if (target == null)
        {
            return;
        }
        orbit.Initialize(transform.position, transform.rotation, target.position + targetOffset);
        cursor.Begin(lockCursor);
        initialized = true;
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
        if (target == null)
        {
            if (initialized)
            {
                Suspend();
            }
            return;
        }
        if (!initialized)
        {
            Initialize();
        }

        // 角色完成 Update 位移后再计算镜头，保证焦点使用本帧最终位置。
        cursor.Tick(input, lockCursor, Application.isFocused);
        ReadOrbitInput();

        float deltaTime = Time.deltaTime;
        if (rotateTarget && input.HasMoveInput())
        {
            target.rotation = CameraRotationMath.AlignTarget(
                target.rotation, orbit.Yaw, targetRotateSmooth, targetRotateSpeed, deltaTime);
        }
        UpdateTransform(deltaTime);
    }

    private void OnDisable()
    {
        Suspend();
    }

    private void Suspend()
    {
        cursor.Release();
        input.Dispose();
        initialized = false;
    }

    private void OnDestroy()
    {
        // 生成的包装类在终结时会断言动作表仍然启用，必须显式释放输入资源。
        input.Dispose();
    }

    private void ReadOrbitInput()
    {
        // Alt 按住期间相机完全不响应鼠标：转动和缩放都让给 UI 或编辑器。
        Vector2 look = cursor.IsLocked && !cursor.FreeCursorHeld && !cursor.SkipLookThisFrame
            ? input.ReadLook()
            : Vector2.zero;
        orbit.ApplyLook(look, sensitivity, minPitch, maxPitch);

        if (!cursor.FreeCursorHeld)
        {
            distance = Mathf.Clamp(
                distance - input.ReadZoom() * scrollSpeed,
                minDistance,
                maxDistance);
        }
    }

    private void UpdateTransform(float deltaTime)
    {
        Vector3 focus = target.position + targetOffset;
        Vector3 candidate = orbit.Advance(distance, minHeightY - targetOffset.y, smoothSpeed, deltaTime);
        Vector3 resolved = collision.Resolve(focus, candidate, target, collisionRadius, collisionLayers);
        orbit.CommitOffset(candidate, resolved);
        transform.SetPositionAndRotation(focus + resolved, orbit.WorldRotation);
    }
}
