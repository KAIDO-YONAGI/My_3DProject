using Core.Scripts.Character.Movement.States;
using UnityEngine;

namespace Core.Scripts.Character.Animation
{
    /// <summary>
    /// 运动快照中能够跨客户端复现的部分，也是网络传输的唯一格式。
    /// 本地专有的信息（例如上一状态、转换边沿）不在此列：远程角色拿不到它们，
    /// 因此表现层不得依赖 <see cref="CharacterMotion"/> 上任何超出本结构体的字段，
    /// 否则本地表现正常、远程静默失效。
    /// </summary>
    public readonly struct CharacterMotionSnapshot
    {
        /// <summary>
        /// 使用三个可传输字段创建快照，字段顺序与网络读写顺序保持一致。
        /// </summary>
        public CharacterMotionSnapshot(
            CharacterMovementStateId state,
            Vector2 localDirection,
            float locomotionWeight01)
        {
            State = state;
            LocalDirection = localDirection;
            LocomotionWeight01 = locomotionWeight01;
        }

        // 离散状态由代码状态机产生，网络同步只需传递这个标识及其连续表现参数。
        public CharacterMovementStateId State { get; }

        // 角色自身坐标系中的移动方向，写入 Animator 的 MoveX 和 MoveY。
        public Vector2 LocalDirection { get; }

        // 直接来自输入幅度，不使用仍在减速的物理速度，因此松键当帧即可切回 Idle。
        public float LocomotionWeight01 { get; }
    }
}
