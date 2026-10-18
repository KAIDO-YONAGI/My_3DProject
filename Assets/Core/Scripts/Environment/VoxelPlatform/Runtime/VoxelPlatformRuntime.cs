using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment
{
    // 维护单个平台的方块状态，并把一次变化归并为需要重建的空间分块。
    internal sealed class VoxelPlatformRuntime
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformFragments fragments;
        private readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();
        private bool hasSourceBlocks;

        internal readonly List<VoxelPlatformBlock> Blocks = new List<VoxelPlatformBlock>();
        internal readonly Dictionary<Vector3Int, VoxelPlatformBlock> Occupied =
            new Dictionary<Vector3Int, VoxelPlatformBlock>();
        internal readonly Dictionary<Vector3Int, List<VoxelPlatformBlock>> Chunks =
            new Dictionary<Vector3Int, List<VoxelPlatformBlock>>();
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
                VoxelPlatformBlock block = saved.ToRuntimeBlock();
                Blocks.Add(block);
                AddToChunk(block);
            }
            UpdateOccupancy();
            Meshes.UseBakedChunks(owner.BakedChunks);
        }

        internal void Rebuild()
        {
            if (owner.HasBakedSurface && !VoxelPlatformSourceCollector.HasSourceGeometry(owner))
            {
                UseBakedSurface();
                return;
            }

            Clear();
            Meshes.ActivateRoots();
            VoxelPlatformSourceCollector.Collect(owner, Blocks);
            hasSourceBlocks = Blocks.Count > 0;
            foreach (VoxelPlatformBlock block in Blocks) AddToChunk(block);
            UpdateOccupancy();
            foreach (Vector3Int chunk in new List<Vector3Int>(Chunks.Keys))
                Meshes.RebuildChunk(chunk);
        }

        internal bool SetBlockActive(Vector3 localPosition, bool active)
        {
            Vector3Int position = Vector3Int.RoundToInt(localPosition * VoxelPlatformGeometry.PositionPrecision);
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
            block.Active = active;
            if (block.Object) block.Object.SetActive(active);
            if (active) fragments.Remove(block.Position);
            else fragments.Spawn(block);
            RebuildAffectedChunks(block);
            return true;
        }

        internal void RefreshChangedBlocks()
        {
            // 运行资源只有数据，没有源组件，直接跳过编辑预览用的轮询。
            if (!hasSourceBlocks) return;
            dirtyChunks.Clear();
            Matrix4x4 worldToLocal = owner.transform.worldToLocalMatrix;
            foreach (VoxelPlatformBlock block in Blocks)
            {
                if (block.Transform && block.Transform.hasChanged)
                {
                    block.Transform.hasChanged = false;
                    if (worldToLocal * block.Transform.localToWorldMatrix != block.Matrix)
                    {
                        Rebuild();
                        return;
                    }
                }

                bool active = VoxelPlatformSourceCollector.IsActive(block);
                if (active == block.Active) continue;
                block.Active = active;
                MarkAffectedChunks(block);
            }

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
            if (!Chunks.TryGetValue(block.Chunk, out List<VoxelPlatformBlock> list))
                Chunks.Add(block.Chunk, list = new List<VoxelPlatformBlock>());
            list.Add(block);
        }

        private void UpdateOccupancy()
        {
            Occupied.Clear();
            // 三维中心包含高度；重复键使用赋值建立索引，避免保存资源时抛出重复键异常。
            foreach (VoxelPlatformBlock block in Blocks)
                if (block.Occludes && block.Active) Occupied[block.Position] = block;
        }

        internal void Clear()
        {
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
