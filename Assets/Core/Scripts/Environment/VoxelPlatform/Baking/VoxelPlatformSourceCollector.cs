using System.Collections.Generic;
using Core.Scripts.Environment.VoxelPlatform.Baking;
using Core.Scripts.Environment.VoxelPlatform.Data;
using Core.Scripts.Environment.VoxelPlatform.Fragments;
using Core.Scripts.Environment.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.Environment
{
    // 从编辑布局提取 Intact，保留源状态以便退出预览时恢复。
    internal static class VoxelPlatformSourceCollector
    {
        internal static bool HasSourceGeometry(VoxelPlatformRenderer owner)
        {
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
                Vector3 center = matrix.MultiplyPoint3x4(filter.sharedMesh.bounds.center);
                var block = new VoxelPlatformBlock
                {
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
                    OriginalForceOff = renderer.forceRenderingOff,
                    Layer = filter.gameObject.layer,
                    ShadowCastingMode = renderer.shadowCastingMode,
                    ReceiveShadows = renderer.receiveShadows,
                    LightProbeUsage = renderer.lightProbeUsage,
                    ReflectionProbeUsage = renderer.reflectionProbeUsage,
                    RenderingLayerMask = renderer.renderingLayerMask,
                    AllowOcclusionWhenDynamic = renderer.allowOcclusionWhenDynamic
                };
                block.Active = IsActive(block);
                blocks.Add(block);
                renderer.forceRenderingOff = true;
                block.Transform.hasChanged = false;
            }
        }

        internal static bool IsActive(VoxelPlatformBlock block)
        {
            return block.Object && block.Renderer && block.Object.activeInHierarchy &&
                block.Renderer.enabled && !block.OriginalForceOff;
        }
    }
}
