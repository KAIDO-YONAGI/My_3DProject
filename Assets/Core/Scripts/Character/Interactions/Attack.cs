using Core.Scripts.Character.Input;
using Core.Scripts.SO;
using Core.Scripts.Scene.VoxelPlatform;
using UnityEngine;

namespace Core.Scripts.Character.Interactions
{
    /// <summary>
    /// 准心交互入口：读取攻击输入，沿相机朝向发射射线，命中方块平台后破坏对应方块。
    /// 光标锁定时鼠标坐标恒定在屏幕中心，射线方向因此取相机朝向。
    /// </summary>
    [DisallowMultipleComponent]
    public class Attack : MonoBehaviour
    {
        // 命中缓冲复用同一份数组，按下攻击不产生垃圾；填满时扩容重试，不退回分配版本。
        private const int HitCapacity = 16;
        private const int MaxHitCapacity = 64;

        [Header("检测")] [Tooltip("从角色位置起算的可破坏距离，超出后不再命中方块。")] [SerializeField, Min(0.1f)]
        private float reachDistance = 5f;

        [Tooltip("参与破坏检测的层，未勾选的层不会被射线命中。")] [SerializeField]
        private LayerMask hitLayers = ~0;

        [Header("事件")] [Tooltip("开局事件：由 VoxelPlatformBlastTrigger 广播，事件不带载荷，收到后是否拒绝由下方开关决定。")] [SerializeField]
        private VoidEventSO gameStartEventSO;

        [SerializeField] private VoidEventSO retryEventSO;

        private readonly CharacterInputReader input = new();

        // 开局事件到达后置位的拒绝标志：置位期间不读攻击输入，也不发射线。
        private bool attackRejected = true;

        private RaycastHit[] hits = new RaycastHit[HitCapacity];

        // 相机挂在角色 Prefab 内，根节点即角色边界，用来排除射线穿过的自身碰撞体。
        private Transform playerRoot;

        private void Awake()
        {
            playerRoot = transform.root;
        }

        private void OnEnable()
        {
            gameStartEventSO.VoidEvent += OnGameStart;
            retryEventSO.VoidEvent += OnGameRetry;
        }

        private void OnDisable()
        {
            gameStartEventSO.VoidEvent -= OnGameStart;
            retryEventSO.VoidEvent -= OnGameRetry;

            // 输入对象持有 Input System 资源，组件停用时释放，避免重复启用后产生多份输入状态。
            input.Dispose();
        }

        /// <summary>
        /// 开局事件回调：按开关置位拒绝标志，订阅关系在 OnEnable/OnDisable 内建立与解除。
        /// </summary>
        private void OnGameStart()
        {
            attackRejected = false;
        }
        private void OnGameRetry()
        {
            attackRejected = true;
        }
        private void Update()
        {
            // 拒绝标志置位时整段攻击逻辑直接返回。
            if (attackRejected)
            {
                return;
            }

            if (!input.AttackPressed)
            {
                return;
            }

            // 光标未锁定表示正在操作 UI，此时左键属于界面交互，不触发破坏。
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            BreakBlockUnderCrosshair();
        }

        private void BreakBlockUnderCrosshair()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            float maxDistance = ResolveMaxDistance();
            // 碎块层由平台模块定义，破坏检测排除这一层，废墟里瞄准不会被碎块挡住。
            int queryLayers = hitLayers.value & ~VoxelPlatformRenderer.DebrisMask;
            int count = Physics.RaycastNonAlloc(
                ray, hits, maxDistance, queryLayers, QueryTriggerInteraction.Ignore);

            // 缓冲区被填满时结果可能缺少最近命中，扩容后重试；到上限仍填满时按现有结果处理。
            while (count == hits.Length && hits.Length < MaxHitCapacity)
            {
                hits = new RaycastHit[hits.Length * 2];
                count = Physics.RaycastNonAlloc(
                    ray, hits, maxDistance, queryLayers, QueryTriggerInteraction.Ignore);
            }

            if (!TryPickNearest(count, out RaycastHit nearest))
            {
                // Log($"未命中可破坏目标：射线命中 {count} 个碰撞体，全部属于角色自身或为空");
                return;
            }

            // 平台把方块合成一份碰撞，命中后由平台按命中点反查具体方块。
            VoxelPlatformRenderer platform = nearest.collider.GetComponentInParent<VoxelPlatformRenderer>();
            if (platform == null)
            {
                return;
            }

            bool resolved = platform.TryGetBlockPosition(nearest, out Vector3 localPosition);
            bool broken = resolved && platform.BreakBlock(localPosition);
        }

        // 相机位于角色身后，检测距离从角色位置起算，reachDistance 才是玩家感知到的攻击范围。
        private float ResolveMaxDistance()
        {
            float originDistance = playerRoot != null
                ? Vector3.Distance(transform.position, playerRoot.position)
                : 0f;
            return originDistance + reachDistance;
        }

        // NonAlloc 的结果未排序，只在前 count 个元素里取最近的非自身碰撞体。
        private bool TryPickNearest(int count, out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || IsPlayerCollider(hit.collider.transform) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearest = hit;
                found = true;
            }

            return found;
        }

        // 射线从相机出发，穿过角色自身后才到达场景，整条角色层级都不参与破坏检测。
        private bool IsPlayerCollider(Transform hitTransform)
        {
            return playerRoot != null && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot));
        }
    }
}