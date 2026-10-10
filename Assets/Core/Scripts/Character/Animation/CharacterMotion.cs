using UnityEngine;

/// <summary>
/// CharacterMotor 输出给 CharacterAnimator 的单帧表现快照。
/// 它只传递动画需要的信息，不暴露 CharacterMotor 内部的物理速度与重力状态。
/// </summary>
public readonly struct CharacterMotion
{
    /// <summary>
    /// 使用明确运动状态创建动画快照。
    /// </summary>
    public CharacterMotion(
        CharacterMovementStateId state,
        Vector2 localDirection,
        float locomotionWeight01)
    {
        State = state;
        LocalDirection = localDirection;
        LocomotionWeight01 = locomotionWeight01;
    }

    /// <summary>
    /// 组合地面状态、冲刺意图、局部方向和移动权重，形成一次完整的动画更新输入。
    /// </summary>
    public CharacterMotion(
        bool isGrounded,
        bool wantsSprint,
        Vector2 localDirection,
        float locomotionWeight01)
        : this(
            !isGrounded
                ? CharacterMovementStateId.Falling
                : wantsSprint
                    ? CharacterMovementStateId.Sprint
                    : locomotionWeight01 > 0.0001f
                        ? CharacterMovementStateId.Run
                        : CharacterMovementStateId.Idle,
            localDirection,
            locomotionWeight01)
    {
    }

    // 离散状态由代码状态机产生，网络同步只需传递这个标识及其连续表现参数。
    public CharacterMovementStateId State { get; }

    // Animator 的 Grounded 参数直接读取该值，负责跳跃和落地两个方向的切换。
    public bool IsGrounded =>
        State == CharacterMovementStateId.Idle
        || State == CharacterMovementStateId.Run
        || State == CharacterMovementStateId.Sprint;

    // 只有存在移动输入时才为 true，CharacterAnimator 据此选择 Sprint 动画组。
    public bool WantsSprint => State == CharacterMovementStateId.Sprint;

    // 角色自身坐标系中的移动方向，写入 Animator 的 MoveX 和 MoveY。
    public Vector2 LocalDirection { get; }

    // 直接来自输入幅度，不使用仍在减速的物理速度，因此松键当帧即可切回 Idle。
    public float LocomotionWeight01 { get; }
}
