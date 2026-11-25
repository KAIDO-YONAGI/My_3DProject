using Core.Scripts.Character.Input;
using Core.Scripts.Common;
using Core.Scripts.Networking.Sync;
using Core.Scripts.SO;
using Core.Scripts.VoxelPlatform.Destruction;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Character.Interactions
{
    /// <summary>
    /// 准心交互入口：读取攻击输入，沿相机朝向提交射线破坏并返回命中结果。
    /// 光标锁定时鼠标坐标恒定在屏幕中心，射线方向因此取相机朝向。
    /// </summary>
    [DisallowMultipleComponent]
    public class Attack : MonoBehaviour
    {
        [Header("绑定")]
        [Tooltip("攻击射线起点与朝向，由 GameplayModule 绑定到本地相机；留空时使用自身 Transform。")]
        [SerializeField]
        private Transform aimSource;

        [Tooltip("玩家根节点，由 GameplayModule 绑定；留空时取所在层级的根节点。")] [SerializeField]
        private Transform playerRoot;

        [Header("检测")] [Tooltip("从角色位置起算的可破坏距离，超出后不再命中方块。")] [SerializeField, Min(0.1f)]
        private float reachDistance = 5f;

        [Tooltip("参与破坏检测的层，未勾选的层不会被射线命中。")] [SerializeField]
        private LayerMask hitLayers = ~0;

        [Header("破坏")] [Tooltip("命中位置的水平破坏半径，单位为世界单位。")] [SerializeField, Min(0f)]
        private float destructionRadius = 0.45f;

        [SerializeField] private DestructionEventSO destructionEventSO;

        [Header("事件")] [Tooltip("开局事件：由 VoxelPlatformDestructionTrigger 广播，事件不带载荷，收到后是否拒绝由下方开关决定。")] [SerializeField]
        private VoidEventSO gameStartEventSO;

        [SerializeField] private VoidEventSO retryEventSO;

        private readonly CharacterInputReader input = new();

        // 开局事件到达后置位的拒绝标志：置位期间不读攻击输入，也不发射线。
        private bool attackRejected = true;

        // 网络角色根上的身份组件：远程角色也带相机与攻击组件，需要据此跳过本地输入。
        private NetworkIdentity characterIdentity;

        // 以 Mirror 本地玩家身份为键缓存网络组件，玩家切换时再重新查找。
        private NetworkIdentity cachedLocalPlayer;
        private NetworkDestructionManager networkDestructionManager;

        /// <summary>检查事件引用并解析射线参考，用于计算射线距离和排除自身。</summary>
        private void Awake()
        {
            ValidateReferences();
            if (playerRoot == null) playerRoot = transform.root;
            if (aimSource == null) aimSource = transform;
            characterIdentity = playerRoot.GetComponent<NetworkIdentity>();
        }

        /// <summary>由 GameplayModule 在组装完成后绑定玩家根与射线参考。</summary>
        /// <param name="targetPlayerRoot">玩家根节点，非空时覆盖序列化值。</param>
        /// <param name="targetAimSource">攻击射线参考，非空时覆盖序列化值。</param>
        public void Bind(Transform targetPlayerRoot, Transform targetAimSource)
        {
            if (targetPlayerRoot != null) playerRoot = targetPlayerRoot;
            if (targetAimSource != null) aimSource = targetAimSource;
            characterIdentity = playerRoot != null ? playerRoot.GetComponent<NetworkIdentity>() : null;
        }

        // 必填引用只校验一次：缺失时按字段名断言，订阅点直接解引用。
        /// <summary>断言游戏开始、攻击表现与重试事件引用已配置。</summary>
        private void ValidateReferences()
        {
            Debug.Assert(gameStartEventSO != null,
                $"{nameof(gameStartEventSO)} 未指定：攻击会一直处于拒绝状态。", this);
            Debug.Assert(destructionEventSO != null,
                $"{nameof(destructionEventSO)} 未指定：本地攻击表现事件不会发布。", this);
            Debug.Assert(retryEventSO != null,
                $"{nameof(retryEventSO)} 未指定：重试后不会恢复拒绝状态。", this);
        }

        /// <summary>启用时订阅攻击状态所需的开始与重试事件。</summary>
        private void OnEnable()
        {
            gameStartEventSO.VoidEvent += OnGameStart;
            retryEventSO.VoidEvent += OnGameRetry;
        }

        /// <summary>停用时解除事件订阅并释放输入资源。</summary>
        private void OnDisable()
        {
            gameStartEventSO.VoidEvent -= OnGameStart;
            retryEventSO.VoidEvent -= OnGameRetry;

            // 输入对象持有 Input System 资源，组件停用时释放，避免重复启用后产生多份输入状态。
            input.Dispose();
        }

        /// <summary>收到开局事件后允许玩家攻击。</summary>
        private void OnGameStart()
        {
            attackRejected = false;
        }
        /// <summary>接收重试事件并暂时拒绝攻击。</summary>
        private void OnGameRetry()
        {
            attackRejected = true;
        }
        /// <summary>检查攻击状态、输入和光标归属，满足条件时执行一次准心破坏。</summary>
        private void Update()
        {
            // 远程角色的相机与攻击组件同样处于启用状态，但它们共享本机输入设备。
            if (!IsLocalCharacter())
            {
                return;
            }
            // 拒绝标志置位时整段攻击逻辑直接返回。
            if (attackRejected)
                return;
            if (!input.AttackPressed)
                return;
            // 光标未锁定表示正在操作 UI，此时左键属于界面交互，不触发破坏。
            if (Cursor.lockState != CursorLockMode.Locked)
                return;
            //本地构造射线并提交 返回的结果提供给RPC
            AttackResult result = AttackFromCrosshair();
            if (!result.HitPoint.HasValue || !result.BlockCenter.HasValue || result.PlatformId <= 0)
                return;

            // 真实命中点用于表现，命中方块中心用于结算，远端按同一份数据复现。
            AttackRequestPack request = new AttackRequestPack(
                AttackType.Explosion, result.PlatformLocalPosition, result.PlatformLocalBlockCenter,
                1, result.Radius, result.PlatformId);
            SubmitNetworkAttack(request);

            // 本地表现事件放在攻击回路末尾，供特效和音效订阅。
            destructionEventSO.OnEventRaised(request);
        }
 
        // 本地角色指本机控制的那一个：单机模式全部算本地，联机只认本机拥有的网络根。
        private bool IsLocalCharacter()
        {
            return !NetworkClient.isConnected
                   || (characterIdentity != null && characterIdentity.isLocalPlayer);
        }

        /// <summary>按本地玩家身份刷新网络组件缓存，并在联机 Ready 后提交攻击命令。</summary>
        private void SubmitNetworkAttack(AttackRequestPack request)
        {
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (!NetworkClient.isConnected || localPlayer == null)
            {
                cachedLocalPlayer = null;
                networkDestructionManager = null;
            }
            else
            {
                // 玩家身份变化时才查找组件；保持与音频请求路径相同的缓存策略。
                if (cachedLocalPlayer != localPlayer)
                {
                    cachedLocalPlayer = localPlayer;
                    networkDestructionManager = localPlayer.GetComponentInChildren<NetworkDestructionManager>(true);
                }

                if (networkDestructionManager != null && networkDestructionManager.isLocalPlayer && NetworkClient.ready)
                {
                    networkDestructionManager.CommandAttack(request);
                }
            }
        }

        /// <summary>从准心提交射线破坏，并把真实命中点、命中方块中心和破坏数量返回给调用方。</summary>
        public AttackResult AttackFromCrosshair()
        {
            Ray ray = new Ray(aimSource.position, aimSource.forward);
            return VoxelPlatformDestruction.RequiredInstance.DestroyFromRay(
                ray, ResolveMaxDistance(), hitLayers.value, playerRoot, destructionRadius);
        }

        // 相机位于角色身后，检测距离从角色位置起算，reachDistance 才是玩家感知到的攻击范围。
        /// <summary>补回相机位于角色身后的距离，使可感知攻击距离从角色位置起算。</summary>
        private float ResolveMaxDistance()
        {
            float originDistance = playerRoot != null
                ? Vector3.Distance(aimSource.position, playerRoot.position)
                : 0f;
            return originDistance + reachDistance;
        }

    }
}
