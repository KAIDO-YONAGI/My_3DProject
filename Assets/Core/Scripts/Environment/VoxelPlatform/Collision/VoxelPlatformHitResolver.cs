using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment
{
    // 合并碰撞不含每块的 GameObject，通过局部空间包围盒反查命中的启用方块。
    internal static class VoxelPlatformHitResolver
    {
        internal static bool TryGetBlockPosition(Transform platform, Transform collisionRoot,
            List<VoxelPlatformBlock> blocks, RaycastHit hit, out Vector3 localPosition)
        {
            localPosition = default;
            if (!collisionRoot || !hit.collider ||
                !hit.collider.transform.IsChildOf(collisionRoot))
                return false;

            Vector3 platformPoint = platform.InverseTransformPoint(hit.point);
            VoxelPlatformBlock result = null;
            float closestDistance = float.MaxValue;
            foreach (VoxelPlatformBlock block in blocks)
            {
                if (!block.Active || !block.Mesh) continue;

                // 先撤销平台变换，再撤销方块变换，兼容平台旋转和 Cube 自身偏移。
                Vector3 meshPoint = block.Matrix.inverse.MultiplyPoint3x4(platformPoint);
                Bounds bounds = block.Mesh.bounds;
                Vector3 closestPoint = new Vector3(
                    Mathf.Clamp(meshPoint.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(meshPoint.y, bounds.min.y, bounds.max.y),
                    Mathf.Clamp(meshPoint.z, bounds.min.z, bounds.max.z));
                float distance = (meshPoint - closestPoint).sqrMagnitude;
                if (distance > 0.000001f || distance >= closestDistance) continue;
                closestDistance = distance;
                result = block;
            }

            if (result == null) return false;
            localPosition = (Vector3)result.Position / VoxelPlatformGeometry.PositionPrecision;
            return true;
        }
    }
}
