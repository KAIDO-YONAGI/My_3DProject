using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Baking;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.Scene.VoxelPlatform.Runtime;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform
{
    /// <summary>
    /// 平台的生命周期和局部方块状态入口。世界爆炸由 VoxelPlatformDestruction 统一结算。
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
        // 运行资源按需创建；破坏服务在同一程序集内直接使用该运行状态，不经过转发 API。
        /// <summary>按需创建并返回此平台唯一的运行状态对象。</summary>
        internal VoxelPlatformRuntime Runtime => runtime ??= new VoxelPlatformRuntime(this);

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

        /// <summary>登记活动平台，并在适用时加载烘焙表面。</summary>
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

        /// <summary>按固定间隔检查源方块变化并刷新运行状态。</summary>
        private void LateUpdate()
        {
            // 编辑模式的源 Cube 由 OnEnable 直接显示，改布局时用菜单重建这个组件。
            if (!Application.isPlaying || Time.unscaledTime < nextRefresh) return;
            // 用固定间隔轮询代替每帧全量扫描；手工改动的速度远低于 20Hz。
            nextRefresh = Time.unscaledTime + 0.05f;
            RefreshChangedBlocks();
        }

        /// <summary>注销活动平台并清理生成的运行资源。</summary>
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
        /// <summary>在编辑器中把当前源布局烘焙为表面和分块碰撞网格。</summary>
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

        /// <summary>返回是否找到方块，重复设置相同状态也返回成功。</summary>
        public bool SetBlockActive(Vector3 localPosition, bool active)
        {
            // 破坏与恢复共用这一处，保证两种操作的返回值和副作用保持一致。
            return Runtime.SetBlockActive(localPosition, active);
        }

        /// <summary>同步源方块的启停与移动，并局部刷新平台外观和碰撞。</summary>
        public void RefreshChangedBlocks()
        {
            Runtime.RefreshChangedBlocks();
        }

        /// <summary>创建归属此平台且初始关闭的运行对象根节点。</summary>
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

        /// <summary>按播放状态选择延迟销毁或立即销毁 Unity 运行对象。</summary>
        internal static void Release(Object value)
        {
            if (!value) return;
            // 编辑模式的 Destroy 要等到帧末且对非播放对象无效，只能用 DestroyImmediate。
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
