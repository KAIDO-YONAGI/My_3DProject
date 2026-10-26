using Core.Scripts.Character.Input;
using Core.Scripts.Networking.Sync;
using Core.Scripts.SO;
using Core.Scripts.Scene.VoxelPlatform.Destruction;
using UnityEngine;

namespace Core.Scripts.Character.Interactions
{
    /// <summary>
    /// 准心交互入口：读取攻击输入，沿相机朝向提交射线爆炸并返回命中结果。
    /// 光标锁定时鼠标坐标恒定在屏幕中心，射线方向因此取相机朝向。
    /// </summary>
    [DisallowMultipleComponent]
    public class Attack : MonoBehaviour
    {
        [Header("检测")] [Tooltip("从角色位置起算的可破坏距离，超出后不再命中方块。")] [SerializeField, Min(0.1f)]
        private float reachDistance = 5f;

        [Tooltip("参与破坏检测的层，未勾选的层不会被射线命中。")] [SerializeField]
        private LayerMask hitLayers = ~0;

        [Header("爆炸")] [Tooltip("命中位置的水平爆炸半径，单位为世界单位。")] [SerializeField, Min(0f)]
        private float blastRadius = 0.45f;

        [SerializeField] private BlastEventSO blastEventSO;

        [Header("事件")] [Tooltip("开局事件：由 VoxelPlatformBlastTrigger 广播，事件不带载荷，收到后是否拒绝由下方开关决定。")] [SerializeField]
        private VoidEventSO gameStartEventSO;

        [SerializeField] private VoidEventSO retryEventSO;

        private readonly CharacterInputReader input = new();

        // 开局事件到达后置位的拒绝标志：置位期间不读攻击输入，也不发射线。
        private bool attackRejected = true;

        // 相机挂在角色 Prefab 内，根节点即角色边界，用来排除射线穿过的自身碰撞体。
        private Transform playerRoot;

        /// <summary>检查事件引用并记录角色根节点，用于计算射线距离和排除自身。</summary>
        private void Awake()
        {
            ValidateReferences();
            playerRoot = transform.root;
        }

        // 必填引用只校验一次：缺失时按字段名报错，订阅点直接解引用。
        /// <summary>报告缺失的游戏开始或重试事件引用。</summary>
        private void ValidateReferences()
        {
            if (gameStartEventSO == null)
            {
                Debug.LogError($"[{nameof(Attack)}] gameStartEventSO 未指定：攻击会一直处于拒绝状态。", this);
            }

            if (blastEventSO == null)
            {
                Debug.LogError($"[{nameof(Attack)}] blastEventSO 未指定：攻击爆炸不会同步给其他客户端。", this);
            }

            if (retryEventSO == null)
            {
                Debug.LogError($"[{nameof(Attack)}] retryEventSO 未指定：重试后不会恢复拒绝状态。", this);
            }
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
        /// <summary>检查攻击状态、输入和光标归属，满足条件时执行一次准心爆炸。</summary>
        private void Update()
        {
            // 拒绝标志置位时整段攻击逻辑直接返回。
            if (attackRejected)
                return;
            if (!input.AttackPressed)
                return;
            // 光标未锁定表示正在操作 UI，此时左键属于界面交互，不触发破坏。
            if (Cursor.lockState != CursorLockMode.Locked)
                return;
            AttackResult result = AttackFromCrosshair();
            if (!result.ExplosionCenter.HasValue) return;
            blastEventSO.OnEventRaised(new BlastMessage(
                playerRoot.gameObject,
                new AttackRequestPack(result.ExplosionCenter.Value, 1, result.Radius)));
        }

        /// <summary>从准心提交射线爆炸，并把爆炸中心、半径和破坏数量返回给调用方。</summary>
        public AttackResult AttackFromCrosshair()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            return VoxelPlatformDestruction.ExplodeFromRay(
                ray, ResolveMaxDistance(), hitLayers.value, playerRoot, blastRadius);
        }

        // 相机位于角色身后，检测距离从角色位置起算，reachDistance 才是玩家感知到的攻击范围。
        /// <summary>补回相机位于角色身后的距离，使可感知攻击距离从角色位置起算。</summary>
        private float ResolveMaxDistance()
        {
            float originDistance = playerRoot != null
                ? Vector3.Distance(transform.position, playerRoot.position)
                : 0f;
            return originDistance + reachDistance;
        }

    }
}
