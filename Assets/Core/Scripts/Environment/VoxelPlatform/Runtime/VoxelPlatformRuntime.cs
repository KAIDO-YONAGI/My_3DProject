using System.Collections.Generic;
using Core.Scripts.Environment.VoxelPlatform.Baking;
using Core.Scripts.Environment.VoxelPlatform.Data;
using Core.Scripts.Environment.VoxelPlatform.Fragments;
using Core.Scripts.Environment.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Runtime
{
    // 维护单个平台的方块状态，并把一次变化归并为需要重建的空间分块。
    class VoxelPlatformRuntime
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformFragments fragments;
        // 一次变化可能波及相邻多个分块，先归并再统一重建，避免同一分块重复重建。
        private readonly HashSet<Vector3Int> dirtyChunks = new();
        // 只有带源组件的编辑布局才需要轮询，运行资源不必承担这段开销。
        private bool hasSourceBlocks;

        // 三张表各负责一件事：顺序遍历用列表，接触面查询用占据索引，空间重建用分块表。
        internal readonly List<VoxelPlatformBlock> Blocks = new();
        internal readonly Dictionary<Vector3Int, VoxelPlatformBlock> Occupied = new();
        internal readonly Dictionary<Vector3Int, List<VoxelPlatformBlock>> Chunks =new();
        internal VoxelPlatformMeshBuilder Meshes { get; }

        internal VoxelPlatformRuntime(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
            fragments = new VoxelPlatformFragments(owner);
            Meshes = new VoxelPlatformMeshBuilder(owner, this);
        }

        internal void UseBakedSurface()
        {
            // 启用运行资源只恢复数据索引和已保存的网格，不重新收集源 Cube。
            Clear();
            if (!owner.HasBakedSurface) return;
            foreach (VoxelPlatformBakedBlock saved in owner.BakedBlocks)
            {
                // 烘焙记录已不含源对象引用，只还原落格、矩阵和渲染配置。
                VoxelPlatformBlock block = saved.ToRuntimeBlock();
                Blocks.Add(block);
                AddToChunk(block);
            }
            UpdateOccupancy();
            Meshes.UseBakedChunks(owner.BakedChunks);
        }

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
            VoxelPlatformSourceCollector.Collect(owner, Blocks);
            hasSourceBlocks = Blocks.Count > 0;
            foreach (VoxelPlatformBlock block in Blocks) AddToChunk(block);
            // 占据关系必须先建好，逐分块重建时才能正确剔除接触面。
            UpdateOccupancy();
            foreach (Vector3Int chunk in new List<Vector3Int>(Chunks.Keys))
                Meshes.RebuildChunk(chunk);
        }

        internal bool SetBlockActive(Vector3 localPosition, bool active)
        {
            // 入参按方块键的同一精度取整，调用方给近似中心即可命中。
            Vector3Int position = Vector3Int.RoundToInt(localPosition * VoxelPlatformGeometry.PositionPrecision);
            // 占据索引只登记遮挡方块，非遮挡方块要退回线性查找。
            if (!Occupied.TryGetValue(position, out VoxelPlatformBlock block))
            {
                foreach (VoxelPlatformBlock candidate in Blocks)
                {
                    if (candidate.Position != position) continue;
                    block = candidate;
                    break;
                }
            }

            // 返回值表示是否找到目标；重复设置同一状态不重复生成碎块。
            if (block == null) return false;
            if (block.Active == active) return true;
            // 运行记录和源对象一起改，编辑视图与运行状态才不会分叉。
            // 烘焙数据的方块没有源对象，这一步只改记录本身。
            block.Active = active;
            if (block.Object) block.Object.SetActive(active);
            // 恢复时清掉旧碎块，破坏时补上碎块实例。
            if (active) fragments.Remove(block.Position);
            else fragments.Spawn(block);
            RebuildAffectedChunks(block);
            return true;
        }

        internal void RefreshChangedBlocks()
        {
            // 运行资源只有数据，没有源组件，直接跳过编辑预览用的轮询。
            if (!hasSourceBlocks) return;
            // 脏分块每轮重新累积，上一轮的结果不能带进来。
            dirtyChunks.Clear();
            // 平台矩阵在循环外求一次逆，避免对每个方块重复计算。
            Matrix4x4 worldToLocal = owner.transform.worldToLocalMatrix;
            foreach (VoxelPlatformBlock block in Blocks)
            {
                if (block.Transform && block.Transform.hasChanged)
                {
                    block.Transform.hasChanged = false;
                    // 矩阵变化说明源方块被移动，记录里的落格已经失效，只能整体重建。
                    if (worldToLocal * block.Transform.localToWorldMatrix != block.Matrix)
                    {
                        Rebuild();
                        return;
                    }
                }

                // Inspector 中的启停和父级开关只能通过源组件读取，轮询后同步到运行记录。
                bool active = VoxelPlatformSourceCollector.IsActive(block);
                if (active == block.Active) continue;
                block.Active = active;
                MarkAffectedChunks(block);
            }

            // 没有任何方块变化时跳过占据重建和分块刷新，轮询开销只剩一次遍历。
            if (dirtyChunks.Count == 0) return;
            UpdateOccupancy();
            foreach (Vector3Int chunk in dirtyChunks) Meshes.RebuildChunk(chunk);
        }

        private void RebuildAffectedChunks(VoxelPlatformBlock block)
        {
            dirtyChunks.Clear();
            // 先查询变化前的占据索引，确保跨分块的相邻表面也进入刷新范围。
            MarkAffectedChunks(block);
            UpdateOccupancy();
            foreach (Vector3Int chunk in dirtyChunks) Meshes.RebuildChunk(chunk);
        }

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

        private void AddToChunk(VoxelPlatformBlock block)
        {
            // 分块表按空格归属登记，重建时只取出同一块内的方块，不必扫描全平台。
            if (!Chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> list))
                Chunks.Add(block.Chunk, list = new List<VoxelPlatformBlock>());
            list.Add(block);
        }

        private void UpdateOccupancy()
        {
            Occupied.Clear();
            // 三维中心包含高度；重复键使用赋值建立索引，避免保存资源时抛出重复键异常。
            // 已破坏的方块不再遮挡邻居，建索引时就要排除。
            foreach (VoxelPlatformBlock block in Blocks)
                if (block.Occludes && block.Active) Occupied[block.Position] = block;
        }

        internal void Clear()
        {
            // 恢复源渲染状态，编辑视图才能重新显示被合并表面顶替的源 Cube。
            // 恢复要用到方块记录，这一步必须排在 Blocks.Clear() 之前。
            foreach (VoxelPlatformBlock block in Blocks)
                if (block.Renderer) block.Renderer.forceRenderingOff = block.OriginalForceOff;
            Meshes.Clear();
            fragments.Clear();
            Blocks.Clear();
            hasSourceBlocks = false;
            Chunks.Clear();
            Occupied.Clear();
            dirtyChunks.Clear();
        }
    }
}
