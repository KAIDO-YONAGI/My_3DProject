using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>归并一次变化波及的相邻分块，并保证每个分块只重建一次。</summary>
    class VoxelPlatformChunkUpdates
    {
        private readonly VoxelPlatformBlockTable blocks;
        private readonly VoxelPlatformMeshBuilder meshes;
        // 一次变化可能波及相邻多个分块，先归并再统一重建，避免同一分块重复重建。
        private readonly HashSet<Vector3Int> dirtyChunks = new();

        internal VoxelPlatformChunkUpdates(VoxelPlatformBlockTable blocks, VoxelPlatformMeshBuilder meshes)
        {
            this.blocks = blocks;
            this.meshes = meshes;
        }

        /// <summary>清空本轮累积的待重建分块。</summary>
        internal void Clear()
        {
            dirtyChunks.Clear();
        }

        /// <summary>登记方块和其已占据邻居所在的全部待刷新 chunk。</summary>
        internal void Mark(VoxelPlatformBlock block)
        {
            // 邻居的接触面同样失效，所以相邻方块所在的分块一起进入刷新范围。
            dirtyChunks.Add(block.Chunk);
            foreach (Vector3Int direction in VoxelPlatformGeometry.Directions)
            {
                if (blocks.Occupied.TryGetValue(block.Position + direction * VoxelPlatformGeometry.PositionPrecision,
                        out VoxelPlatformBlock neighbor))
                    dirtyChunks.Add(neighbor.Chunk);
            }
        }

        /// <summary>对已登记的全部待重建分块各重建一次。</summary>
        internal void Flush()
        {
            foreach (Vector3Int chunk in dirtyChunks) meshes.RebuildChunk(chunk);
        }
    }
}
