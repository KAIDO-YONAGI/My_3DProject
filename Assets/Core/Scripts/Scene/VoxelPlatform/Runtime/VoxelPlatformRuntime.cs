using System;
using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Baking;
using Core.Scripts.Scene.VoxelPlatform.Collision;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.Scene.VoxelPlatform.Fragments;
using Core.Scripts.Scene.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Runtime
{
    /// <summary>维护单个平台的方块状态，并把变化归并为需要重建的空间分块。</summary>
    class VoxelPlatformRuntime
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformFragments fragments;
        // 一次变化可能波及相邻多个分块，先归并再统一重建，避免同一分块重复重建。
        private readonly HashSet<Vector3Int> dirtyChunks = new();
        // 批量破坏的收集缓冲，避免每次破坏都分配临时列表。
        private readonly List<VoxelPlatformBlock> brokenBlocks = new();
        private readonly List<VoxelPlatformBlock> pendingTargets = new();
        // 源布局收集与爆炸结算共用的缓冲，重建和攻击都不再分配临时列表。
        private readonly List<VoxelPlatformBlock> collected = new();
        // 攻击只索引世界 XZ chunk 列，高度不限制候选块。
        private readonly Dictionary<Vector2Int, List<VoxelPlatformBlock>> horizontalColumns = new();
        private readonly HashSet<Vector2Int> visitedHorizontalColumns = new();
        private readonly List<Vector2Int> pendingHorizontalColumns = new();
        // 只有带源组件的编辑布局才需要轮询，运行资源不必承担这段开销。
        private bool hasSourceBlocks;
        private Matrix4x4 horizontalIndexTransform;
        private bool horizontalIndexInitialized;
        // 方块在平台局部空间中的最大包围盒半尺寸，世界爆炸范围按它扩张后再映射区块。
        private Vector3 maxBlockHalfSize;

        // 运行时唯一的方块集合按区块登记，位置查找与分块重建都从所属区块起步。
        private readonly Dictionary<Vector3Int, List<VoxelPlatformBlock>> chunks = new();
        // 遮挡索引只登记启用且能挡住邻居的方块，供接触面剔除与邻块查询使用。
        internal readonly Dictionary<Vector3Int, VoxelPlatformBlock> Occupied = new();
        internal VoxelPlatformMeshBuilder Meshes { get; }
        internal int LastHorizontalCandidateColumnCount { get; private set; }

        private static readonly Vector2Int[] HorizontalDirections =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        /// <summary>创建平台运行状态，并连接碎块和网格构建器。</summary>
        internal VoxelPlatformRuntime(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
            fragments = new VoxelPlatformFragments(owner);
            Meshes = new VoxelPlatformMeshBuilder(owner, this);
        }

        /// <summary>从烘焙记录重建运行索引并启用已保存的网格。</summary>
        internal void UseBakedSurface()
        {
            // 启用运行资源只恢复数据索引和已保存的网格，不重新收集源 Cube。
            Clear();
            if (!owner.HasBakedSurface) return;
            List<VoxelPlatformBakedBlock> saved = owner.BakedBlocks;
            for (int index = 0; index < saved.Count; index++) IndexBlock(saved[index].ToRuntimeBlock());
            UpdateOccupancy();
            Meshes.UseBakedChunks(owner.BakedChunks);
        }

        /// <summary>从源几何或烘焙记录重新建立平台运行状态。</summary>
        internal void Rebuild()
        {
            // 运行资源已剥离源层级，没有可采集的源几何，只能沿用现有烘焙表面。
            if (owner.HasBakedSurface && !owner.HasBakeSources)
            {
                UseBakedSurface();
                return;
            }

            Clear();
            // 根节点平时是关闭的，重建前先启用，新生成的子对象才会显示。
            Meshes.ActivateRoots();
            // 从保留源组件的编辑布局重新收集方块记录。
            collected.Clear();
            VoxelPlatformSourceCollector.Collect(owner, collected);
            hasSourceBlocks = collected.Count > 0;
            for (int index = 0; index < collected.Count; index++) IndexBlock(collected[index]);
            collected.Clear();
            // 占据关系必须先建好，逐分块重建时才能正确剔除接触面。
            UpdateOccupancy();
            ForEachChunk(Meshes.RebuildChunk);
        }

        /// <summary>按平台局部位置启用或破坏单个方块。</summary>
        internal bool SetBlockActive(Vector3 localPosition, bool active)
        {
            // 返回值表示是否找到目标；重复设置同一状态不重复生成碎块。
            if (!TryGetBlock(localPosition, out VoxelPlatformBlock block)) return false;
            if (block.Active == active) return true;
            if (active)
            {
                block.Active = true;
                if (block.Object) block.Object.SetActive(true);
                fragments.Remove(block.Position);
                RebuildAffectedChunks(block);
                return true;
            }

            pendingTargets.Clear();
            pendingTargets.Add(block);
            // 单块破坏沿用 Prefab 上的碎块碰撞配置。
            BreakBlocks(pendingTargets);
            return true;
        }

        // 入参按方块键的同一精度取整，调用方给近似中心即可命中。
        /// <summary>按位置键从占据索引或所属 chunk 定位方块记录。</summary>
        internal bool TryGetBlock(Vector3 localPosition, out VoxelPlatformBlock block)
        {
            Vector3Int position = VoxelPlatformGeometry.PositionKeyOf(localPosition);
            // 占据索引只登记启用方块，先查它能让常见命中早日结束；其余方块退回所属区块。
            if (Occupied.TryGetValue(position, out block)) return true;
            if (!chunks.TryGetValue(VoxelPlatformGeometry.ChunkOfPosition(position),
                    out List<VoxelPlatformBlock> candidates))
            {
                block = null;
                return false;
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Position != position) continue;
                block = candidates[index];
                return true;
            }

            block = null;
            return false;
        }

        // 按区块取得候选方块；没有登记的分块返回 false，调用方不必再判空。
        /// <summary>取得一个三维 chunk 内登记的方块候选。</summary>
        internal bool TryGetChunkBlocks(Vector3Int chunk, out List<VoxelPlatformBlock> blocks)
        {
            return chunks.TryGetValue(chunk, out blocks);
        }

        /// <summary>由命中 Collider 找到对应 chunk，并解析命中的平台局部方块位置。</summary>
        internal bool TryGetBlockPosition(RaycastHit hit, out Vector3 localPosition)
        {
            if (!hit.collider || !Meshes.Collisions.TryGetChunk(hit.collider, out Vector3Int chunk) ||
                !TryGetChunkBlocks(chunk, out List<VoxelPlatformBlock> candidates))
            {
                localPosition = default;
                return false;
            }

            return VoxelPlatformHitResolver.TryGetBlockPosition(owner.transform, owner.CollisionRoot,
                candidates, hit, out localPosition);
        }

        // 按区块遍历运行索引；回调不得增删区块，重建与遍历互不影响。
        /// <summary>对当前已登记的三维 chunk 执行只读回调。</summary>
        internal void ForEachChunk(Action<Vector3Int> visitor)
        {
            foreach (Vector3Int chunk in chunks.Keys) visitor(chunk);
        }

        // 烘焙遍历由运行索引提供，调用方只拿到要保存的记录。
        /// <summary>将运行索引中的方块记录追加为烘焙数据。</summary>
        internal void CollectBakedBlocks(List<VoxelPlatformBakedBlock> results)
        {
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                foreach (VoxelPlatformBlock block in list)
                    results.Add(block.ToBaked());
        }

        /// <summary>按区域体积收集命中方块并执行一次批量破坏。</summary>
        internal int BreakBlocksIn(IReadOnlyList<VoxelPlatformBlast> blasts, float? collisionDuration)
        {
            Matrix4x4 localToWorld = owner.transform.localToWorldMatrix;
            CollectBlastTargets(blasts, localToWorld, pendingTargets);
            return BreakBlocks(pendingTargets, collisionDuration);
        }

        /// <summary>从爆炸中心向邻接水平 chunk 列扩展并精确筛选命中方块。</summary>
        internal int BreakBlocksAroundWorldCenter(Vector3 worldCenter, float worldRadius,
            float? collisionDuration = null)
        {
            Matrix4x4 localToWorld = owner.transform.localToWorldMatrix;
            EnsureHorizontalIndexCurrent(localToWorld);
            pendingTargets.Clear();
            pendingHorizontalColumns.Clear();
            visitedHorizontalColumns.Clear();
            LastHorizontalCandidateColumnCount = 0;
            if (chunks.Count == 0) return 0;
            if (float.IsNaN(worldRadius) || float.IsInfinity(worldRadius)) return 0;

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
                    LastHorizontalCandidateColumnCount++;
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        VoxelPlatformBlock block = candidates[index];
                        if (!block.Active || !block.Mesh) continue;

                        Vector3 blockCenter = GetWorldBlockCenter(block, localToWorld);
                        float dx = blockCenter.x - worldCenter.x;
                        float dz = blockCenter.z - worldCenter.z;
                        if (dx * dx + dz * dz <= radiusSquared) pendingTargets.Add(block);
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

            return BreakBlocks(pendingTargets, collisionDuration);
        }

        /// <summary>平台世界变换改变时重建水平 chunk 列索引。</summary>
        private void EnsureHorizontalIndexCurrent(Matrix4x4 localToWorld)
        {
            if (horizontalIndexInitialized && horizontalIndexTransform.Equals(localToWorld)) return;

            horizontalColumns.Clear();
            foreach (List<VoxelPlatformBlock> blocks in chunks.Values)
                for (int index = 0; index < blocks.Count; index++)
                    IndexHorizontalBlock(blocks[index], localToWorld);

            horizontalIndexTransform = localToWorld;
            horizontalIndexInitialized = true;
        }

        /// <summary>按方块世界中心将方块加入对应水平 chunk 列。</summary>
        private void IndexHorizontalBlock(VoxelPlatformBlock block, Matrix4x4 localToWorld)
        {
            Vector2Int column = HorizontalColumnOf(GetWorldBlockCenter(block, localToWorld));
            if (!horizontalColumns.TryGetValue(column, out List<VoxelPlatformBlock> blocks))
                horizontalColumns.Add(column, blocks = new List<VoxelPlatformBlock>());
            blocks.Add(block);
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

        /// <summary>判断水平 chunk 列的矩形是否与爆炸圆相交。</summary>
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

        // 先把多份体积合并成保守的局部范围并映射到区块，每区块只扫描一次，
        // 最终仍按世界包围盒精确判定，重叠体积覆盖的同一方块只进入结果一次。
        /// <summary>只在区域体积覆盖的三维 chunk 中收集相交方块，合并重叠体积结果。</summary>
        private void CollectBlastTargets(IReadOnlyList<VoxelPlatformBlast> blasts, Matrix4x4 localToWorld,
            List<VoxelPlatformBlock> results)
        {
            results.Clear();
            if (blasts == null || blasts.Count == 0 || chunks.Count == 0) return;

            Bounds worldRange = blasts[0].WorldBounds;
            for (int index = 1; index < blasts.Count; index++) worldRange.Encapsulate(blasts[index].WorldBounds);

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

            localMin -= maxBlockHalfSize;
            localMax += maxBlockHalfSize;
            // 范围映射到区块坐标，不存在的区块直接跳过。
            Vector3Int minChunk = VoxelPlatformGeometry.ChunkOf(localMin);
            Vector3Int maxChunk = VoxelPlatformGeometry.ChunkOf(localMax);
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            for (int y = minChunk.y; y <= maxChunk.y; y++)
            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                if (!chunks.TryGetValue(new Vector3Int(x, y, z), out List<VoxelPlatformBlock> candidates))
                    continue;
                for (int index = 0; index < candidates.Count; index++)
                {
                    VoxelPlatformBlock block = candidates[index];
                    if (!block.Active || !block.Mesh) continue;
                    Bounds bounds = GetWorldBounds(block, localToWorld);
                    for (int volume = 0; volume < blasts.Count; volume++)
                    {
                        if (!blasts[volume].Overlaps(bounds)) continue;
                        results.Add(block);
                        break;
                    }
                }
            }
        }

        // 旋转后的轴对齐包围盒；整数 90 度旋转时与方块本身完全重合。
        /// <summary>计算方块网格变换后的世界轴对齐包围盒。</summary>
        private static Bounds GetWorldBounds(VoxelPlatformBlock block, Matrix4x4 localToWorld)
        {
            Matrix4x4 matrix = localToWorld * block.Matrix;
            Bounds local = block.Mesh.bounds;
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 extents = local.extents;
            Vector3 size = new Vector3(
                Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z);
            return new Bounds(center, size * 2f);
        }

        // 批量破坏：先把所有目标切到关闭并生成碎块，再统一重建一次受影响分块。
        /// <summary>停用目标方块、创建碎块并对受影响 chunk 各重建一次。</summary>
        internal int BreakBlocks(List<VoxelPlatformBlock> targets, float? collisionDuration = null)
        {
            brokenBlocks.Clear();
            foreach (VoxelPlatformBlock block in targets)
            {
                // 同一帧多份体积可能覆盖同一块，已破坏的直接跳过。
                if (block == null || !block.Active) continue;
                block.Active = false;
                if (block.Object) block.Object.SetActive(false);
                fragments.Spawn(block, collisionDuration);
                brokenBlocks.Add(block);
            }

            if (brokenBlocks.Count == 0) return 0;
            // 保留旧占据状态收集受影响邻块，再只改动本次破坏的占据键。
            dirtyChunks.Clear();
            foreach (VoxelPlatformBlock block in brokenBlocks) MarkAffectedChunks(block);
            for (int index = 0; index < brokenBlocks.Count; index++)
                UpdateOccupancyForBlock(brokenBlocks[index]);
            foreach (Vector3Int chunk in dirtyChunks) Meshes.RebuildChunk(chunk);
            return brokenBlocks.Count;
        }

        /// <summary>同步源对象的启停与移动，并局部刷新或整体重建平台。</summary>
        internal void RefreshChangedBlocks()
        {
            // 运行资源只有数据，没有源组件，直接跳过编辑预览用的轮询。
            if (!hasSourceBlocks) return;
            // 脏分块每轮重新累积，上一轮的结果不能带进来。
            dirtyChunks.Clear();
            pendingTargets.Clear();
            // 平台矩阵在循环外求一次逆，避免对每个方块重复计算。
            Matrix4x4 worldToLocal = owner.transform.worldToLocalMatrix;
            bool moved = false;
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
            {
                foreach (VoxelPlatformBlock block in list)
                {
                    if (block.Transform && block.Transform.hasChanged)
                    {
                        block.Transform.hasChanged = false;
                        // 矩阵变化说明源方块被移动，记录里的落格已经失效，只能整体重建。
                        if (worldToLocal * block.Transform.localToWorldMatrix != block.Matrix)
                        {
                            moved = true;
                            break;
                        }
                    }

                    // Inspector 中的启停和父级开关只能通过源组件读取，轮询后同步到运行记录。
                    bool active = VoxelPlatformSourceCollector.IsActive(block);
                    if (active == block.Active) continue;
                    block.Active = active;
                    pendingTargets.Add(block);
                    MarkAffectedChunks(block);
                }

                if (moved) break;
            }

            // 整体重建会清空区块表，必须等遍历结束再执行。
            if (moved)
            {
                Rebuild();
                return;
            }

            // 没有任何方块变化时跳过占据重建和分块刷新，轮询开销只剩一次遍历。
            if (dirtyChunks.Count == 0) return;
            for (int index = 0; index < pendingTargets.Count; index++)
                UpdateOccupancyForBlock(pendingTargets[index]);
            foreach (Vector3Int chunk in dirtyChunks) Meshes.RebuildChunk(chunk);
        }

        /// <summary>更新单块占据状态并重建自身及相邻方块所在 chunk。</summary>
        private void RebuildAffectedChunks(VoxelPlatformBlock block)
        {
            dirtyChunks.Clear();
            // 先查询变化前的占据索引，确保跨分块的相邻表面也进入刷新范围。
            MarkAffectedChunks(block);
            UpdateOccupancyForBlock(block);
            foreach (Vector3Int chunk in dirtyChunks) Meshes.RebuildChunk(chunk);
        }

        /// <summary>按单个方块的当前状态更新遮挡占据索引。</summary>
        private void UpdateOccupancyForBlock(VoxelPlatformBlock block)
        {
            if (!chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> candidates))
            {
                Occupied.Remove(block.Position);
                return;
            }

            Occupied.Remove(block.Position);
            for (int index = 0; index < candidates.Count; index++)
            {
                VoxelPlatformBlock candidate = candidates[index];
                if (candidate.Position == block.Position && candidate.Occludes && candidate.Active)
                    Occupied[block.Position] = candidate;
            }
        }

        /// <summary>登记方块和其已占据邻居所在的全部待刷新 chunk。</summary>
        private void MarkAffectedChunks(VoxelPlatformBlock block)
        {
            // 邻居的接触面同样失效，所以相邻方块所在的分块一起进入刷新范围。
            dirtyChunks.Add(block.Chunk);
            foreach (Vector3Int direction in VoxelPlatformGeometry.Directions)
            {
                if (Occupied.TryGetValue(block.Position + direction * VoxelPlatformGeometry.PositionPrecision,
                        out VoxelPlatformBlock neighbor))
                    dirtyChunks.Add(neighbor.Chunk);
            }
        }

        // 区块坐标由位置键统一换算，收集、烘焙加载与位置查找因此指向同一分块。
        /// <summary>把方块记录加入所属 chunk、水平列索引和尺寸统计。</summary>
        private void IndexBlock(VoxelPlatformBlock block)
        {
            block.Chunk = VoxelPlatformGeometry.ChunkOfPosition(block.Position);
            if (!chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> list))
                chunks.Add(block.Chunk, list = new List<VoxelPlatformBlock>());
            list.Add(block);
            if (block.Mesh)
            {
                maxBlockHalfSize = Vector3.Max(maxBlockHalfSize, VoxelPlatformGeometry.LocalHalfSize(block));
                if (!horizontalIndexInitialized)
                {
                    horizontalIndexTransform = owner.transform.localToWorldMatrix;
                    horizontalIndexInitialized = true;
                }
                IndexHorizontalBlock(block, horizontalIndexTransform);
            }
        }

        /// <summary>从当前运行记录重建启用且具有遮挡作用的方块索引。</summary>
        private void UpdateOccupancy()
        {
            Occupied.Clear();
            // 三维中心包含高度；重复键使用赋值建立索引，避免保存资源时抛出重复键异常。
            // 已破坏的方块不再遮挡邻居，建索引时就要排除。
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                foreach (VoxelPlatformBlock block in list)
                    if (block.Occludes && block.Active) Occupied[block.Position] = block;
        }

        /// <summary>恢复源渲染状态并释放平台的网格、碎块和空间索引。</summary>
        internal void Clear()
        {
            // 恢复源渲染状态，编辑视图才能重新显示被合并表面顶替的源 Cube。
            // 恢复要用到方块记录，这一步必须排在区块表清空之前。
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                foreach (VoxelPlatformBlock block in list)
                    if (block.Renderer) block.Renderer.forceRenderingOff = block.OriginalForceOff;
            Meshes.Clear();
            fragments.Clear();
            chunks.Clear();
            hasSourceBlocks = false;
            maxBlockHalfSize = Vector3.zero;
            Occupied.Clear();
            dirtyChunks.Clear();
            brokenBlocks.Clear();
            pendingTargets.Clear();
            collected.Clear();
            horizontalColumns.Clear();
            visitedHorizontalColumns.Clear();
            pendingHorizontalColumns.Clear();
            horizontalIndexInitialized = false;
            LastHorizontalCandidateColumnCount = 0;
        }
    }
}
