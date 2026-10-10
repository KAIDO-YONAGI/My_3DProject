using System;
using System.Collections.Generic;

/// <summary>
/// 角色运动状态机对外使用的离散状态。
/// </summary>
public enum CharacterMovementStateId : byte
{
    Idle = 0,
    Run = 1,
    Sprint = 2,
    Rising = 3,
    Falling = 4
}

/// <summary>
/// 状态机处理一帧运动后的输入和碰撞结果。
/// </summary>
public readonly struct CharacterMovementStateContext
{
    public CharacterMovementStateContext(
        bool groundedBeforeMove,
        bool groundedAfterMove,
        bool hasMoveInput,
        bool sprintHeld,
        bool jumpStarted,
        float verticalVelocity)
    {
        GroundedBeforeMove = groundedBeforeMove;
        GroundedAfterMove = groundedAfterMove;
        HasMoveInput = hasMoveInput;
        SprintHeld = sprintHeld;
        JumpStarted = jumpStarted;
        VerticalVelocity = verticalVelocity;
    }

    public bool GroundedBeforeMove { get; }
    public bool GroundedAfterMove { get; }
    public bool HasMoveInput { get; }
    public bool SprintHeld { get; }
    public bool JumpStarted { get; }
    public float VerticalVelocity { get; }
}

internal interface ICharacterMovementState
{
    CharacterMovementStateId Id { get; }
    void Enter(CharacterMovementStateContext context);
    CharacterMovementStateId Update(CharacterMovementStateContext context);
    void Exit(CharacterMovementStateContext context);
}

internal abstract class CharacterMovementStateBase : ICharacterMovementState
{
    public abstract CharacterMovementStateId Id { get; }

    public virtual void Enter(CharacterMovementStateContext context)
    {
    }

    public abstract CharacterMovementStateId Update(CharacterMovementStateContext context);

    public virtual void Exit(CharacterMovementStateContext context)
    {
    }

    protected static CharacterMovementStateId ResolveGroundedState(CharacterMovementStateContext context)
    {
        if (!context.GroundedAfterMove)
        {
            return context.VerticalVelocity > 0f
                ? CharacterMovementStateId.Rising
                : CharacterMovementStateId.Falling;
        }

        if (!context.HasMoveInput)
        {
            return CharacterMovementStateId.Idle;
        }

        return context.SprintHeld
            ? CharacterMovementStateId.Sprint
            : CharacterMovementStateId.Run;
    }
}

/// <summary>
/// 角色运动状态机。
/// 每帧只接受一次输入和碰撞结果，状态转换完成后由 CharacterMotor 统一执行一次位移。
/// </summary>
public class CharacterMovementStateMachine
{
    private readonly Dictionary<CharacterMovementStateId, ICharacterMovementState> states =
        new Dictionary<CharacterMovementStateId, ICharacterMovementState>
        {
            { CharacterMovementStateId.Idle, new IdleCharacterMovementState() },
            { CharacterMovementStateId.Run, new RunCharacterMovementState() },
            { CharacterMovementStateId.Sprint, new SprintCharacterMovementState() },
            { CharacterMovementStateId.Rising, new RisingCharacterMovementState() },
            { CharacterMovementStateId.Falling, new FallingCharacterMovementState() }
        };

    private ICharacterMovementState currentState;

    public CharacterMovementStateMachine()
    {
        currentState = states[CharacterMovementStateId.Idle];
        currentState.Enter(default);
    }

    public CharacterMovementStateId CurrentStateId => currentState.Id;

    public bool CanJump(bool groundedBeforeMove)
    {
        return groundedBeforeMove;
    }

    public CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        CharacterMovementStateId nextState = currentState.Update(context);
        if (nextState == currentState.Id)
        {
            return currentState.Id;
        }

        currentState.Exit(context);
        if (!states.TryGetValue(nextState, out ICharacterMovementState replacement))
        {
            throw new InvalidOperationException($"未注册角色运动状态 {nextState}。");
        }

        currentState = replacement;
        currentState.Enter(context);
        return currentState.Id;
    }
}
