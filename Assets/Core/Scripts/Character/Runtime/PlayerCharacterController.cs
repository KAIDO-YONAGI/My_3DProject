using UnityEngine;
using Mirror;

/// <summary>
/// 角色控制器的 Unity 生命周期入口。
/// 本类只负责组织“采集输入 -> CharacterMotor 位移 -> CharacterAnimator 表现”的顺序，
/// 具体运动算法、动画绑定和骨骼跟随分别由独立类型承担。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCharacterController : MonoBehaviour
{
    [Header("场景引用")]
    // animator 可以位于角色子节点；characterController 必须与本组件位于同一对象。
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    // 显式指定后，移动方向会参考该物体；留空时 CharacterMotor 尝试使用主相机。
    [SerializeField] private Transform inputSpace;

    [Header("移动")]
    // 参数对象只描述手感，实际速度状态保存在 CharacterMotor 中。
    [SerializeField] private CharacterMovementSettings movementSettings = new CharacterMovementSettings();

    [Header("动画")]
    // 这些素材会覆盖 Resources/CharacterLocomotion.controller 中的通用占位片段。
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private DirectionalAnimationSet runDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private DirectionalAnimationSet sprintDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private CharacterJumpAnimationSet jumpAnimationSet = new CharacterJumpAnimationSet();

    [Header("发辫跟随")]
    // 可选修正项，在 Animator 完成骨骼更新后执行。
    [SerializeField] private CharacterBoneFollower ponytailFollower = new CharacterBoneFollower();

    // 输入读取器按需创建 PlayerControls，只有真正读取输入的本地玩家才会占用输入资源。
    private readonly CharacterInputReader input = new();

    // 两个运行时对象不参与序列化，分别管理运动状态和 AnimatorOverrideController。
    private CharacterMotor motor;
    private CharacterAnimator animationDriver;
    private NetworkIdentity networkIdentity;
    // 网络身份在根对象初始化时确定；查不到也缓存，避免单机角色每帧重查。
    private bool networkIdentityInitialized;
    private Vector3 previousNetworkPosition;
    private bool hasPreviousNetworkPosition;

    // 添加组件或在 Inspector 重置时自动补齐最常见引用和默认配置。
    private void Reset()
    {
        CacheReferences();
        EnsureSettings();
    }

    // Awake 只负责组装依赖，不在这里读取输入或执行位移。
    private void Awake()
    {
        EnsureRuntimeState();
    }

    private void Update()
    {
        // 专用服务器只负责转发网络状态，不执行本地输入、重力和动画驱动，
        // 否则无摄像头和本地输入的服务器会把网络玩家持续推落场景。
        if (networkIdentity != null && NetworkServer.active && !NetworkClient.active)
        {
            return;
        }

        // 联机时只允许本地玩家采集输入；没有 NetworkIdentity 的单机角色仍沿用原有控制逻辑。
        if (NetworkClient.isConnected && (networkIdentity == null || !networkIdentity.isLocalPlayer))
        {
            return;
        }

        // 网络模型是运行时挂载的，首次 Update 可能早于实例化后的依赖初始化，避免因此丢失移动能力。
        if (!EnsureRuntimeState())
        {
            return;
        }

        // 保持固定数据流，动画永远读取本帧移动后的碰撞与方向结果。
        CharacterInput input = ReadInput();
        CharacterMotion motion = motor.Tick(input, inputSpace, Time.deltaTime);
        animationDriver?.Apply(motion);
    }

    private void LateUpdate()
    {
        // 远程玩家不读取本地输入；它的动画速度来自 NetworkTransform 更新后的根节点位移。
        // 放在 LateUpdate 是为了先让 Mirror 完成本帧位置插值，再根据最终位置驱动 Animator。
        if (IsRemoteNetworkPlayer())
        {
            ApplyRemoteAnimation(Time.deltaTime);
        }

        // Animator 已完成骨骼计算后，再恢复发辫根节点相对头部的偏移。
        ponytailFollower.Apply();
    }

    private void OnDestroy()
    {
        // CharacterAnimator 内部创建了运行时覆盖器，需要显式释放。
        animationDriver?.Dispose();

        // 生成的包装类在终结时会断言动作表仍然启用，必须显式释放输入资源。
        input.Dispose();
    }

    // 自动引用只填补空字段，不覆盖 Inspector 中已经指定的对象。
    private void CacheReferences()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // 允许网络模型在运行时挂载后补齐 CharacterMotor 与动画驱动，单机 Prefab 仍沿用同一套初始化逻辑。
    private bool EnsureRuntimeState()
    {
        CacheReferences();
        EnsureSettings();
        if (!networkIdentityInitialized)
        {
            networkIdentity = GetComponent<NetworkIdentity>();
            networkIdentityInitialized = true;
        }

        if (characterController == null)
        {
            return false;
        }

        if (motor == null)
        {
            motor = new CharacterMotor(transform, characterController, movementSettings);
        }

        if (animationDriver == null && animator != null)
        {
            animationDriver = new CharacterAnimator(
                animator,
                idleClip,
                runDirectionalSet,
                sprintDirectionalSet,
                jumpAnimationSet);
            animationDriver.Initialize();
            ponytailFollower.Initialize(animator);
        }

        return true;
    }

    /// <summary>
    /// 模型 Prefab 被替换后，重新绑定网络根对象使用的 Animator。
    /// </summary>
    public void RebindAnimator(Animator replacementAnimator, PlayerCharacterController visualConfiguration = null)
    {
        animationDriver?.Dispose();
        animationDriver = null;
        animator = replacementAnimator;

        // 当前本地或远程视觉预制体拥有具体角色的 Idle/Run/Sprint/Jump 素材；
        // 网络根对象只负责移动和网络身份，因此绑定 Animator 时必须把这些配置复制过来。
        if (visualConfiguration != null)
        {
            CopyAnimationConfiguration(visualConfiguration);
        }

        // 清空引用时只保留“无动画驱动”状态，避免 CacheReferences 又找回正在销毁的旧模型。
        if (replacementAnimator != null)
        {
            hasPreviousNetworkPosition = false;
            EnsureRuntimeState();
        }
    }

    /// <summary>
    /// 绑定本地玩家的输入参考空间。
    /// CharactersForLocal 自带相机，网络根对象必须读取这台相机的朝向，
    /// 否则移动会退回 Camera.main 或根节点朝向，出现“转相机但前进方向不变”。
    /// </summary>
    public void RebindInputSpace(Transform replacementInputSpace)
    {
        // 远程角色传入 null，表示它不采集本地输入；本地角色传入本地视觉 Prefab 的相机。
        inputSpace = replacementInputSpace;
    }

    /// <summary>
    /// 从当前视觉预制体的控制器复制动画素材配置。
    /// 不复制 CharacterController、移动参数和输入引用，避免远程视觉模型重新接管移动。
    /// </summary>
    private void CopyAnimationConfiguration(PlayerCharacterController source)
    {
        idleClip = source.idleClip;
        runDirectionalSet = source.runDirectionalSet;
        sprintDirectionalSet = source.sprintDirectionalSet;
        jumpAnimationSet = source.jumpAnimationSet;

        // 发辫跟随配置属于角色视觉预制体，网络根对象只在这里接管它的引用，
        // 不重新创建或改写视觉预制体上的 DynamicBone 组件。
        ponytailFollower.CopyConfigurationFrom(source.ponytailFollower);
    }

    private bool IsRemoteNetworkPlayer()
    {
        return NetworkClient.isConnected
            && networkIdentity != null
            && !networkIdentity.isLocalPlayer;
    }

    /// <summary>
    /// 根据远程根节点本帧的实际位移生成动画快照。
    /// 位置由 NetworkTransformReliable 同步，动画不再依赖远程客户端的本地输入。
    /// </summary>
    private void ApplyRemoteAnimation(float deltaTime)
    {
        if (!EnsureRuntimeState() || animationDriver == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        if (!hasPreviousNetworkPosition || deltaTime <= 0f)
        {
            previousNetworkPosition = currentPosition;
            hasPreviousNetworkPosition = true;
            animationDriver.Apply(new CharacterMotion(true, false, Vector2.zero, 0f));
            return;
        }

        Vector3 frameVelocity = (currentPosition - previousNetworkPosition) / deltaTime;
        previousNetworkPosition = currentPosition;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(frameVelocity, Vector3.up);
        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        float speed = planarVelocity.magnitude;
        float runSpeed = movementSettings.GetTargetSpeed(false);
        float sprintSpeed = movementSettings.GetTargetSpeed(true);
        float sprintThreshold = Mathf.Lerp(runSpeed, sprintSpeed, 0.65f);

        Vector2 localDirection = localVelocity.sqrMagnitude > 0.0001f
            ? new Vector2(localVelocity.x, localVelocity.z).normalized
            : Vector2.zero;
        bool grounded = characterController == null || characterController.isGrounded
            || Mathf.Abs(frameVelocity.y) < 0.05f;
        bool wantsSprint = speed >= sprintThreshold;
        float locomotionWeight = runSpeed > 0.01f ? Mathf.Clamp01(speed / runSpeed) : 0f;

        animationDriver.Apply(new CharacterMotion(
            grounded,
            wantsSprint,
            localDirection,
            locomotionWeight));
    }

    // Unity 旧序列化数据可能把可序列化类保存为 null，这里统一补回默认对象。
    private void EnsureSettings()
    {
        movementSettings ??= new CharacterMovementSettings();
        runDirectionalSet ??= new DirectionalAnimationSet();
        sprintDirectionalSet ??= new DirectionalAnimationSet();
        jumpAnimationSet ??= new CharacterJumpAnimationSet();
        ponytailFollower ??= new CharacterBoneFollower();
    }

    /// <summary>
    /// 从 Input System 读取一帧输入，并在进入运动层前完成归一化。
    /// </summary>
    private CharacterInput ReadInput()
    {
        // 移动长度已在 CharacterInputReader 中截断到单位圆内，斜向不会超速。
        Vector2 move = input.ReadMove();
        return new CharacterInput(move, input.SprintHeld, input.JumpPressed);
    }
}
