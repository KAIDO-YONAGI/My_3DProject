using System;
using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>按空间分块保存运行方块，并维护遮挡占据索引与方块尺寸统计。</summary>
    class VoxelPlatformBlockTable
    {
        // 运行时唯一的方块集合按区块登记，位置查找与分块重建都从所属区块起步。
        private readonly Dictionary<Vector3Int, List<VoxelPlatformBlock>> chunks = new();
        // 遮挡索引只登记启用且能挡住邻居的方块，供接触面剔除与邻块查询使用。
        private readonly Dictionary<Vector3Int, VoxelPlatformBlock> occupied = new();
        // 方块在平台局部空间中的最大包围盒半尺寸，世界爆炸范围按它扩张后再映射区块。
        private Vector3 maxBlockHalfSize;

        /// <summary>只读遮挡索引，供接触面剔除与邻块查询使用。</summary>
        internal IReadOnlyDictionary<Vector3Int, VoxelPlatformBlock> Occupied => occupied;

        /// <summary>已登记分块数量。</summary>
        internal int ChunkCount => chunks.Count;

        /// <summary>已索引方块的最大包围盒半尺寸。</summary>
        internal Vector3 MaxBlockHalfSize => maxBlockHalfSize;

        /// <summary>登记方块记录、所属分块和尺寸统计。</summary>
        internal void Index(VoxelPlatformBlock block)
        {
            // 区块坐标由位置键统一换算，收集、烘焙加载与位置查找因此指向同一分块。
            block.Chunk = VoxelPlatformGeometry.ChunkOfPosition(block.Position);
            if (!chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> list))
                chunks.Add(block.Chunk, list = new List<VoxelPlatformBlock>());
            list.Add(block);
            if (block.Mesh)
                maxBlockHalfSize = Vector3.Max(maxBlockHalfSize, VoxelPlatformGeometry.LocalHalfSize(block));
        }

        /// <summary>按位置键从占据索引或所属 chunk 定位方块记录。</summary>
        internal bool TryGetBlock(Vector3Int position, out VoxelPlatformBlock block)
        {
            // 占据索引只登记启用方块，先查它能让常见命中早日结束；其余方块退回所属区块。
            if (occupied.TryGetValue(position, out block)) return true;
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

        /// <summary>取得一个三维 chunk 内登记的方块候选。</summary>
        internal bool TryGetChunkBlocks(Vector3Int chunk, out List<VoxelPlatformBlock> blocks)
        {
            return chunks.TryGetValue(chunk, out blocks);
        }

        /// <summary>对当前已登记的三维 chunk 执行只读回调。</summary>
        internal void ForEachChunk(Action<Vector3Int> visitor)
        {
            foreach (Vector3Int chunk in chunks.Keys) visitor(chunk);
        }

        /// <summary>对当前已登记的全部方块执行只读回调。</summary>
        internal void ForEachBlock(Action<VoxelPlatformBlock> visitor)
        {
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                for (int index = 0; index < list.Count; index++)
                    visitor(list[index]);
        }

        /// <summary>将运行索引中的方块记录追加为烘焙数据。</summary>
        internal void AppendBakedBlocks(List<VoxelPlatformBakedBlock> results)
        {
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                for (int index = 0; index < list.Count; index++)
                    results.Add(list[index].ToBaked());
        }

        /// <summary>按单个方块的当前状态更新遮挡占据索引。</summary>
        internal void UpdateOccupancyForBlock(VoxelPlatformBlock block)
        {
            occupied.Remove(block.Position);
            if (!chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> candidates)) return;
            for (int index = 0; index < candidates.Count; index++)
            {
                VoxelPlatformBlock candidate = candidates[index];
                if (candidate.Position == block.Position && candidate.Occludes && candidate.Active)
                    occupied[block.Position] = candidate;
            }
        }

        /// <summary>从当前运行记录重建启用且具有遮挡作用的方块索引。</summary>
        internal void RebuildOccupancy()
        {
            occupied.Clear();
            // 三维中心包含高度；重复键使用赋值建立索引，避免保存资源时抛出重复键异常。
            // 已破坏的方块不再遮挡邻居，建索引时就要排除。
            foreach (List<VoxelPlatformBlock> list in chunks.Values)
                for (int index = 0; index < list.Count; index++)
                {
                    VoxelPlatformBlock block = list[index];
                    if (block.Occludes && block.Active) occupied[block.Position] = block;
                }
        }

        /// <summary>释放方块表、占据索引和尺寸统计。</summary>
        internal void Clear()
        {
            chunks.Clear();
            occupied.Clear();
            maxBlockHalfSize = Vector3.zero;
        }
    }
}
