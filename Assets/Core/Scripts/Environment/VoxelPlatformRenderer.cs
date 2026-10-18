using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment
{
    /// <summary>
    /// 平台的生命周期和调用入口。方块接口使用平台局部空间中的网格中心坐标。
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class VoxelPlatformRenderer : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform surfaceRoot;
        [SerializeField, HideInInspector] private Transform collisionRoot;
        [SerializeField, HideInInspector] private Transform fragmentRoot;
        [SerializeField, HideInInspector] private List<VoxelPlatformBakedBlock> bakedBlocks =
            new List<VoxelPlatformBakedBlock>();
        [SerializeField, HideInInspector] private List<VoxelPlatformBakedChunk> bakedChunks =
            new List<VoxelPlatformBakedChunk>();

        private VoxelPlatformRuntime runtime;
        private float nextRefresh;
        private VoxelPlatformRuntime Runtime => runtime ?? (runtime = new VoxelPlatformRuntime(this));

        internal Transform SurfaceRoot { get => surfaceRoot; set => surfaceRoot = value; }
        internal Transform CollisionRoot { get => collisionRoot; set => collisionRoot = value; }
        internal Transform FragmentRoot { get => fragmentRoot; set => fragmentRoot = value; }
        internal List<VoxelPlatformBakedBlock> BakedBlocks => bakedBlocks;
        internal List<VoxelPlatformBakedChunk> BakedChunks => bakedChunks;

        public int RenderedTriangleCount => runtime?.Meshes.RenderedTriangleCount ?? 0;
        public int SurfaceRendererCount => runtime?.Meshes.SurfaceRendererCount ?? 0;
        public int CollisionColliderCount => runtime?.Meshes.CollisionColliderCount ?? 0;
        public bool HasBakedSurface => surfaceRoot && bakedBlocks.Count > 0 && bakedChunks.Count > 0;
        public bool HasBakeSources => VoxelPlatformSourceCollector.HasSourceGeometry(this);

        private void OnEnable()
        {
#if UNITY_EDITOR
            // 编辑用平台保留源 Cube，避免启用组件时用烘焙结果覆盖编辑视图。
            if (!Application.isPlaying && HasBakeSources) return;
#endif
            if (HasBakedSurface) UseBakedSurface();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.05f;
            RefreshChangedBlocks();
        }

        private void OnDisable()
        {
            Runtime.Clear();
        }

        /// <summary>清理当前变化并加载烘焙布局，完整表面和碰撞直接复用资产。</summary>
        public void UseBakedSurface()
        {
            Runtime.UseBakedSurface();
            nextRefresh = Time.unscaledTime + 0.05f;
        }

#if UNITY_EDITOR
        public Mesh[] Bake()
        {
            return VoxelPlatformBaking.Bake(this, Runtime);
        }
#endif

        /// <summary>源布局重新收集几何；运行资源重新加载烘焙状态。</summary>
        [ContextMenu("Rebuild Platform Surface")]
        public void Rebuild()
        {
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

        /// <summary>从本平台合并碰撞的命中取得局部方块中心，供破坏与恢复共用。</summary>
        public bool TryGetBlockPosition(RaycastHit hit, out Vector3 localPosition)
        {
            return VoxelPlatformHitResolver.TryGetBlockPosition(transform, collisionRoot,
                Runtime.Blocks, hit, out localPosition);
        }

        public bool BreakBlockAtHit(RaycastHit hit)
        {
            return TryGetBlockPosition(hit, out Vector3 localPosition) && BreakBlock(localPosition);
        }

        /// <summary>返回是否找到方块，重复设置相同状态也返回成功。</summary>
        public bool SetBlockActive(Vector3 localPosition, bool active)
        {
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
            group.SetActive(false);
            return group.transform;
        }

        internal static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
