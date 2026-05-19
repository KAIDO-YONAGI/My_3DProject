using System;
using UnityEngine;

[Serializable]
public sealed class SimpleCharacterJumpAnimationSet
{
    [SerializeField] private AnimationClip fullJumpClip;
    [SerializeField] private AnimationClip jumpStartClip;
    [SerializeField] private AnimationClip jumpUpLoopClip;
    [SerializeField] private AnimationClip jumpDownLoopClip;
    [SerializeField] private AnimationClip jumpLandIdleClip;
    [SerializeField] private AnimationClip jumpLandMoveClip;

    public AnimationClip FullJumpClip => fullJumpClip;
    public AnimationClip JumpStartClip => jumpStartClip;
    public AnimationClip JumpUpLoopClip => jumpUpLoopClip;
    public AnimationClip JumpDownLoopClip => jumpDownLoopClip;
    public AnimationClip JumpLandIdleClip => jumpLandIdleClip;
    public AnimationClip JumpLandMoveClip => jumpLandMoveClip;
    public bool HasFullJumpClip => fullJumpClip != null;
    public bool HasAnyClip =>
        fullJumpClip != null
        || jumpStartClip != null
        || jumpUpLoopClip != null
        || jumpDownLoopClip != null
        || jumpLandIdleClip != null
        || jumpLandMoveClip != null;

    public AnimationClip GetLandingClip(bool moving)
    {
        if (moving)
        {
            return jumpLandMoveClip != null ? jumpLandMoveClip : jumpLandIdleClip;
        }

        return jumpLandIdleClip != null ? jumpLandIdleClip : jumpLandMoveClip;
    }

    public bool TryAssignFullJump(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref fullJumpClip, clip, overwriteExisting);
    }

    public bool TryAssignJumpStart(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref jumpStartClip, clip, overwriteExisting);
    }

    public bool TryAssignJumpUpLoop(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref jumpUpLoopClip, clip, overwriteExisting);
    }

    public bool TryAssignJumpDownLoop(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref jumpDownLoopClip, clip, overwriteExisting);
    }

    public bool TryAssignLandIdle(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref jumpLandIdleClip, clip, overwriteExisting);
    }

    public bool TryAssignLandMove(AnimationClip clip, bool overwriteExisting)
    {
        return TryAssign(ref jumpLandMoveClip, clip, overwriteExisting);
    }

    private static bool TryAssign(ref AnimationClip target, AnimationClip source, bool overwriteExisting)
    {
        if (source == null)
        {
            return false;
        }

        if (!overwriteExisting && target != null)
        {
            return false;
        }

        if (target == source)
        {
            return false;
        }

        target = source;
        return true;
    }
}
