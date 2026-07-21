using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterAnimator : IDisposable
{
    public const string TemplateResourcePath = "CharacterLocomotion";

    private const string IdleClipName = "__Idle";
    private const string JumpClipName = "__Jump";
    private const string RunPrefix = "__Run_";
    private const string SprintPrefix = "__Sprint_";

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IdleWeightHash = Animator.StringToHash("IdleWeight");
    private static readonly int RunWeightHash = Animator.StringToHash("RunWeight");
    private static readonly int SprintWeightHash = Animator.StringToHash("SprintWeight");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private static readonly DirectionSlot[] DirectionSlots =
    {
        new DirectionSlot("Forward", new Vector2(0f, 1f)),
        new DirectionSlot("FrontRight", new Vector2(0.7071f, 0.7071f)),
        new DirectionSlot("RightForward", new Vector2(1f, 0.25f)),
        new DirectionSlot("RightBackward", new Vector2(1f, -0.25f)),
        new DirectionSlot("BackRight", new Vector2(0.7071f, -0.7071f)),
        new DirectionSlot("Backward", new Vector2(0f, -1f)),
        new DirectionSlot("BackLeft", new Vector2(-0.7071f, -0.7071f)),
        new DirectionSlot("LeftBackward", new Vector2(-1f, -0.25f)),
        new DirectionSlot("LeftForward", new Vector2(-1f, 0.25f)),
        new DirectionSlot("FrontLeft", new Vector2(-0.7071f, 0.7071f)),
    };

    private readonly Animator animator;
    private readonly AnimationClip idleClip;
    private readonly DirectionalAnimationSet runAnimations;
    private readonly DirectionalAnimationSet sprintAnimations;
    private readonly CharacterJumpAnimationSet jumpAnimations;

    private AnimatorOverrideController overrideController;
    private bool wasGrounded = true;

    public CharacterAnimator(
        Animator animator,
        AnimationClip idleClip,
        DirectionalAnimationSet runAnimations,
        DirectionalAnimationSet sprintAnimations,
        CharacterJumpAnimationSet jumpAnimations)
    {
        this.animator = animator;
        this.idleClip = idleClip;
        this.runAnimations = runAnimations;
        this.sprintAnimations = sprintAnimations;
        this.jumpAnimations = jumpAnimations;
    }

    public bool Initialize()
    {
        if (animator == null)
        {
            return false;
        }

        RuntimeAnimatorController template =
            Resources.Load<RuntimeAnimatorController>(TemplateResourcePath);

        if (template == null)
        {
            Debug.LogError($"缺少 Resources/{TemplateResourcePath}.controller，角色动画无法初始化。", animator);
            return false;
        }

        AnimationClip fallback = ResolveFallbackClip();
        overrideController = new AnimatorOverrideController(template);

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);
        for (int index = 0; index < overrides.Count; index++)
        {
            AnimationClip source = overrides[index].Key;
            AnimationClip replacement = ResolveClip(source.name, fallback);
            overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(source, replacement ?? source);
        }

        overrideController.ApplyOverrides(overrides);
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = overrideController;
        animator.Rebind();
        animator.SetBool(GroundedHash, true);
        ResetMovementParameters();
        wasGrounded = true;
        return true;
    }

    public void Apply(CharacterMotion motion)
    {
        if (overrideController == null)
        {
            return;
        }

        if (motion.IsGrounded && !wasGrounded)
        {
            // 落地先回到 Idle，避免空中保留的跑步权重影响衔接。
            ResetMovementParameters();
        }
        else
        {
            ApplyGroundedBlend(motion);
        }

        animator.SetBool(GroundedHash, motion.IsGrounded);
        wasGrounded = motion.IsGrounded;

        if (motion.JumpStarted)
        {
            animator.SetTrigger(JumpHash);
        }
    }

    public void Dispose()
    {
        if (overrideController == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(overrideController);
        overrideController = null;
    }

    private AnimationClip ResolveClip(string sourceName, AnimationClip fallback)
    {
        if (sourceName == IdleClipName)
        {
            return idleClip != null ? idleClip : fallback;
        }

        if (sourceName == JumpClipName)
        {
            return jumpAnimations != null ? jumpAnimations.GetJumpClip(fallback) : fallback;
        }

        for (int index = 0; index < DirectionSlots.Length; index++)
        {
            DirectionSlot slot = DirectionSlots[index];
            if (sourceName == RunPrefix + slot.Name)
            {
                return FindDirectionalClip(runAnimations, slot.Direction, fallback);
            }

            if (sourceName == SprintPrefix + slot.Name)
            {
                AnimationClip runFallback = FindDirectionalClip(runAnimations, slot.Direction, fallback);
                return FindDirectionalClip(sprintAnimations, slot.Direction, runFallback);
            }
        }

        return fallback;
    }

    private AnimationClip ResolveFallbackClip()
    {
        if (idleClip != null)
        {
            return idleClip;
        }

        return FindDirectionalClip(runAnimations, Vector2.up, null);
    }

    private void ResetMovementParameters()
    {
        animator.SetFloat(MoveXHash, 0f);
        animator.SetFloat(MoveYHash, 0f);
        animator.SetFloat(IdleWeightHash, 1f);
        animator.SetFloat(RunWeightHash, 0f);
        animator.SetFloat(SprintWeightHash, 0f);
    }

    private void ApplyGroundedBlend(CharacterMotion motion)
    {
        float locomotionWeight = Mathf.Clamp01(motion.MoveMagnitude01);
        bool useSprint = motion.WantsSprint && HasAnimations(sprintAnimations);
        bool useRun = !useSprint && HasAnimations(runAnimations);

        if (!useRun && !useSprint && HasAnimations(runAnimations))
        {
            useRun = true;
        }

        if (!useRun && !useSprint && HasAnimations(sprintAnimations))
        {
            useSprint = true;
        }

        // 与历史版本一致：每帧直接混合 Idle/Run/Sprint，不增加状态门槛和二次阻尼。
        bool hasLocomotion = useRun || useSprint;
        float idleWeight = idleClip != null
            ? 1f - (hasLocomotion ? locomotionWeight : 0f)
            : 0f;

        animator.SetFloat(MoveXHash, motion.LocalDirection.x);
        animator.SetFloat(MoveYHash, motion.LocalDirection.y);
        animator.SetFloat(IdleWeightHash, idleWeight);
        animator.SetFloat(RunWeightHash, useRun ? locomotionWeight : 0f);
        animator.SetFloat(SprintWeightHash, useSprint ? locomotionWeight : 0f);
    }

    private static AnimationClip FindDirectionalClip(
        DirectionalAnimationSet animationSet,
        Vector2 direction,
        AnimationClip fallback)
    {
        return animationSet != null
            ? animationSet.FindClosestClip(direction, fallback)
            : fallback;
    }

    private static bool HasAnimations(DirectionalAnimationSet animationSet)
    {
        return animationSet != null && animationSet.HasValidSamples;
    }

    private readonly struct DirectionSlot
    {
        public DirectionSlot(string name, Vector2 direction)
        {
            Name = name;
            Direction = direction;
        }

        public string Name { get; }
        public Vector2 Direction { get; }
    }
}
