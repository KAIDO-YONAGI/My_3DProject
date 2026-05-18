using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class SimpleActionCharacterController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform inputSpace;

    [Header("Movement")]
    [SerializeField] private SimpleCharacterMovementSettings movementSettings = new SimpleCharacterMovementSettings();

    [Header("Animations")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private DirectionalAnimationSet runDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private DirectionalAnimationSet sprintDirectionalSet = new DirectionalAnimationSet();
    [SerializeField] private SimpleCharacterJumpAnimationSet jumpAnimationSet = new SimpleCharacterJumpAnimationSet();
    [SerializeField] private SimpleCharacterAnimationSettings animationSettings = new SimpleCharacterAnimationSettings();

    [Header("Ponytail Fix")]
    [SerializeField] private CharacterBoneFollower ponytailFollower = new CharacterBoneFollower();

    private SimpleCharacterMotor motor;
    private SimpleCharacterStateMachine stateMachine;
    private SimpleCharacterAnimationRig animationRig;
    private bool runtimeInitialized;

    private void Reset()
    {
        CacheComponentReferences();
        EnsureSerializedContainers();
    }

    private void Awake()
    {
        if (!InitializeRuntime())
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (!runtimeInitialized)
        {
            return;
        }

        CharacterInputFrame input = ReadInput();
        SimpleCharacterMotorFrame frame = motor.Tick(input, inputSpace, Time.deltaTime);
        SimpleCharacterAnimationState state = stateMachine.Tick(
            frame,
            animationRig.GetJumpStartExitTime(),
            animationRig.GetLandHoldTime(frame.MoveMagnitude01 > 0.1f),
            Time.deltaTime);

        animationRig.Apply(frame, state);
        animationRig.Tick(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!runtimeInitialized)
        {
            return;
        }

        ponytailFollower.Apply();
    }

    private void OnDestroy()
    {
        if (animationRig != null)
        {
            animationRig.Dispose();
            animationRig = null;
        }
    }

    [ContextMenu("Auto Configure Character")]
    public void AutoConfigureCharacter()
    {
#if UNITY_EDITOR
        EditorAutoConfigure(overwriteExisting: true);
#endif
    }

#if UNITY_EDITOR
    public void EditorAutoConfigure(bool overwriteExisting)
    {
        CacheComponentReferences();
        EnsureSerializedContainers();
        EnsureCharacterController();
        FitCharacterControllerToRenderers();

        SimpleCharacterAutoConfigCache cache = SimpleCharacterAutoConfigCache.LoadOrCreateAsset();
        if (!cache.HasAnimationBindings && EditorHasAnyConfigurationBindings())
        {
            EditorWriteCache(cache);
        }

        bool changed = false;
        if (cache.HasAnimationBindings)
        {
            changed |= EditorApplyCache(cache, overwriteExisting);
        }
        else
        {
            changed |= movementSettings.ApplyRecommendedMinimums();
        }

        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(cache);
        if (changed)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
    }

    public bool EditorHasAnyConfigurationBindings()
    {
        EnsureSerializedContainers();
        return idleClip != null
            || runDirectionalSet.HasValidSamples
            || sprintDirectionalSet.HasValidSamples
            || jumpAnimationSet.HasAnyClip;
    }

    public bool EditorWriteCache(SimpleCharacterAutoConfigCache cache)
    {
        if (cache == null || !EditorHasAnyConfigurationBindings())
        {
            return false;
        }

        return cache.EditorOverwriteFrom(
            movementSettings,
            idleClip,
            runDirectionalSet,
            sprintDirectionalSet,
            jumpAnimationSet,
            animationSettings);
    }
#endif

    private bool InitializeRuntime()
    {
        if (runtimeInitialized)
        {
            return true;
        }

        CacheComponentReferences();
        EnsureSerializedContainers();
        if (animator == null)
        {
            Debug.LogWarning($"[{nameof(SimpleActionCharacterController)}] Missing Animator on {name}. Runtime setup was skipped.", this);
            return false;
        }

        if (characterController == null)
        {
            Debug.LogWarning($"[{nameof(SimpleActionCharacterController)}] Missing CharacterController on {name}. Run auto configure once in the editor before entering play mode.", this);
            return false;
        }

        ponytailFollower.AutoAssign(animator, transform);
        ponytailFollower.InitializeRuntimeBinding();

        motor = new SimpleCharacterMotor(transform, characterController, movementSettings);
        stateMachine = new SimpleCharacterStateMachine();
        animationRig = new SimpleCharacterAnimationRig(
            animator,
            idleClip,
            runDirectionalSet,
            sprintDirectionalSet,
            jumpAnimationSet,
            animationSettings);
        animationRig.SetImmediateState(SimpleCharacterAnimationState.Grounded);
        animationRig.EvaluateImmediate();

        if (!animationRig.HasRequiredBindings)
        {
            Debug.LogWarning($"[{nameof(SimpleActionCharacterController)}] Missing one or more animation bindings on {name}. Run auto configure from the component context menu or the Tools menu.", this);
        }

        runtimeInitialized = true;
        return true;
    }

    private void CacheComponentReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private void EnsureSerializedContainers()
    {
        if (movementSettings == null)
        {
            movementSettings = new SimpleCharacterMovementSettings();
        }

        if (runDirectionalSet == null)
        {
            runDirectionalSet = new DirectionalAnimationSet();
        }

        if (sprintDirectionalSet == null)
        {
            sprintDirectionalSet = new DirectionalAnimationSet();
        }

        if (jumpAnimationSet == null)
        {
            jumpAnimationSet = new SimpleCharacterJumpAnimationSet();
        }

        if (animationSettings == null)
        {
            animationSettings = new SimpleCharacterAnimationSettings();
        }

        if (ponytailFollower == null)
        {
            ponytailFollower = new CharacterBoneFollower();
        }
    }

    private void EnsureCharacterController()
    {
        if (characterController != null)
        {
            return;
        }

        characterController = gameObject.AddComponent<CharacterController>();
    }

    private void FitCharacterControllerToRenderers()
    {
        if (characterController == null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        Vector3 scale = transform.lossyScale;
        float scaleY = Mathf.Max(0.0001f, scale.y);
        float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(scale.x, scale.z));

        float height = bounds.size.y / scaleY;
        float radius = Mathf.Max(0.2f, Mathf.Min(bounds.size.x, bounds.size.z) / scaleXZ * 0.25f);
        Vector3 center = transform.InverseTransformPoint(bounds.center);
        center.y = height * 0.5f;

        characterController.height = Mathf.Max(height, radius * 2f + 0.1f);
        characterController.radius = radius;
        characterController.center = center;
        characterController.stepOffset = Mathf.Min(characterController.height * 0.2f, 0.35f);
        characterController.skinWidth = Mathf.Clamp(radius * 0.1f, 0.01f, 0.08f);
        characterController.minMoveDistance = 0f;
    }

    private static CharacterInputFrame ReadInput()
    {
        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        move = Vector2.ClampMagnitude(move, 1f);

        bool sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
        return new CharacterInputFrame(move, sprintHeld, jumpPressed);
    }

#if UNITY_EDITOR
    private bool EditorApplyCache(SimpleCharacterAutoConfigCache cache, bool overwriteExisting)
    {
        if (cache == null)
        {
            return false;
        }

        bool changed = false;
        if (overwriteExisting)
        {
            changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(cache.MovementSettings, movementSettings);
            changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(cache.AnimationSettings, animationSettings);
        }

        if ((overwriteExisting || idleClip == null) && cache.IdleClip != null && idleClip != cache.IdleClip)
        {
            idleClip = cache.IdleClip;
            changed = true;
        }

        if (overwriteExisting || !runDirectionalSet.HasValidSamples)
        {
            changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(cache.RunDirectionalSet, runDirectionalSet);
        }

        if (overwriteExisting || !sprintDirectionalSet.HasValidSamples)
        {
            changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(cache.SprintDirectionalSet, sprintDirectionalSet);
        }

        if (overwriteExisting || !jumpAnimationSet.HasAnyClip)
        {
            changed |= EditorJsonOverwriteUtility.OverwriteIfChanged(cache.JumpAnimationSet, jumpAnimationSet);
        }

        return changed;
    }
#endif
}

#if UNITY_EDITOR
internal static class EditorJsonOverwriteUtility
{
    public static bool OverwriteIfChanged<T>(T source, T target) where T : class
    {
        if (source == null || target == null)
        {
            return false;
        }

        string sourceJson = JsonUtility.ToJson(source);
        string targetJson = JsonUtility.ToJson(target);
        if (sourceJson == targetJson)
        {
            return false;
        }

        JsonUtility.FromJsonOverwrite(sourceJson, target);
        return true;
    }
}
#endif
