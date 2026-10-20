using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Fragments;
using Core.Scripts.Scene.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Baking
{
    // 从编辑布局提取 Intact，保留源状态以便退出预览时恢复。
    static class VoxelPlatformSourceCollector
    {
        internal static bool HasSourceGeometry(VoxelPlatformRenderer owner)
        {
            // 只有保留源 Cube 的编辑布局才为真；运行资源查不到 Intact 会走烘焙分支。
            foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>(true))
                if (IsBakeSource(filter)) return true;
            return false;
        }

        private static bool IsBakeSource(MeshFilter filter)
        {
            // 独立编辑区使用自己的 Renderer 和 Collider，外围合批不接管它的状态。
            return filter.name == "Intact" &&
                !filter.GetComponentInParent<VoxelPlatformBakeExclusion>(true);
        }

        internal static void Collect(VoxelPlatformRenderer owner, List<VoxelPlatformBlock> blocks)
        {
            Matrix4x4 worldToLocal = owner.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>(true))
            {
                // 生成剔除变体要读取顶点与三角形，不可读的源网格直接跳过。
                if (!IsBakeSource(filter) || !filter.sharedMesh || !filter.sharedMesh.isReadable)
                    continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                // 当前合并约定是一块一个材质，烘焙光照对象交由原有渲染流程处理。
                if (!renderer || filter.sharedMesh.subMeshCount != 1 ||
                    renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial ||
                    renderer.lightmapIndex >= 0)
                    continue;

                Matrix4x4 matrix = worldToLocal * filter.transform.localToWorldMatrix;
                Transform fragmentContainer = VoxelPlatformFragments.FindContainer(filter.transform);
                // 格键取网格包围盒中心，源 Cube 的轴心不在几何中心时也能落进正确的格子。
                Vector3 center = matrix.MultiplyPoint3x4(filter.sharedMesh.bounds.center);
                var block = new VoxelPlatformBlock
                {
                    // 源对象引用只在编辑布局里有效，烘焙时会被丢掉。
                    Renderer = renderer,
                    Transform = filter.transform,
                    Object = filter.gameObject,
                    FragmentPrefab = VoxelPlatformFragments.FindPrefab(filter.transform),
                    Mesh = filter.sharedMesh,
                    Material = renderer.sharedMaterial,
                    Matrix = matrix,
                    // 碎块实例使用 Cube 容器变换，避免再次叠加 Intact 的局部偏移。
                    FragmentMatrix = fragmentContainer
                        ? worldToLocal * fragmentContainer.localToWorldMatrix
                        : matrix,
                    Position = Vector3Int.RoundToInt(center * VoxelPlatformGeometry.PositionPrecision),
                    Chunk = new Vector3Int(Mathf.FloorToInt(center.x / VoxelPlatformGeometry.ChunkSize),
                        Mathf.FloorToInt(center.y / VoxelPlatformGeometry.ChunkSize),
                        Mathf.FloorToInt(center.z / VoxelPlatformGeometry.ChunkSize)),
                    Occludes = VoxelPlatformGeometry.IsUnitCube(filter.sharedMesh, matrix),
                    // 记下原始开关，退出预览时按原样恢复。
                    OriginalForceOff = renderer.forceRenderingOff,
                    Layer = filter.gameObject.layer,
                    ShadowCastingMode = renderer.shadowCastingMode,
                    ReceiveShadows = renderer.receiveShadows,
                    LightProbeUsage = renderer.lightProbeUsage,
                    ReflectionProbeUsage = renderer.reflectionProbeUsage,
                    RenderingLayerMask = renderer.renderingLayerMask,
                    AllowOcclusionWhenDynamic = renderer.allowOcclusionWhenDynamic
                };
                // 源组件的当前启停就是初始显示状态。
                block.Active = IsActive(block);
                blocks.Add(block);
                // 源 Cube 与合并表面同时绘制会重影，收集后立即关掉源渲染。
                renderer.forceRenderingOff = true;
                // 清掉初始变更标记，避免下一次轮询把首次收集当成方块被移动。
                block.Transform.hasChanged = false;
            }
        }

        // 编辑期以源组件的启停作为唯一真相；原本就关闭的渲染不参与显示。
        internal static bool IsActive(VoxelPlatformBlock block)
        {
            return block.Object && block.Renderer && block.Object.activeInHierarchy &&
                block.Renderer.enabled && !block.OriginalForceOff;
        }
    }
}
