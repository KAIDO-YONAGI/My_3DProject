using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Collision;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Destruction;
using Core.Scripts.VoxelPlatform.Fragments;
using Core.Scripts.VoxelPlatform.Rendering;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Runtime
{
    /// <summary>编排平台运行状态的生命周期、方块变化与破坏结算。</summary>
    class VoxelPlatformRuntime
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformFragments fragments;
        private readonly VoxelPlatformMeshBuilder meshes;
        private readonly VoxelPlatformBlockTable blocks;
        private readonly VoxelPlatformChunkUpdates chunkUpdates;
        private readonly VoxelPlatformDestructionTargeting destructionTargeting;
        private readonly VoxelPlatformBlockDestroyer destroyer;
        private readonly VoxelPlatformSourceSync sourceSync;
        // 三条破坏入口共用的目标缓冲，填写方在入口先清空再填。
        private readonly List<VoxelPlatformBlock> destroyTargets = new();

        /// <summary>网格构建器，供烘焙与平台统计读取分块结果。</summary>
        internal VoxelPlatformMeshBuilder Meshes => meshes;

        /// <summary>创建平台运行状态，并连接方块表、破坏、源同步和网格构建器。</summary>
        internal VoxelPlatformRuntime(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
            fragments = new VoxelPlatformFragments(owner);
            blocks = new VoxelPlatformBlockTable();
            meshes = new VoxelPlatformMeshBuilder(owner, blocks);
            chunkUpdates = new VoxelPlatformChunkUpdates(blocks, meshes);
            destructionTargeting = new VoxelPlatformDestructionTargeting(owner, blocks);
            destroyer = new VoxelPlatformBlockDestroyer(blocks, chunkUpdates, fragments);
            sourceSync = new VoxelPlatformSourceSync(owner, blocks, chunkUpdates);
        }

        /// <summary>从烘焙记录重建运行索引并启用已保存的网格。</summary>
        internal void UseBakedSurface()
        {
            // 启用运行资源只恢复数据索引和已保存的网格，不重新收集源 Cube。
            Clear();
            if (!owner.HasBakedSurface) return;
            List<VoxelPlatformBakedBlock> saved = owner.BakedBlocks;
            for (int index = 0; index < saved.Count; index++) blocks.Index(saved[index].ToRuntimeBlock());
            blocks.RebuildOccupancy();
            destructionTargeting.RebuildIndex();
            meshes.UseBakedChunks(owner.BakedChunks);
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
            meshes.ActivateRoots();
            // 从保留源组件的编辑布局重新收集方块记录。
            sourceSync.IndexSourceBlocks();
            // 占据关系必须先建好，逐分块重建时才能正确剔除接触面。
            blocks.RebuildOccupancy();
            destructionTargeting.RebuildIndex();
            blocks.ForEachChunk(meshes.RebuildChunk);
        }

        /// <summary>按平台局部位置启用或破坏单个方块。</summary>
        internal bool SetBlockActive(Vector3 localPosition, bool active)
        {
            // 返回值表示是否找到目标；重复设置同一状态不重复生成碎块。
            if (!blocks.TryGetBlock(VoxelPlatformGeometry.PositionKeyOf(localPosition), out VoxelPlatformBlock block))
                return false;
            if (block.Active == active) return true;
            if (active)
            {
                block.Active = true;
                if (block.Object) block.Object.SetActive(true);
                fragments.Remove(block.Position);
                chunkUpdates.Clear();
                // 先查询变化前的占据索引，确保跨分块的相邻表面也进入刷新范围。
                chunkUpdates.Mark(block);
                blocks.UpdateOccupancyForBlock(block);
                chunkUpdates.Flush();
                return true;
            }

            destroyTargets.Clear();
            destroyTargets.Add(block);
            // 单块破坏沿用 Prefab 上的碎块碰撞配置。
            destroyer.Destroy(destroyTargets);
            return true;
        }

        /// <summary>由命中 Collider 找到对应 chunk，并解析命中的平台局部方块位置。</summary>
        internal bool TryGetBlockPosition(RaycastHit hit, out Vector3 localPosition)
        {
            if (!hit.collider || !meshes.Collisions.TryGetChunk(hit.collider, out Vector3Int chunk) ||
                !blocks.TryGetChunkBlocks(chunk, out List<VoxelPlatformBlock> candidates))
            {
                localPosition = default;
                return false;
            }

            return VoxelPlatformHitResolver.TryGetBlockPosition(owner.transform, owner.CollisionRoot,
                candidates, hit, out localPosition);
        }

        /// <summary>将运行索引中的方块记录追加为烘焙数据。</summary>
        internal void CollectBakedBlocks(List<VoxelPlatformBakedBlock> results)
        {
            blocks.AppendBakedBlocks(results);
        }

        /// <summary>按区域体积收集命中方块并执行一次批量破坏。</summary>
        internal int DestroyBlocksIn(IReadOnlyList<VoxelPlatformDestructionVolume> volumes, float? collisionDuration)
        {
            destructionTargeting.CollectInVolumes(volumes, owner.transform.localToWorldMatrix, destroyTargets);
            return destroyer.Destroy(destroyTargets, collisionDuration);
        }

        /// <summary>从破坏中心向邻接水平 chunk 列扩展并精确筛选命中方块。</summary>
        internal int DestroyBlocksAroundWorldCenter(Vector3 worldCenter, float worldRadius,
            float? collisionDuration = null)
        {
            destructionTargeting.CollectAroundWorldCenter(worldCenter, worldRadius, destroyTargets);
            return destroyer.Destroy(destroyTargets, collisionDuration);
        }

        /// <summary>同步源对象的启停与移动，并局部刷新或整体重建平台。</summary>
        internal void RefreshChangedBlocks()
        {
            // 运行资源只有数据，没有源组件，直接跳过编辑预览用的轮询。
            if (!sourceSync.HasSourceBlocks) return;
            // 移动检测需要整体重建，重建会清空区块表，必须等轮询结束再执行。
            if (sourceSync.PollAndApply()) Rebuild();
        }

        /// <summary>恢复源渲染状态并释放平台的网格、碎块和空间索引。</summary>
        internal void Clear()
        {
            // 源渲染状态只能从方块表恢复，必须在表清空之前执行。
            sourceSync.Reset();
            meshes.Clear();
            fragments.Clear();
            blocks.Clear();
            chunkUpdates.Clear();
            destructionTargeting.Clear();
        }
    }
}
