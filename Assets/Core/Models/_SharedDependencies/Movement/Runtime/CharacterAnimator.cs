using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责动画素材绑定和参数驱动。
/// 初始化时把角色自己的 Idle、Run、Sprint、Jump 片段覆盖到通用 Animator 模板；
/// 运行时读取 CharacterMotion，得到最终的待机、方向移动、冲刺、跳跃和落地表现。
/// </summary>
public sealed class CharacterAnimator : IDisposable
{
    // Resources.Load 使用不带扩展名的相对路径加载通用 Animator 模板。
    public const string TemplateResourcePath = "CharacterLocomotion";

    // 模板片段名是稳定槽位，具体角色素材通过 AnimatorOverrideController 替换。
    private const string IdleClipName = "__Idle";
    private const string JumpClipName = "__Jump";
    private const string RunPrefix = "__Run_";
    private const string SprintPrefix = "__Sprint_";

    // 缓存参数哈希，避免 Update 中反复按字符串查找 Animator 参数。
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IdleWeightHash = Animator.StringToHash("IdleWeight");
    private static readonly int RunWeightHash = Animator.StringToHash("RunWeight");
    private static readonly int SprintWeightHash = Animator.StringToHash("SprintWeight");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    // 每个槽位同时定义模板后缀和二维方向，用于从角色素材中寻找最近动画。
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

    // Animator 是最终输出端，其余字段来自 PlayerCharacterController 的角色专属配置。
    private readonly Animator animator;
    private readonly AnimationClip idleClip;
    private readonly DirectionalAnimationSet runAnimations;
    private readonly DirectionalAnimationSet sprintAnimations;
    private readonly CharacterJumpAnimationSet jumpAnimations;

    // 覆盖器由本类创建和销毁；wasGrounded 用于只在落地边沿执行一次 Idle 重置。
    private AnimatorOverrideController overrideController;
    private bool wasGrounded = true;

    /// <summary>
    /// 接收 Animator 与角色素材配置，实际模板加载延迟到 Initialize。
    /// </summary>
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

    /// <summary>
    /// 加载通用模板、替换所有动画槽位，并把 Animator 初始化为 Grounded + Idle。
    /// </summary>
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

        // fallback 保证个别方向缺素材时，覆盖器仍能得到可播放片段。
        AnimationClip fallback = ResolveFallbackClip();
        overrideController = new AnimatorOverrideController(template);

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);
        for (int index = 0; index < overrides.Count; index++)
        {
            // 根据模板占位片段名称决定应当绑定待机、跳跃、跑步还是冲刺素材。
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

    /// <summary>
    /// 把 CharacterMotion 应用到 Animator。
    /// Grounded 参数负责进入和退出 Jump，移动权重只负责 Grounded 内部的直接混合。
    /// </summary>
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

        // 单一布尔值同时覆盖主动跳跃和走下平台，不再与 Jump Trigger 形成两条竞争路径。
        animator.SetBool(GroundedHash, motion.IsGrounded);
        wasGrounded = motion.IsGrounded;
    }

    /// <summary>
    /// 释放运行时创建的 AnimatorOverrideController，避免角色反复生成销毁时遗留对象。
    /// </summary>
    public void Dispose()
    {
        if (overrideController == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(overrideController);
        overrideController = null;
    }

    /// <summary>
    /// 根据通用模板片段名称解析角色实际应播放的动画。
    /// </summary>
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

    /// <summary>
    /// 优先用 Idle 作为缺失素材回退；没有 Idle 时再尝试前进跑步片段。
    /// </summary>
    private AnimationClip ResolveFallbackClip()
    {
        if (idleClip != null)
        {
            return idleClip;
        }

        return FindDirectionalClip(runAnimations, Vector2.up, null);
    }

    /// <summary>
    /// 把 Grounded Blend Tree 立即设为纯 Idle，用于初始化和落地首帧。
    /// </summary>
    private void ResetMovementParameters()
    {
        animator.SetFloat(MoveXHash, 0f);
        animator.SetFloat(MoveYHash, 0f);
        animator.SetFloat(IdleWeightHash, 1f);
        animator.SetFloat(RunWeightHash, 0f);
        animator.SetFloat(SprintWeightHash, 0f);
    }

    /// <summary>
    /// 根据输入意图直接设置 Idle、Run、Sprint 权重，并把局部方向写入二维 Blend Tree。
    /// </summary>
    private void ApplyGroundedBlend(CharacterMotion motion)
    {
        float locomotionWeight = Mathf.Clamp01(motion.LocomotionWeight01);
        bool hasRunAnimations = HasAnimations(runAnimations);
        bool hasSprintAnimations = HasAnimations(sprintAnimations);
        bool useSprint = motion.WantsSprint && hasSprintAnimations;
        bool useRun = !useSprint && hasRunAnimations;

        // 没有跑步素材但存在冲刺素材时，普通移动也用冲刺组兜底。
        if (!useRun && !useSprint && hasSprintAnimations)
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

    /// <summary>
    /// 从指定动画组中寻找最接近模板槽位方向的片段。
    /// </summary>
    private static AnimationClip FindDirectionalClip(
        DirectionalAnimationSet animationSet,
        Vector2 direction,
        AnimationClip fallback)
    {
        return animationSet != null
            ? animationSet.FindClosestClip(direction, fallback)
            : fallback;
    }

    /// <summary>
    /// 判断动画组是否至少包含一个可用片段。
    /// </summary>
    private static bool HasAnimations(DirectionalAnimationSet animationSet)
    {
        return animationSet != null && animationSet.HasValidSamples;
    }

    // 将模板使用的方向名称和二维坐标绑定在一个不可变值中。
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
