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

    // 两个运行时对象不参与序列化，分别管理运动状态和 AnimatorOverrideController。
    private CharacterMotor motor;
    private CharacterAnimator animationDriver;
    private NetworkIdentity networkIdentity;

    // 添加组件或在 Inspector 重置时自动补齐最常见引用和默认配置。
    private void Reset()
    {
        CacheReferences();
        EnsureSettings();
    }

    // Awake 只负责组装依赖，不在这里读取输入或执行位移。
    private void Awake()
    {
        CacheReferences();
        EnsureSettings();
        networkIdentity = GetComponent<NetworkIdentity>();

        if (characterController == null)
        {
            Debug.LogError($"{name} 缺少 CharacterController，角色控制已停用。", this);
            enabled = false;
            return;
        }

        // CharacterMotor 始终创建，因此即使没有 Animator，角色仍能正常移动。
        motor = new CharacterMotor(transform, characterController, movementSettings);

        if (animator != null)
        {
            animationDriver = new CharacterAnimator(
                animator,
                idleClip,
                runDirectionalSet,
                sprintDirectionalSet,
                jumpAnimationSet);
            animationDriver.Initialize();
        }
        else
        {
            Debug.LogWarning($"{name} 缺少 Animator，角色仍可移动但不会播放动画。", this);
        }

        // 骨骼跟随依赖 Animator 的人形骨骼信息，必须在引用确定后初始化。
        ponytailFollower.Initialize(animator);
    }

    private void Update()
    {
        // 联机时只允许本地玩家采集输入；没有 NetworkIdentity 的单机角色仍沿用原有控制逻辑。
        if (NetworkClient.active && (networkIdentity == null || !networkIdentity.isLocalPlayer))
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
        // Animator 已完成骨骼计算后，再恢复发辫根节点相对头部的偏移。
        ponytailFollower.Apply();
    }

    private void OnDestroy()
    {
        // CharacterAnimator 内部创建了运行时覆盖器，需要显式释放。
        animationDriver?.Dispose();
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
    /// 从旧版 Unity Input Manager 读取一帧输入，并在进入运动层前完成归一化。
    /// </summary>
    private static CharacterInput ReadInput()
    {
        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // GetAxisRaw 保证键盘按下和松开没有输入平滑延迟，ClampMagnitude 防止斜向超速。
        move = Vector2.ClampMagnitude(move, 1f);

        bool sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        return new CharacterInput(move, sprintHeld, jumpPressed);
    }
}
