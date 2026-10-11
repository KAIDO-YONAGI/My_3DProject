using Core.My_FrameWork.Scripts.StateMachine;

namespace Core.Scripts.Character.Movement.States
{
    /// <summary>
    /// 根状态：承担起跳这条跨子树的转换。
    /// 起跳当帧会清零离地计时，因此仍落在容忍窗口内，
    /// 必须先于地面子树判定，否则向上的速度会被当成贴地而误判为 Run 或 Idle。
    /// </summary>
    internal sealed class CharacterMovementRoot : CharacterMovementStateNode
    {
        public CharacterMovementRoot() : base(null)
        {
            Grounded = new GroundedStateNode(this);
            Airborne = new AirborneStateNode(this);
        }

        public GroundedStateNode Grounded { get; }

        public AirborneStateNode Airborne { get; }

        protected override HierarchicalState<CharacterMovementStateContext> InitialChild => Grounded;

        protected override HierarchicalState<CharacterMovementStateContext> ResolveTransition(
            CharacterMovementStateContext context)
        {
            return context.JumpStarted ? Airborne.Rising : null;
        }

        /// <summary>
        /// 按竖直速度方向解析空中两态，供地面离地判定与空中换向共用。
        /// </summary>
        public CharacterMovementLeafState ResolveAirborneTarget(CharacterMovementStateContext context)
        {
            return context.VerticalVelocity > 0f ? Airborne.Rising : Airborne.Falling;
        }
    }

    /// <summary>
    /// 地面子树：承担离地判定与 Idle、Run、Sprint 三态解析。
    /// 三个地面状态原先各自重复一遍起跳与离地判定，现在统一由本状态承担。
    /// </summary>
    internal sealed class GroundedStateNode : CharacterMovementStateNode
    {
        private readonly CharacterMovementRoot root;

        public GroundedStateNode(CharacterMovementRoot root) : base(root)
        {
            this.root = root;
            Idle = new IdleCharacterMovementState(this);
            Run = new RunCharacterMovementState(this);
            Sprint = new SprintCharacterMovementState(this);
        }

        public IdleCharacterMovementState Idle { get; }

        public RunCharacterMovementState Run { get; }

        public SprintCharacterMovementState Sprint { get; }

        protected override HierarchicalState<CharacterMovementStateContext> InitialChild => Idle;

        protected override HierarchicalState<CharacterMovementStateContext> ResolveTransition(
            CharacterMovementStateContext context)
        {
            // 下坡与台阶会造成短暂悬空；容忍窗口内继续解析地面状态，
            // 避免动画在 Grounded 与 Jump 之间来回切换。
            if (!context.GroundedAfterMove && !context.WithinAirborneTolerance)
            {
                return root.ResolveAirborneTarget(context);
            }

            CharacterMovementLeafState target = ResolveGroundedTarget(context);
            return ReferenceEquals(ActiveChild, target) ? null : target;
        }

        /// <summary>
        /// 按移动输入和冲刺意图解析地面三态；空中落地时复用同一规则。
        /// </summary>
        public CharacterMovementLeafState ResolveGroundedTarget(CharacterMovementStateContext context)
        {
            if (!context.HasMoveInput)
            {
                return Idle;
            }

            return context.SprintHeld ? Sprint : Run;
        }
    }

    /// <summary>
    /// 空中子树：承担接地判定与 Rising、Falling 两态解析。
    /// 两个空中状态原先各自镜像一遍速度换向判定，现在统一由本状态承担。
    /// </summary>
    internal sealed class AirborneStateNode : CharacterMovementStateNode
    {
        private readonly CharacterMovementRoot root;

        public AirborneStateNode(CharacterMovementRoot root) : base(root)
        {
            this.root = root;
            Rising = new RisingCharacterMovementState(this);
            Falling = new FallingCharacterMovementState(this);
        }

        public RisingCharacterMovementState Rising { get; }

        public FallingCharacterMovementState Falling { get; }

        protected override HierarchicalState<CharacterMovementStateContext> InitialChild => Rising;

        protected override HierarchicalState<CharacterMovementStateContext> ResolveTransition(
            CharacterMovementStateContext context)
        {
            // 移动后接地即回到地面子树；落地当帧直接给出目标地面状态，不额外多停一帧。
            if (context.GroundedAfterMove || context.WithinAirborneTolerance)
            {
                return root.Grounded.ResolveGroundedTarget(context);
            }

            CharacterMovementLeafState target = root.ResolveAirborneTarget(context);
            return ReferenceEquals(ActiveChild, target) ? null : target;
        }
    }

    /// <summary>没有移动输入时的地面状态。</summary>
    internal sealed class IdleCharacterMovementState : CharacterMovementLeafState
    {
        public IdleCharacterMovementState(GroundedStateNode parent) : base(parent)
        {
        }

        public override CharacterMovementStateId Id => CharacterMovementStateId.Idle;
    }

    /// <summary>有移动输入且未按冲刺键时的地面状态。</summary>
    internal sealed class RunCharacterMovementState : CharacterMovementLeafState
    {
        public RunCharacterMovementState(GroundedStateNode parent) : base(parent)
        {
        }

        public override CharacterMovementStateId Id => CharacterMovementStateId.Run;
    }

    /// <summary>有移动输入且按住冲刺键时的地面状态。</summary>
    internal sealed class SprintCharacterMovementState : CharacterMovementLeafState
    {
        public SprintCharacterMovementState(GroundedStateNode parent) : base(parent)
        {
        }

        public override CharacterMovementStateId Id => CharacterMovementStateId.Sprint;
    }

    /// <summary>竖直速度向上的空中状态。</summary>
    internal sealed class RisingCharacterMovementState : CharacterMovementLeafState
    {
        public RisingCharacterMovementState(AirborneStateNode parent) : base(parent)
        {
        }

        public override CharacterMovementStateId Id => CharacterMovementStateId.Rising;
    }

    /// <summary>竖直速度向下或静止的空中状态。</summary>
    internal sealed class FallingCharacterMovementState : CharacterMovementLeafState
    {
        public FallingCharacterMovementState(AirborneStateNode parent) : base(parent)
        {
        }

        public override CharacterMovementStateId Id => CharacterMovementStateId.Falling;
    }
}
