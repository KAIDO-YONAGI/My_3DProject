using System;
using Core.My_FrameWork.Scripts.StateMachine;

namespace Core.Scripts.Character.Movement.States
{
    /// <summary>
    /// 角色运动状态机对外使用的离散状态。
    /// 这五个值是网络协议的一部分；分层结构新增的根、地面、空中三个父状态只参与转换解析，
    /// 不进入本枚举，因此网络线格式与分层前保持一致。
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

    /// <summary>
    /// 角色运动状态节点的公共基类，复用框架的分层状态机。
    /// </summary>
    internal abstract class CharacterMovementStateNode : HierarchicalState<CharacterMovementStateContext>
    {
        protected CharacterMovementStateNode(CharacterMovementStateNode parent) : base(parent)
        {
        }
    }

    /// <summary>
    /// 叶子状态基类。只有叶子状态才对应一个对外的离散标识，父状态不参与网络同步。
    /// </summary>
    internal abstract class CharacterMovementLeafState : CharacterMovementStateNode
    {
        protected CharacterMovementLeafState(CharacterMovementStateNode parent) : base(parent)
        {
        }

        public abstract CharacterMovementStateId Id { get; }
    }

    /// <summary>
    /// 角色运动状态机。
    /// 每帧只接受一次输入和碰撞结果，状态转换在当帧同步完成，转换完成后由 CharacterMotor 统一执行一次位移。
    /// 地面三态与空中两态的共有规则分别上提到地面与空中父状态，叶子状态只保留自身标识。
    /// </summary>
    public class CharacterMovementStateMachine
    {
        private readonly CharacterMovementRoot root;
        private readonly HierarchicalStateMachine<CharacterMovementStateContext> machine;

        public CharacterMovementStateMachine()
        {
            root = new CharacterMovementRoot();
            machine = new HierarchicalStateMachine<CharacterMovementStateContext>(root);

            // 启动只按默认子树下钻，不解析转换，保证构造后就是 Idle。
            machine.Start(default);
            PreviousStateId = CurrentStateId;
        }

        /// <summary>当前叶子状态对应的离散标识。</summary>
        public CharacterMovementStateId CurrentStateId
        {
            get
            {
                if (machine.Leaf is CharacterMovementLeafState leaf)
                {
                    return leaf.Id;
                }

                throw new InvalidOperationException("角色运动状态机的活动叶子不是可同步的叶子状态。");
            }
        }

        /// <summary>
        /// 上一次转换前的离散标识，供表现层做落地、起跳这类边沿检测；
        /// 尚未发生转换时与 CurrentStateId 相同。
        /// </summary>
        public CharacterMovementStateId PreviousStateId { get; private set; }

        /// <summary>本帧是否发生过状态转换。</summary>
        public bool ChangedThisFrame { get; private set; }

        public bool CanJump(bool groundedBeforeMove)
        {
            return groundedBeforeMove;
        }

        public CharacterMovementStateId Update(CharacterMovementStateContext context)
        {
            CharacterMovementStateId previous = CurrentStateId;
            machine.Update(context);
            CharacterMovementStateId current = CurrentStateId;

            // 边沿信息在状态的进入和退出钩子执行完毕后才成立，同一帧即可被表现层读取。
            ChangedThisFrame = current != previous;
            if (ChangedThisFrame)
            {
                PreviousStateId = previous;
            }

            return current;
        }
    }
}
