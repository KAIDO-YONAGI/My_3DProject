using Core.Scripts.Character.Animation;
using Core.Scripts.Character.Input;
using Core.Scripts.Character.Movement;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Character.Control
{
    /// <summary>
    /// 角色控制器的 Unity 生命周期入口。
    /// 本类只负责组织“采集输入 -> CharacterMotor 位移 -> CharacterAnimator 表现”的顺序；
    /// 运动对象在 Awake 建立，视觉实例的动画与输入参考由表现层在绑定阶段一次传入。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("场景引用")]
        // 单机 Prefab 直接序列化 Animator；联机在视觉实例创建后由 BindModel 传入。
        [SerializeField] private Animator animator;

        // 必须与本组件位于同一对象，RequireComponent 保证组件存在。
        [SerializeField] private CharacterController characterController;

        // 单机 Prefab 序列化输入相机；联机由 BindModel 传入本地视觉的输入相机。
        [SerializeField] private Transform inputSpace;

        [Header("移动")]
        // 参数对象只描述手感，实际速度状态保存在 CharacterMotor 中。
        [SerializeField] private CharacterMovementSettings movementSettings = new();

        // 输入读取器按需创建 PlayerControls：远程玩家不读输入，就不会占用输入资源。
        private readonly CharacterInputReader input = new();

        // 运动与动画对象在装配阶段创建，之后每帧只做位移和参数写入。
        private CharacterMotor motor;
        private CharacterAnimator animationDriver;
        private NetworkIdentity networkIdentity;

        // 视觉实例绑定完成前不模拟：联机本地玩家需要视觉相机作为输入参考。
        private bool characterBound;

        /// <summary>本帧运动结果：本地玩家写入本地模拟结果，远程玩家接收网络快照。</summary>
        public CharacterMotion CurrentMotion { get; private set; }

        /// <summary>
        /// 应用网络同步的远程运动快照。
        /// </summary>
        public void ReceiveRemoteMotion(CharacterMotionSnapshot snapshot)
        {
            CurrentMotion = new CharacterMotion(snapshot);
        }

        /// <summary>
        /// 绑定视觉实例：动画与输入参考由表现层传入，控制器不查找场景对象。
        /// 同一视觉实例的身份变化会再次调用本方法，只更新输入参考，不重建动画驱动。
        /// </summary>
        public void BindModel(Animator modelAnimator, Transform modelInputSpace)
        {
            // 模型替换必须先解绑：旧驱动仍引用旧 Animator，直接换绑会丢失落地衔接状态。
            if (animationDriver != null && animator != modelAnimator)
            {
                Debug.LogError($"{name} 在未解绑的情况下更换了视觉模型，先调用 UnbindModel。", this);
                return;
            }

            animator = modelAnimator;
            inputSpace = modelInputSpace;

            if (animationDriver == null)
            {
                var driver = new CharacterAnimator(animator);
                if (!driver.Initialize())
                {
                    Debug.LogError($"{name} 的视觉模型缺少可用的 Animator Controller，动画不会更新。", this);
                    return;
                }

                animationDriver = driver;
            }

            characterBound = true;
        }

        /// <summary>
        /// 解除视觉绑定：表现层销毁视觉实例之前调用，之后不再驱动动画或读取输入参考。
        /// </summary>
        public void UnbindModel()
        {
            characterBound = false;
            animationDriver = null;
            animator = null;
            inputSpace = null;
        }

        // 编辑器添加组件时补齐同对象引用；运行时不执行任何组件查找。
        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            // 网络根下的视觉实例不承担运动与输入，其控制器由表现层在激活前停用。
            if (!IsMotionOwner()) return;

            // 专用服务器没有本地输入与相机，不创建运动对象。
            if (IsDedicatedServer) return;

            characterController = characterController != null
                ? characterController
                : GetComponent<CharacterController>();
            if (characterController == null)
            {
                Debug.LogError($"{name} 缺少 CharacterController，角色无法位移。", this);
                enabled = false;
                return;
            }

            networkIdentity = GetComponent<NetworkIdentity>();
            motor = new CharacterMotor(transform, characterController, movementSettings);

            // 单机 Prefab 自带序列化 Animator 与输入参考；联机等待表现层 BindModel。
            if (animator != null)
            {
                BindModel(animator, inputSpace);
            }
        }

        private void Update()
        {
            // 本机是该角色的模拟者，且视觉参考已绑定，才推进模拟与动画。
            if (!characterBound || !CanSimulate()) return;

            CharacterInput frameInput = ReadInput();
            CurrentMotion = motor.Tick(frameInput, inputSpace, Time.deltaTime);
            animationDriver.Apply(CurrentMotion);
        }

        private void LateUpdate()
        {
            // 远程玩家的位置由变换同步驱动，动画只应用收到的运动快照。
            if (!characterBound || !IsRemoteNetworkPlayer()) return;
            animationDriver.Apply(CurrentMotion);
        }

        private void OnDestroy()
        {
            // 生成的包装类在终结时会断言动作表仍然启用，必须显式释放输入资源。
            input.Dispose();
        }

        /// <summary>
        /// 本组件是否承担运动：网络根与单机角色承担，挂在网络根下的视觉实例不承担。
        /// </summary>
        private bool IsMotionOwner()
        {
            if (GetComponent<NetworkIdentity>() != null)
            {
                return true;
            }

            return GetComponentInParent<NetworkIdentity>(true) == null;
        }

        // 专用服务器只转发网络状态，不执行输入、运动和动画驱动。
        private static bool IsDedicatedServer => NetworkServer.active && !NetworkClient.active;

        /// <summary>
        /// 本机是否该模拟这个角色：单机始终模拟；联机只有本地拥有者模拟。
        /// </summary>
        private bool CanSimulate()
        {
            if (IsDedicatedServer)
            {
                return false;
            }

            if (!NetworkClient.isConnected)
            {
                return true;
            }

            return networkIdentity != null && networkIdentity.isLocalPlayer;
        }

        // 联机时非本地拥有者的角色，其位置与动画都由网络数据驱动。
        private bool IsRemoteNetworkPlayer()
        {
            return NetworkClient.isConnected
                   && networkIdentity != null
                   && !networkIdentity.isLocalPlayer;
        }

        /// <summary>
        /// 从 Input System 读取一帧输入，并在进入运动层前完成归一化。
        /// </summary>
        private CharacterInput ReadInput()
        {
            // 移动长度已在 CharacterInputReader 中截断到单位圆内，斜向不会超速。
            Vector2 move = input.ReadMove();
            return new CharacterInput(move, input.SprintHeld, input.JumpPressed);
        }
    }
}
