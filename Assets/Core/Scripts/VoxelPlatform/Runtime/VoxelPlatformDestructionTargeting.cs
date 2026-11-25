using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Destruction;
using Core.Scripts.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>把世界空间的破坏范围映射为平台内候选方块。</summary>
    class VoxelPlatformDestructionTargeting
    {
        private static readonly Vector2Int[] HorizontalDirections =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformBlockTable blocks;
        // 攻击只索引世界 XZ chunk 列，高度不限制候选块。
        private readonly Dictionary<Vector2Int, List<VoxelPlatformBlock>> horizontalColumns = new();
        private readonly HashSet<Vector2Int> visitedHorizontalColumns = new();
        private readonly List<Vector2Int> pendingHorizontalColumns = new();
        // 世界列索引按平台世界变换构建，变换未变化时直接复用。
        private Matrix4x4 horizontalIndexTransform;
        private bool horizontalIndexInitialized;

        internal VoxelPlatformDestructionTargeting(VoxelPlatformRenderer owner, VoxelPlatformBlockTable blocks)
        {
            this.owner = owner;
            this.blocks = blocks;
        }

        /// <summary>最近一次圆形查询实际检查的水平 chunk 列数。</summary>
        internal int LastCandidateColumnCount { get; private set; }

        /// <summary>按当前平台世界变换重建水平 chunk 列索引。</summary>
        internal void RebuildIndex()
        {
            horizontalColumns.Clear();
            Matrix4x4 localToWorld = owner.transform.localToWorldMatrix;
            blocks.ForEachBlock(block =>
            {
                if (!block.Mesh) return;
                Vector2Int column = HorizontalColumnOf(GetWorldBlockCenter(block, localToWorld));
                if (!horizontalColumns.TryGetValue(column, out List<VoxelPlatformBlock> columnBlocks))
                    horizontalColumns.Add(column, columnBlocks = new List<VoxelPlatformBlock>());
                columnBlocks.Add(block);
            });
            horizontalIndexTransform = localToWorld;
            horizontalIndexInitialized = true;
        }

        /// <summary>从破坏中心向邻接水平 chunk 列扩展并精确筛选命中方块。</summary>
        internal void CollectAroundWorldCenter(Vector3 worldCenter, float worldRadius,
            List<VoxelPlatformBlock> results)
        {
            results.Clear();
            LastCandidateColumnCount = 0;
            pendingHorizontalColumns.Clear();
            visitedHorizontalColumns.Clear();
            if (blocks.ChunkCount == 0) return;
            if (float.IsNaN(worldRadius) || float.IsInfinity(worldRadius)) return;

            Matrix4x4 localToWorld = owner.transform.localToWorldMatrix;
            EnsureIndexCurrent(localToWorld);
            float radius = Mathf.Max(0f, worldRadius);
            float radiusSquared = radius * radius;
            Vector2Int start = HorizontalColumnOf(worldCenter);
            pendingHorizontalColumns.Add(start);
            visitedHorizontalColumns.Add(start);

            for (int cursor = 0; cursor < pendingHorizontalColumns.Count; cursor++)
            {
                Vector2Int column = pendingHorizontalColumns[cursor];
                if (!HorizontalColumnIntersectsCircle(column, worldCenter, radiusSquared)) continue;

                if (horizontalColumns.TryGetValue(column, out List<VoxelPlatformBlock> candidates))
                {
                    LastCandidateColumnCount++;
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        VoxelPlatformBlock block = candidates[index];
                        if (!block.Active || !block.Mesh) continue;

                        Vector3 blockCenter = GetWorldBlockCenter(block, localToWorld);
                        float dx = blockCenter.x - worldCenter.x;
                        float dz = blockCenter.z - worldCenter.z;
                        if (dx * dx + dz * dz <= radiusSquared) results.Add(block);
                    }
                }

                for (int direction = 0; direction < HorizontalDirections.Length; direction++)
                {
                    Vector2Int neighbor = column + HorizontalDirections[direction];
                    if (visitedHorizontalColumns.Add(neighbor) &&
                        HorizontalColumnIntersectsCircle(neighbor, worldCenter, radiusSquared))
                        pendingHorizontalColumns.Add(neighbor);
                }
            }
        }

        // 先把多份体积合并成保守的局部范围并映射到区块，每区块只扫描一次，
        // 最终仍按世界包围盒精确判定，重叠体积覆盖的同一方块只进入结果一次。
        /// <summary>只在区域体积覆盖的三维 chunk 中收集相交方块，合并重叠体积结果。</summary>
        internal void CollectInVolumes(IReadOnlyList<VoxelPlatformDestructionVolume> volumes, Matrix4x4 localToWorld,
            List<VoxelPlatformBlock> results)
        {
            results.Clear();
            if (volumes == null || volumes.Count == 0 || blocks.ChunkCount == 0) return;

            Bounds worldRange = volumes[0].WorldBounds;
            for (int index = 1; index < volumes.Count; index++) worldRange.Encapsulate(volumes[index].WorldBounds);

            // 包围盒角点换到平台局部空间，再按最大方块半尺寸扩张，得到保守的方块覆盖范围。
            Matrix4x4 worldToLocal = localToWorld.inverse;
            Vector3 min = worldRange.min;
            Vector3 max = worldRange.max;
            Vector3 localMin = Vector3.positiveInfinity;
            Vector3 localMax = Vector3.negativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 local = worldToLocal.MultiplyPoint3x4(point);
                localMin = Vector3.Min(localMin, local);
                localMax = Vector3.Max(localMax, local);
            }

            localMin -= blocks.MaxBlockHalfSize;
            localMax += blocks.MaxBlockHalfSize;
            // 范围映射到区块坐标，不存在的区块直接跳过。
            Vector3Int minChunk = VoxelPlatformGeometry.ChunkOf(localMin);
            Vector3Int maxChunk = VoxelPlatformGeometry.ChunkOf(localMax);
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            for (int y = minChunk.y; y <= maxChunk.y; y++)
            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                if (!blocks.TryGetChunkBlocks(new Vector3Int(x, y, z),
                        out List<VoxelPlatformBlock> candidates))
                    continue;
                for (int index = 0; index < candidates.Count; index++)
                {
                    VoxelPlatformBlock block = candidates[index];
                    if (!block.Active || !block.Mesh) continue;
                    Bounds bounds = GetWorldBounds(block, localToWorld);
                    for (int volume = 0; volume < volumes.Count; volume++)
                    {
                        if (!volumes[volume].Overlaps(bounds)) continue;
                        results.Add(block);
                        break;
                    }
                }
            }
        }

        /// <summary>释放世界列索引、扩展缓冲和查询统计。</summary>
        internal void Clear()
        {
            horizontalColumns.Clear();
            visitedHorizontalColumns.Clear();
            pendingHorizontalColumns.Clear();
            // 旧矩阵由初始化标志门控，这里只让索引失效。
            horizontalIndexInitialized = false;
            LastCandidateColumnCount = 0;
        }

        /// <summary>平台世界变换改变时重建水平 chunk 列索引。</summary>
        private void EnsureIndexCurrent(Matrix4x4 localToWorld)
        {
            if (horizontalIndexInitialized && horizontalIndexTransform.Equals(localToWorld)) return;
            RebuildIndex();
        }

        /// <summary>计算方块网格中心在世界空间中的坐标。</summary>
        private static Vector3 GetWorldBlockCenter(VoxelPlatformBlock block, Matrix4x4 localToWorld)
        {
            Vector3 localCenter = block.Matrix.MultiplyPoint3x4(block.Mesh.bounds.center);
            return localToWorld.MultiplyPoint3x4(localCenter);
        }

        /// <summary>将世界 XZ 坐标换算为水平 chunk 列坐标。</summary>
        private static Vector2Int HorizontalColumnOf(Vector3 worldPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / VoxelPlatformGeometry.ChunkSize),
                Mathf.FloorToInt(worldPosition.z / VoxelPlatformGeometry.ChunkSize));
        }

        /// <summary>判断水平 chunk 列的矩形是否与圆形范围相交。</summary>
        private static bool HorizontalColumnIntersectsCircle(Vector2Int column, Vector3 center,
            float radiusSquared)
        {
            float size = VoxelPlatformGeometry.ChunkSize;
            float minX = column.x * size;
            float minZ = column.y * size;
            float closestX = Mathf.Clamp(center.x, minX, minX + size);
            float closestZ = Mathf.Clamp(center.z, minZ, minZ + size);
            float dx = center.x - closestX;
            float dz = center.z - closestZ;
            return dx * dx + dz * dz <= radiusSquared;
        }

        // 旋转后的轴对齐包围盒；整数 90 度旋转时与方块本身完全重合。
        /// <summary>计算方块网格变换后的世界轴对齐包围盒。</summary>
        private static Bounds GetWorldBounds(VoxelPlatformBlock block, Matrix4x4 localToWorld)
        {
            Matrix4x4 matrix = localToWorld * block.Matrix;
            Bounds local = block.Mesh.bounds;
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 size = VoxelPlatformGeometry.TransformExtents(matrix, local.extents) * 2f;
            return new Bounds(center, size);
        }
    }
}
