using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Collision
{
    // 合并碰撞不含每块的 GameObject，通过局部空间包围盒反查命中的启用方块。
    static class VoxelPlatformHitResolver
    {
        internal static bool TryGetBlockPosition(Transform platform, Transform collisionRoot,
            List<VoxelPlatformBlock> blocks, RaycastHit hit, out Vector3 localPosition)
        {
            localPosition = default;
            // 只处理本平台合并碰撞的命中，其他碰撞体交给各自的逻辑。
            if (!collisionRoot || !hit.collider ||
                !hit.collider.transform.IsChildOf(collisionRoot))
                return false;

            // 命中点先换到平台局部空间，后续比较都在同一套坐标里进行。
            Vector3 platformPoint = platform.InverseTransformPoint(hit.point);
            VoxelPlatformBlock result = null;
            // 命中点可能压在相邻方块的公共边界上，取包围盒距离最小的那个。
            float closestDistance = float.MaxValue;
            foreach (VoxelPlatformBlock block in blocks)
            {
                // 已破坏或没有网格的方块不参与反查。
                if (!block.Active || !block.Mesh) continue;

                // 先撤销平台变换，再撤销方块变换，兼容平台旋转和 Cube 自身偏移。
                Vector3 meshPoint = block.Matrix.inverse.MultiplyPoint3x4(platformPoint);
                Bounds bounds = block.Mesh.bounds;
                // 把点夹到包围盒内再求距离，点落在盒外时得到的就是最近距离。
                Vector3 closestPoint = new Vector3(
                    Mathf.Clamp(meshPoint.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(meshPoint.y, bounds.min.y, bounds.max.y),
                    Mathf.Clamp(meshPoint.z, bounds.min.z, bounds.max.z));
                float distance = (meshPoint - closestPoint).sqrMagnitude;
                // 距离为零说明命中点落在该方块网格的包围盒内；容差吸收浮点误差。
                if (distance > 0.000001f || distance >= closestDistance) continue;
                closestDistance = distance;
                result = block;
            }

            if (result == null) return false;
            // 按与 SetBlockActive 相同的精度还原局部中心，反查结果可以直接用于破坏与恢复。
            // 中心从位置键换回浮点，平台平移旋转后仍是同一方块的稳定标识。
            localPosition = (Vector3)result.Position / VoxelPlatformGeometry.PositionPrecision;
            return true;
        }
    }
}
