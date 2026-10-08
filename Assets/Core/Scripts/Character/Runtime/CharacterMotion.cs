using UnityEngine;

/// <summary>
/// CharacterMotor 输出给 CharacterAnimator 的单帧表现快照。
/// 它只传递动画需要的信息，不暴露 CharacterMotor 内部的物理速度与重力状态。
/// </summary>
public readonly struct CharacterMotion
{
    /// <summary>
    /// 组合地面状态、冲刺意图、局部方向和移动权重，形成一次完整的动画更新输入。
    /// </summary>
    public CharacterMotion(
        bool isGrounded,
        bool wantsSprint,
        Vector2 localDirection,
        float locomotionWeight01)
    {
        IsGrounded = isGrounded;
        WantsSprint = wantsSprint;
        LocalDirection = localDirection;
        LocomotionWeight01 = locomotionWeight01;
    }

    // Animator 的 Grounded 参数直接读取该值，负责跳跃和落地两个方向的切换。
    public bool IsGrounded { get; }

    // 只有存在移动输入时才为 true，CharacterAnimator 据此选择 Sprint 动画组。
    public bool WantsSprint { get; }

    // 角色自身坐标系中的移动方向，写入 Animator 的 MoveX 和 MoveY。
    public Vector2 LocalDirection { get; }

    // 直接来自输入幅度，不使用仍在减速的物理速度，因此松键当帧即可切回 Idle。
    public float LocomotionWeight01 { get; }
}
