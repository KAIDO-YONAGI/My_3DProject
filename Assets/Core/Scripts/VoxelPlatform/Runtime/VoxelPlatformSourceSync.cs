using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Baking;
using Core.Scripts.VoxelPlatform.Data;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>维护编辑布局的源方块状态，并把它同步到运行索引。</summary>
    class VoxelPlatformSourceSync
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformBlockTable blocks;
        private readonly VoxelPlatformChunkUpdates chunkUpdates;
        // 源布局收集与启停同步共用的缓冲，收集和轮询都不再分配临时列表。
        private readonly List<VoxelPlatformBlock> collected = new();
        private readonly List<VoxelPlatformBlock> changedBlocks = new();

        /// <summary>当前索引是否来自保留源组件的编辑布局。</summary>
        internal bool HasSourceBlocks { get; private set; }

        internal VoxelPlatformSourceSync(VoxelPlatformRenderer owner, VoxelPlatformBlockTable blocks,
            VoxelPlatformChunkUpdates chunkUpdates)
        {
            this.owner = owner;
            this.blocks = blocks;
            this.chunkUpdates = chunkUpdates;
        }

        /// <summary>从保留源组件的编辑布局重新收集并登记方块。</summary>
        internal void IndexSourceBlocks()
        {
            collected.Clear();
            VoxelPlatformSourceCollector.Collect(owner, collected);
            HasSourceBlocks = collected.Count > 0;
            for (int index = 0; index < collected.Count; index++) blocks.Index(collected[index]);
            collected.Clear();
        }

        /// <summary>同步源方块的启停与移动；返回 true 表示需要整体重建。</summary>
        internal bool PollAndApply()
        {
            // 脏分块每轮重新累积，上一轮的结果不能带进来。
            chunkUpdates.Clear();
            changedBlocks.Clear();
            // 平台矩阵在循环外求一次逆，避免对每个方块重复计算。
            Matrix4x4 worldToLocal = owner.transform.worldToLocalMatrix;
            bool moved = false;
            blocks.ForEachBlock(block =>
            {
                if (moved) return;
                if (block.Transform && block.Transform.hasChanged)
                {
                    block.Transform.hasChanged = false;
                    // 矩阵变化说明源方块被移动，记录里的落格已经失效，只能整体重建。
                    if (worldToLocal * block.Transform.localToWorldMatrix != block.Matrix)
                    {
                        moved = true;
                        return;
                    }
                }

                // Inspector 中的启停和父级开关只能通过源组件读取，轮询后同步到运行记录。
                bool active = VoxelPlatformSourceCollector.IsActive(block);
                if (active == block.Active) return;
                block.Active = active;
                changedBlocks.Add(block);
                chunkUpdates.Mark(block);
            });

            // 整体重建会清空区块表，必须等遍历结束再交给调用方执行。
            if (moved) return true;

            // 没有任何方块变化时跳过占据重建和分块刷新，轮询开销只剩一次遍历。
            if (changedBlocks.Count == 0) return false;
            for (int index = 0; index < changedBlocks.Count; index++)
                blocks.UpdateOccupancyForBlock(changedBlocks[index]);
            chunkUpdates.Flush();
            return false;
        }

        /// <summary>恢复源渲染状态并清除源布局标记。</summary>
        internal void Reset()
        {
            // 恢复源渲染状态，编辑视图才能重新显示被合并表面顶替的源 Cube。
            // 恢复要用到方块记录，这一步必须排在区块表清空之前。
            blocks.ForEachBlock(RestoreSourceRendering);
            HasSourceBlocks = false;
        }

        private static void RestoreSourceRendering(VoxelPlatformBlock block)
        {
            if (block.Renderer) block.Renderer.forceRenderingOff = block.OriginalForceOff;
        }
    }
}
