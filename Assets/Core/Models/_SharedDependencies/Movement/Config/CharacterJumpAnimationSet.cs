using System;
using UnityEngine;

[Serializable]
public sealed class CharacterJumpAnimationSet
{
    [SerializeField] private AnimationClip fullJumpClip;
    [SerializeField] private AnimationClip jumpStartClip;
    [SerializeField] private AnimationClip jumpUpLoopClip;
    [SerializeField] private AnimationClip jumpDownLoopClip;

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

        return jumpDownLoopClip != null ? jumpDownLoopClip : fallback;
    }
}
