using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Fragments;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>执行一次批量破坏：停用方块、生成碎块并刷新受影响分块。</summary>
    class VoxelPlatformBlockDestroyer
    {
        private readonly VoxelPlatformBlockTable blocks;
        private readonly VoxelPlatformChunkUpdates chunkUpdates;
        private readonly VoxelPlatformFragments fragments;
        // 批量破坏的收集缓冲，避免每次破坏都分配临时列表。
        private readonly List<VoxelPlatformBlock> destroyedBlocks = new();

        internal VoxelPlatformBlockDestroyer(VoxelPlatformBlockTable blocks,
            VoxelPlatformChunkUpdates chunkUpdates, VoxelPlatformFragments fragments)
        {
            this.blocks = blocks;
            this.chunkUpdates = chunkUpdates;
            this.fragments = fragments;
        }

        /// <summary>停用目标方块、创建碎块并对受影响 chunk 各重建一次。</summary>
        internal int Destroy(IReadOnlyList<VoxelPlatformBlock> targets, float? collisionDuration = null)
        {
            destroyedBlocks.Clear();
            for (int index = 0; index < targets.Count; index++)
            {
                VoxelPlatformBlock block = targets[index];
                // 同一帧多份体积可能覆盖同一块，已破坏的直接跳过。
                if (block == null || !block.Active) continue;
                block.Active = false;
                if (block.Object) block.Object.SetActive(false);
                fragments.Spawn(block, collisionDuration);
                destroyedBlocks.Add(block);
            }

            if (destroyedBlocks.Count == 0) return 0;
            // 保留旧占据状态收集受影响邻块，再只改动本次破坏的占据键。
            chunkUpdates.Clear();
            foreach (VoxelPlatformBlock block in destroyedBlocks) chunkUpdates.Mark(block);
            for (int index = 0; index < destroyedBlocks.Count; index++)
                blocks.UpdateOccupancyForBlock(destroyedBlocks[index]);
            chunkUpdates.Flush();
            return destroyedBlocks.Count;
        }
    }
}
