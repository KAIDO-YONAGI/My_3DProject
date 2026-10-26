using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Scene.VoxelPlatform.Collision
{
    // 使用完整方块网格生成静态分块碰撞，与可见面剔除保持独立。
    class VoxelPlatformCollisionBuilder
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformRuntime runtime;
        // 分块到碰撞对象的映射，与表面表分开维护，两边生命周期互不依赖。
        internal readonly Dictionary<Vector3Int, VoxelPlatformCollisionSurface> Surfaces = new ();
        // 射线命中后按 Collider 直接取得所属 chunk，避免扫描整张碰撞分块表。
        private readonly Dictionary<Collider, Vector3Int> colliderChunks = new();
#if UNITY_EDITOR
        // 只在烘焙期间存在，用于复用上一轮烘焙的碰撞对象。
        private Queue<GameObject> bakePool;
#endif

        internal int ColliderCount { get; private set; }

        /// <summary>根据合并碰撞体索引取得所属 chunk。</summary>
        internal bool TryGetChunk(Collider collider, out Vector3Int chunk)
        {
            if (collider && colliderChunks.TryGetValue(collider, out chunk)) return true;
            chunk = default;
            return false;
        }

        /// <summary>绑定平台与运行状态，创建碰撞构建器。</summary>
        internal VoxelPlatformCollisionBuilder(VoxelPlatformRenderer owner, VoxelPlatformRuntime runtime)
        {
            this.owner = owner;
            this.runtime = runtime;
        }

        /// <summary>创建并启用承载合并碰撞体的根节点。</summary>
        internal void ActivateRoot()
        {
            // 碰撞根与表面根一样按需创建，平台之间互不干扰。
            if (!owner.CollisionRoot) owner.CollisionRoot = owner.CreateRoot("CombinedCollision");
            owner.CollisionRoot.gameObject.SetActive(true);
        }

        /// <summary>登记烘焙碰撞体，并为缺少碰撞资产的平台重建碰撞。</summary>
        internal void UseBakedChunks(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
                // 没有保存碰撞的分块跳过，稍后由兜底分支重新生成。
                if (chunk.Collision == null || !chunk.Collision.Object) continue;
                chunk.Collision.Baked = true;
                Surfaces[chunk.Position] = chunk.Collision;
                RegisterCollider(chunk.Position, chunk.Collision.Object);
                chunk.Collision.Object.SetActive(true);
                ColliderCount++;
            }

            // 兼容仅保存表面的烘焙数据；已有碰撞数据时直接复用资产。
            if (Surfaces.Count == 0) runtime.ForEachChunk(RebuildChunk);
        }

        /// <summary>重建一个 chunk 的完整方块网格碰撞体。</summary>
        internal void RebuildChunk(Vector3Int chunk)
        {
            ClearChunk(chunk);
            // 分块里没有方块时，清完旧碰撞体就结束，不必生成空网格。
            if (!runtime.TryGetChunkBlocks(chunk, out List<VoxelPlatformBlock> chunkBlocks)) return;

            // 碰撞保留完整方块网格，剔除后的表面会让被遮挡的接触面留下可以穿过的缝隙。
            var instances = new List<CombineInstance>();
            // 记住第一个方块，用来给合并后的碰撞体继承图层。
            VoxelPlatformBlock source = null;
            foreach (VoxelPlatformBlock block in chunkBlocks)
            {
                // 已破坏或缺网格的方块不参与碰撞。
                if (!block.Active || !block.Mesh) continue;
                source = source ?? block;
                instances.Add(new CombineInstance { mesh = block.Mesh, transform = block.Matrix });
            }

            if (instances.Count == 0) return;
            // 每个分块合成一个静态网格碰撞体，碰撞体数量压到分块数量级。
            var mesh = new Mesh
            {
                name = "PlatformCollision_" + chunk,
                hideFlags = HideFlags.DontSave,
                // 一个分块可能容纳上万个顶点，必须使用 32 位索引。
                indexFormat = IndexFormat.UInt32
            };
            // 合并时把方块矩阵烘进顶点，碰撞对象本身保持单位变换。
            mesh.CombineMeshes(instances.ToArray(), true, true);

            GameObject child = TakeObject(mesh.name);
            child.name = mesh.name;
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(owner.CollisionRoot, false);
            // 图层跟着源方块走，平台层与世界层对物理查询的可见性因此一致。
            child.layer = source != null ? source.Layer : owner.gameObject.layer;
            child.SetActive(true);
            MeshCollider collider = child.GetComponent<MeshCollider>();
            if (!collider) collider = child.AddComponent<MeshCollider>();
            // 静态平台用非凸网格，凸包会把凹形内部一并填实。
            collider.convex = false;
            // 清空后重新赋值，使复用的碰撞对象采用本次生成的网格。
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            VoxelPlatformCollisionSurface surface = new() { Object = child, Mesh = mesh };
            Surfaces[chunk] = surface;
            colliderChunks[collider] = chunk;
            ColliderCount++;
        }

        /// <summary>登记烘焙碰撞对象的 Collider 与所属 chunk。</summary>
        private void RegisterCollider(Vector3Int chunk, GameObject collisionObject)
        {
            Collider collider = collisionObject.GetComponent<Collider>();
            if (collider) colliderChunks[collider] = chunk;
        }

        // 烘焙时复用旧碰撞对象，避免每轮烘焙都新建一批子对象。
        /// <summary>从烘焙复用池取碰撞对象，池空时新建对象。</summary>
        private GameObject TakeObject(string name)
        {
#if UNITY_EDITOR
            if (bakePool != null && bakePool.Count > 0) return bakePool.Dequeue();
#endif
            return new GameObject(name);
        }

        /// <summary>移除单个 chunk 的碰撞映射并释放非烘焙对象。</summary>
        private void ClearChunk(Vector3Int chunk)
        {
            // 该分块从没生成过碰撞，没有可清理的对象。
            if (!Surfaces.TryGetValue(chunk, out VoxelPlatformCollisionSurface collision)) return;
            if (collision.Object && collision.Object.TryGetComponent(out Collider collider))
                colliderChunks.Remove(collider);
            // 先关闭再销毁，销毁要等到帧末才生效，这一帧不能再挡路。
            if (collision.Object) collision.Object.SetActive(false);
            ColliderCount--;
            // 属于资产的烘焙碰撞只隐藏，销毁会破坏平台资产的引用。
            if (!collision.Baked)
            {
                VoxelPlatformRenderer.Release(collision.Object);
                VoxelPlatformRenderer.Release(collision.Mesh);
            }
            Surfaces.Remove(chunk);
        }

        /// <summary>清理所有碰撞 chunk 并关闭碰撞根节点。</summary>
        internal void Clear()
        {
            // 复制键集合再遍历，ClearChunk 会从字典里移除当前项。
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys)) ClearChunk(chunk);
            colliderChunks.Clear();
            ColliderCount = 0;
            // 根节点关闭，场景层级里不留空容器。
            if (owner.CollisionRoot) owner.CollisionRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>收集本轮烘焙可复用的旧碰撞对象。</summary>
        internal void BeginBake(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            // 先收回旧碰撞对象供重建复用，平台内的碰撞引用因此保持不变。
            bakePool = new Queue<GameObject>();
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
                if (chunk.Collision != null && chunk.Collision.Object)
                    bakePool.Enqueue(chunk.Collision.Object);
        }

        /// <summary>销毁未复用的旧对象并结束碰撞对象复用。</summary>
        internal void EndBake()
        {
            // 池里剩余的旧对象这次没有被复用，烘焙结束后统一销毁。
            while (bakePool.Count > 0) VoxelPlatformRenderer.Release(bakePool.Dequeue());
            bakePool = null;
        }
#endif
    }
}
