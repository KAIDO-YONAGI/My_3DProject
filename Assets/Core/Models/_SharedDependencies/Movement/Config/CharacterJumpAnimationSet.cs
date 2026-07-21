using System;
using UnityEngine;

/// <summary>
/// 保存单个角色的跳跃动画素材。
/// CharacterAnimator 会从这里选出一个片段，替换通用 Animator 模板中的跳跃占位动画。
/// </summary>
[Serializable]
public sealed class CharacterJumpAnimationSet
{
    // 素材本身已经包含完整起跳和落地时，优先使用这一段。
    [SerializeField] private AnimationClip fullJumpClip;

    // 以下三个字段兼容只有分段跳跃素材的角色。
    [SerializeField] private AnimationClip jumpStartClip;
    [SerializeField] private AnimationClip jumpUpLoopClip;
    [SerializeField] private AnimationClip jumpDownLoopClip;

    /// <summary>
    /// 按可表现完整跳跃的优先级选择片段；全部未配置时使用调用方提供的兜底动画。
    /// </summary>
    public AnimationClip GetJumpClip(AnimationClip fallback)
    {
        if (fullJumpClip != null)
        {
            return fullJumpClip;
        }

        if (jumpStartClip != null)
        {
            return jumpStartClip;
        }

        if (jumpUpLoopClip != null)
        {
            return jumpUpLoopClip;
        }

        // 下落片段是最后一个可用配置，仍为空时才回退到通用素材。
        return jumpDownLoopClip != null ? jumpDownLoopClip : fallback;
    }
}
