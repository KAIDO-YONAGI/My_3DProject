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

        private void OnEnable()
        {
#if UNITY_EDITOR
            // 编辑用平台保留源 Cube，避免启用组件时用烘焙结果覆盖编辑视图。
            if (!Application.isPlaying && VoxelPlatformSourceCollector.HasSourceGeometry(this)) return;
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

        [ContextMenu("Rebuild Platform Surface")]
        public void Rebuild()
        {
            Runtime.Rebuild();
        }

        public bool BreakBlock(Vector3 localPosition)
        {
            return SetBlockActive(localPosition, false);
        }

        public bool RestoreBlock(Vector3 localPosition)
        {
            return SetBlockActive(localPosition, true);
        }

        public bool TryGetBlockPosition(RaycastHit hit, out Vector3 localPosition)
        {
            return VoxelPlatformHitResolver.TryGetBlockPosition(transform, collisionRoot,
                Runtime.Blocks, hit, out localPosition);
        }

        public bool BreakBlockAtHit(RaycastHit hit)
        {
            return TryGetBlockPosition(hit, out Vector3 localPosition) && BreakBlock(localPosition);
        }

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
