using Core.Scripts.Character.Animation;
using Core.Scripts.Character.Control;
using Core.Scripts.Character.Movement.States;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking
{
    /// <summary>
    /// 挂在 NetworkPlayer 根节点，同步控制器的运动状态、局部方向和动画权重。
    /// 拥有者客户端产生快照，服务器校验并保存后转发，远程控制器读取快照驱动动画。
    /// </summary>
    public class NetworkCharacterMovementSync : NetworkBehaviour
    {
        // 缓存同一根节点上的控制器，发送时读取 CurrentMotion，接收时写入远程快照。
        private PlayerCharacterController controller;

        private void Awake()
        {
            controller = GetComponent<PlayerCharacterController>();

            // 同步方向：拥有者客户端发送到服务器，服务器再转发给其他观察者客户端。
            syncDirection = SyncDirection.ClientToServer;

            // 可靠传输：丢包后重传，接收端按发送顺序收到状态数据。
            syncMethod = SyncMethod.Reliable;

            // 观察者是 Mirror 为此网络对象维护的接收客户端，运动状态向这些客户端同步。
            syncMode = SyncMode.Observers;

            // 两次常规同步至少间隔 0.1 秒，每秒最多约 10 次；实际时机还受 Mirror 发送频率影响。
            // 设为 0 时，组件使用 Mirror 的发送节奏，待发送标记仍由 SetDirty 提供。
            syncInterval = 0.1f;
        }

        private void LateUpdate()
        {
            // 拥有者持续标记待发送，实际发送频率由 Mirror 控制。
            // isOwned 表示当前客户端拥有此网络对象，主机的本地玩家也走这个入口。
            // SetDirty 标记组件需要同步，Mirror 到发送时机调用 OnSerialize 并清除标记。
            // 即使角色静止，也持续提供快照，让接收端保持更新。
            if (isOwned)
                SetDirty();
        }

        /// <summary>
        /// 序列化就是把快照字段按约定顺序写入网络数据。
        /// Mirror 在拥有者客户端上行和服务器向观察者发送状态时调用此方法。
        /// </summary>
        /// <param name="writer">Mirror 提供的网络数据写入器。</param>
        /// <param name="initialState">
        /// 为 true 时表示对象的初始状态同步。初始与后续同步均完整写入三个字段。
        /// </param>
        public override void OnSerialize(
            NetworkWriter writer, bool initialState)
        {
            // 拥有者读取本地移动结果；服务器读取 OnDeserialize 保存的有效快照。
            CharacterMotion motion = controller.CurrentMotion;

            // 状态编号：Idle=0、Run=1、Sprint=2、Rising=3、Falling=4。
            writer.WriteByte((byte)motion.State);
            // 角色自身坐标系的方向：X 表示左右，Y 表示前后，供方向动画混合使用。
            writer.WriteVector2(motion.LocalDirection);
            // 动画移动权重为 0 到 1，来自输入幅度。
            writer.WriteFloat(motion.LocomotionWeight01);
        }

        /// <summary>
        /// 反序列化就是从网络数据中还原快照，服务器和远程客户端均通过此入口接收。
        /// 接收端校验字段后，把有效快照保存到控制器。
        /// </summary>
        /// <param name="reader">Mirror 提供的网络数据读取器。</param>
        /// <param name="initialState">表示是否为初始同步，读取格式与 OnSerialize 保持一致。</param>
        public override void OnDeserialize(
            NetworkReader reader, bool initialState)
        {
            // 本地拥有者使用自己计算的状态。
            if (isOwned) 
                return;
            
            // 读取顺序和写入顺序完全一致。
            byte state = reader.ReadByte();
            Vector2 direction = reader.ReadVector2();
            float weight = reader.ReadFloat();

            // 校验状态编号、方向范围和权重，同时排除非有限数值。
            // 方向长度平方允许少量浮点误差；异常数据直接返回，控制器保留上一份有效快照。
            if (state > (byte)CharacterMovementStateId.Falling
                || !(direction.sqrMagnitude <= 1.001f)
                || !(weight >= 0f && weight <= 1f))
                return;



            // 服务器保存该快照供后续发送，Mirror 收到拥有者数据后会自动标记组件待转发。
            // 远程客户端保存该快照供动画读取；主机上的远程玩家使用同一个控制器接收入口。
            controller.ReceiveRemoteMotion(new CharacterMotion(
                (CharacterMovementStateId)state, direction, weight));
        }
    }
}
