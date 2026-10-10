using System;
using System.Collections.Generic;

namespace Core.Scripts.Character.Movement.States
{
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
        /// <summary>
        /// 使用离地容忍判定创建上下文；withinAirborneTolerance 由 CharacterMotor 按配置的容忍时长计算。
        /// </summary>
        public CharacterMovementStateContext(
            bool groundedBeforeMove,
            bool groundedAfterMove,
            bool hasMoveInput,
            bool sprintHeld,
            bool jumpStarted,
            float verticalVelocity,
            bool withinAirborneTolerance)
        {
            GroundedBeforeMove = groundedBeforeMove;
            GroundedAfterMove = groundedAfterMove;
            HasMoveInput = hasMoveInput;
            SprintHeld = sprintHeld;
            JumpStarted = jumpStarted;
            VerticalVelocity = verticalVelocity;
            WithinAirborneTolerance = withinAirborneTolerance;
        }

        /// <summary>
        /// 兼容未提供离地容忍判定的调用方：等同于“已超出容忍窗口”，保持原有空中状态判定。
        /// </summary>
        public CharacterMovementStateContext(
            bool groundedBeforeMove,
            bool groundedAfterMove,
            bool hasMoveInput,
            bool sprintHeld,
            bool jumpStarted,
            float verticalVelocity)
            : this(
                groundedBeforeMove,
                groundedAfterMove,
                hasMoveInput,
                sprintHeld,
                jumpStarted,
                verticalVelocity,
                false)
        {
        }

        public bool GroundedBeforeMove { get; }
        public bool GroundedAfterMove { get; }
        public bool HasMoveInput { get; }
        public bool SprintHeld { get; }
        public bool JumpStarted { get; }
        public float VerticalVelocity { get; }

        // 非跳跃离地仍在容忍窗口内时按贴地处理，避免下坡与台阶造成的悬空帧误播跳跃动画。
        public bool WithinAirborneTolerance { get; }
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
            // 下坡与台阶会造成短暂悬空；容忍窗口内继续解析地面状态，
            // 避免动画在 Grounded 与 Jump 之间来回切换。
            if (!context.GroundedAfterMove && !context.WithinAirborneTolerance)
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
}