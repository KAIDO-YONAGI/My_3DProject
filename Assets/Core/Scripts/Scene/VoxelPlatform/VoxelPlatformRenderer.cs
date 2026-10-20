using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Baking;
using Core.Scripts.Scene.VoxelPlatform.Collision;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.Scene.VoxelPlatform.Runtime;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform
{
    /// <summary>
    /// 平台的生命周期和调用入口。方块接口使用平台局部空间中的网格中心坐标，
    /// 破坏、恢复与命中反查成员公开给游戏逻辑调用。
    /// </summary>
    // ExecuteAlways 让编辑视图同样维护合并表面，界面上的启停与破坏操作直接作用在运行组件上。
    [ExecuteAlways, DisallowMultipleComponent]
    public class VoxelPlatformRenderer : MonoBehaviour
    {
        // 碎块实例统一使用的专属层，破坏检测按层整体排除这些碰撞体。
        public const string DebrisLayerName = "Debris";

        // MonoBehaviour 的静态字段初始化发生在反序列化期间，那里不能调用 Unity API，
        // 所以静态构造只写常量，层索引推迟到第一次访问时解析。
        private static int debrisLayer = -1;

        /// <summary>碎块层索引，首次访问时解析；工程里没有该层时为 -1。</summary>
        public static int DebrisLayer
        {
            get
            {
                if (debrisLayer < 0) debrisLayer = LayerMask.NameToLayer(DebrisLayerName);
                return debrisLayer;
            }
        }

        /// <summary>碎块层的位掩码，层不存在时为零。</summary>
        public static int DebrisMask => DebrisLayer >= 0 ? 1 << DebrisLayer : 0;

        // 三个根节点由平台自己创建和维护；序列化是为了烘焙后仍能直接引用同一批对象。
        [SerializeField, HideInInspector] private Transform surfaceRoot;
        [SerializeField, HideInInspector] private Transform collisionRoot;
        [SerializeField, HideInInspector] private Transform fragmentRoot;
        // 烘焙结果内联序列化在组件上，平台不依赖外部资产引用就能独立加载。
        [SerializeField, HideInInspector] private List<VoxelPlatformBakedBlock> bakedBlocks = new ();
        [SerializeField, HideInInspector] private List<VoxelPlatformBakedChunk> bakedChunks = new ();

        private VoxelPlatformRuntime runtime;
        private float nextRefresh;
        // 运行资源按需创建，从未启用的平台不承担构造开销。
        private VoxelPlatformRuntime Runtime => runtime ??= new VoxelPlatformRuntime(this);

        internal Transform SurfaceRoot { get => surfaceRoot; set => surfaceRoot = value; }
        internal Transform CollisionRoot { get => collisionRoot; set => collisionRoot = value; }
        internal Transform FragmentRoot { get => fragmentRoot; set => fragmentRoot = value; }
        internal List<VoxelPlatformBakedBlock> BakedBlocks => bakedBlocks;
        internal List<VoxelPlatformBakedChunk> BakedChunks => bakedChunks;

        // 统计量取自当前运行资源，平台未创建时不参与统计。
        public int RenderedTriangleCount => runtime?.Meshes.RenderedTriangleCount ?? 0;
        public int SurfaceRendererCount => runtime?.Meshes.SurfaceRendererCount ?? 0;
        public int CollisionColliderCount => runtime?.Meshes.CollisionColliderCount ?? 0;
        // 三样缺任一项都不能算完整烘焙数据，此时只能退回从源几何重建。
        public bool HasBakedSurface => surfaceRoot && bakedBlocks.Count > 0 && bakedChunks.Count > 0;
        public bool HasBakeSources => VoxelPlatformSourceCollector.HasSourceGeometry(this);

        private void OnEnable()
        {
            // 平台在启用时登记到破坏服务，批量结算按登记表遍历，不必扫描场景。
            VoxelPlatformDestruction.Register(this);
#if UNITY_EDITOR
            // 编辑用平台保留源 Cube，避免启用组件时用烘焙结果覆盖编辑视图。
            if (!Application.isPlaying && HasBakeSources) return;
#endif
            // 有烘焙表面就直接复用，运行时不必重新收集源几何。
            if (HasBakedSurface) UseBakedSurface();
        }

        private void LateUpdate()
        {
            // 编辑模式的源 Cube 由 OnEnable 直接显示，改布局时用菜单重建这个组件。
            if (!Application.isPlaying || Time.unscaledTime < nextRefresh) return;
            // 用固定间隔轮询代替每帧全量扫描；手工改动的速度远低于 20Hz。
            nextRefresh = Time.unscaledTime + 0.05f;
            RefreshChangedBlocks();
        }

        private void OnDisable()
        {
            VoxelPlatformDestruction.Unregister(this);
            // 禁用后运行表面留在场景里会继续参与渲染和碰撞，必须一并清掉。
            Runtime.Clear();
        }

        /// <summary>清理当前变化并加载烘焙布局，完整表面和碰撞直接复用资产。</summary>
        public void UseBakedSurface()
        {
            Runtime.UseBakedSurface();
            // 重置轮询计时，避免加载完成后立刻又跑一次全量检查。
            nextRefresh = Time.unscaledTime + 0.05f;
        }

#if UNITY_EDITOR
        public Mesh[] Bake()
        {
            // 只在编辑器暴露：烘焙要写资产和序列化字段，运行时代码不参与。
            return VoxelPlatformBaking.Bake(this, Runtime);
        }
#endif

        /// <summary>源布局重新收集几何；运行资源重新加载烘焙状态。</summary>
        [ContextMenu("Rebuild Platform Surface")]
        public void Rebuild()
        {
            // 同一个入口服务两种来源：编辑布局从源几何重收集，运行资源则重新加载烘焙状态。
            Runtime.Rebuild();
        }

        /// <summary>破坏局部中心对应的方块，创建碎块并同步局部表面与碰撞。</summary>
        public bool BreakBlock(Vector3 localPosition)
        {
            return SetBlockActive(localPosition, false);
        }

        /// <summary>恢复局部中心对应的方块，同时清理该方块的运行碎块实例。</summary>
        public bool RestoreBlock(Vector3 localPosition)
        {
            return SetBlockActive(localPosition, true);
        }

        /// <summary>
        /// 按平台局部方块中心批量破坏，受影响分块只重建一次。
        /// collisionDuration 为碎块从生成起保持碰撞的时长，省略时沿用 Prefab 上的配置。
        /// </summary>
        public int BreakBlocks(IReadOnlyList<Vector3> localCenters, float? collisionDuration = null)
        {
            return Runtime.BreakBlocks(localCenters, collisionDuration);
        }

        /// <summary>
        /// 破坏与这些世界空间体积相交的方块，受影响分块只重建一次。
        /// collisionDuration 为碎块从生成起保持碰撞的时长，省略时沿用 Prefab 上的配置。
        /// </summary>
        public int BreakBlocksIn(IReadOnlyList<VoxelPlatformBlast> blasts, float? collisionDuration = null)
        {
            return Runtime.BreakBlocksIn(blasts, transform.localToWorldMatrix, collisionDuration);
        }

        /// <summary>从本平台合并碰撞的命中取得局部方块中心，供破坏与恢复共用。</summary>
        public bool TryGetBlockPosition(RaycastHit hit, out Vector3 localPosition)
        {
            return VoxelPlatformHitResolver.TryGetBlockPosition(transform, collisionRoot,
                Runtime.Blocks, hit, out localPosition);
        }

        public bool BreakBlockAtHit(RaycastHit hit)
        {
            // 反查失败说明点到别的碰撞体，或落点没有对应方块，直接返回 false。
            return TryGetBlockPosition(hit, out Vector3 localPosition) && BreakBlock(localPosition);
        }

        /// <summary>返回是否找到方块，重复设置相同状态也返回成功。</summary>
        public bool SetBlockActive(Vector3 localPosition, bool active)
        {
            // 破坏与恢复共用这一处，保证两种操作的返回值和副作用保持一致。
            return Runtime.SetBlockActive(localPosition, active);
        }

        // 破坏逻辑切换 Intact 后可立即调用；定时检查也兼容 Inspector 中的启停和移动。
        public void RefreshChangedBlocks()
        {
            Runtime.RefreshChangedBlocks();
        }

        internal Transform CreateRoot(string name)
        {
            // 运行生成物由平台管理；烘焙时再将需要持久化的对象改为可保存。
            var group = new GameObject(name);
            group.transform.SetParent(transform, false);
            group.hideFlags = HideFlags.DontSave;
            // 先保持关闭，等内部对象挂接完毕再整体启用，中途不出现空容器。
            group.SetActive(false);
            return group.transform;
        }

        internal static void Release(Object value)
        {
            if (!value) return;
            // 编辑模式的 Destroy 要等到帧末且对非播放对象无效，只能用 DestroyImmediate。
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
