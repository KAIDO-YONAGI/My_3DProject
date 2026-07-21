using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCharacterController : MonoBehaviour
{
    [Header("场景引用")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform inputSpace;

    [Header("移动")]
    [SerializeField] private CharacterMovementSettings movementSettings = new CharacterMovementSettings();

    [Header("动画")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private DirectionalAnimationSet runDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private DirectionalAnimationSet sprintDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private CharacterJumpAnimationSet jumpAnimationSet = new CharacterJumpAnimationSet();

    [Header("发辫跟随")]
    [SerializeField] private CharacterBoneFollower ponytailFollower = new CharacterBoneFollower();

    private CharacterMotor motor;
    private CharacterAnimator animationDriver;

    private void Reset()
    {
        CacheReferences();
        EnsureSettings();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureSettings();

        if (characterController == null)
        {
            Debug.LogError($"{name} 缺少 CharacterController，角色控制已停用。", this);
            enabled = false;
            return;
        }

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

        ponytailFollower.Initialize(animator);
    }

    private void Update()
    {
        CharacterInput input = ReadInput();
        CharacterMotion motion = motor.Tick(input, inputSpace, Time.deltaTime);
        animationDriver?.Apply(motion);
    }

    private void LateUpdate()
    {
        ponytailFollower.Apply();
    }

    private void OnDestroy()
    {
        animationDriver?.Dispose();
    }

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

    private void EnsureSettings()
    {
        movementSettings ??= new CharacterMovementSettings();
        runDirectionalSet ??= new DirectionalAnimationSet();
        sprintDirectionalSet ??= new DirectionalAnimationSet();
        jumpAnimationSet ??= new CharacterJumpAnimationSet();
        ponytailFollower ??= new CharacterBoneFollower();
    }

    private static CharacterInput ReadInput()
    {
        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        move = Vector2.ClampMagnitude(move, 1f);

        bool sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        return new CharacterInput(move, sprintHeld, jumpPressed);
    }
}
