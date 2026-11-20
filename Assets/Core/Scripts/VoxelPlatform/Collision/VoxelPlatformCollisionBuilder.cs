using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.VoxelPlatform.Collision
{
    /// <summary>使用完整方块网格构建静态分块碰撞，并维护碰撞体到 chunk 的索引。</summary>
    class VoxelPlatformCollisionBuilder
    {
        // 回收池上限：稳态下破坏与重建的对象数量持平，超过上限说明碰上短时集中重建，继续留住只占内存。
        private const int PoolCapacity = 64;

        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformBlockTable blocks;
        // 分块到碰撞对象的映射，与表面表分开维护，两边生命周期互不依赖。
        internal readonly Dictionary<Vector3Int, VoxelPlatformCollisionSurface> Surfaces = new ();
        // 射线命中后按 Collider 直接取得所属 chunk，避免扫描整张碰撞分块表。
        private readonly Dictionary<Collider, Vector3Int> colliderChunks = new();
        // 合并用的实例列表与数组按轮复用，避免每次重建都分配临时集合。
        private readonly List<CombineInstance> instances = new();
        private readonly VoxelPlatformCombineBuffer combineBuffer = new();
        // 运行时生成的碰撞对象与网格回收后留在池里，下一次重建直接取用。
        private readonly Stack<GameObject> objectPool = new();
        private readonly Stack<Mesh> meshPool = new();
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

        /// <summary>绑定平台与方块表。</summary>
        internal VoxelPlatformCollisionBuilder(VoxelPlatformRenderer owner, VoxelPlatformBlockTable blocks)
        {
            this.owner = owner;
            this.blocks = blocks;
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
            if (Surfaces.Count == 0) blocks.ForEachChunk(RebuildChunk);
        }

        /// <summary>重建一个 chunk 的完整方块网格碰撞体。</summary>
        internal void RebuildChunk(Vector3Int chunk)
        {
            ClearChunk(chunk);
            // 分块里没有方块时，清完旧碰撞体就结束，不必生成空网格。
            if (!blocks.TryGetChunkBlocks(chunk, out List<VoxelPlatformBlock> chunkBlocks)) return;

            // 碰撞保留完整方块网格，剔除后的表面会让被遮挡的接触面留下可以穿过的缝隙。
            instances.Clear();
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
            Mesh mesh = TakeMesh();
            // 复用的网格先清空，合并结果只保留本轮实例。
            mesh.Clear();
            mesh.name = "PlatformCollision_" + chunk;
            mesh.hideFlags = HideFlags.DontSave;
            // 一个分块可能容纳上万个顶点，必须使用 32 位索引。
            mesh.indexFormat = IndexFormat.UInt32;
            // 合并数组按实例数从池里取用，长度与实例数一致才能交给 CombineMeshes。
            CombineInstance[] combine = combineBuffer.Rent(instances.Count);
            instances.CopyTo(combine);
            // 合并时把方块矩阵烘进顶点，碰撞对象本身保持单位变换。
            mesh.CombineMeshes(combine, true, true);
            combineBuffer.Return(combine);

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
        /// <summary>从复用池取碰撞对象，池空时新建对象。</summary>
        private GameObject TakeObject(string name)
        {
#if UNITY_EDITOR
            // 烘焙复用池优先：烘焙要保持平台内的序列化引用，运行时池在这条路径上已清空。
            if (bakePool != null && bakePool.Count > 0) return bakePool.Dequeue();
#endif
            return objectPool.Count > 0 ? objectPool.Pop() : new GameObject(name);
        }

        /// <summary>从网格池取合并用网格，池空时创建。</summary>
        private Mesh TakeMesh()
        {
            return meshPool.Count > 0 ? meshPool.Pop() : new Mesh();
        }

        /// <summary>把运行时碰撞对象放回复用池，池满时销毁。</summary>
        private void RecycleObject(GameObject target)
        {
            if (!target) return;
            if (objectPool.Count >= PoolCapacity)
            {
                VoxelPlatformRenderer.Release(target);
                return;
            }
            objectPool.Push(target);
        }

        /// <summary>把运行时网格放回复用池，池满时销毁。</summary>
        private void RecycleMesh(Mesh mesh)
        {
            if (!mesh) return;
            if (meshPool.Count >= PoolCapacity)
            {
                VoxelPlatformRenderer.Release(mesh);
                return;
            }
            meshPool.Push(mesh);
        }

        /// <summary>销毁池里没有被复用的对象与网格。</summary>
        private void FlushPools()
        {
            while (objectPool.Count > 0) VoxelPlatformRenderer.Release(objectPool.Pop());
            while (meshPool.Count > 0) VoxelPlatformRenderer.Release(meshPool.Pop());
        }

        /// <summary>移除单个 chunk 的碰撞映射并回收非烘焙对象。</summary>
        private void ClearChunk(Vector3Int chunk)
        {
            // 该分块从没生成过碰撞，没有可清理的对象。
            if (!Surfaces.TryGetValue(chunk, out VoxelPlatformCollisionSurface collision)) return;
            if (collision.Object && collision.Object.TryGetComponent(out Collider collider))
                colliderChunks.Remove(collider);
            // 先关闭再回收，销毁要等到帧末才生效，这一帧不能再挡路。
            if (collision.Object) collision.Object.SetActive(false);
            ColliderCount--;
            // 属于资产的烘焙碰撞只隐藏，销毁会破坏平台资产的引用。
            if (!collision.Baked)
            {
                // 池里的碰撞体不再持有合并网格，避免非活动对象继续占着烹饪结果。
                if (collision.Object && collision.Object.TryGetComponent(out MeshCollider meshCollider))
                    meshCollider.sharedMesh = null;
                RecycleObject(collision.Object);
                RecycleMesh(collision.Mesh);
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
            // 重建缓存与对象池都属于平台运行时资源，随平台一起丢弃。
            combineBuffer.Clear();
            FlushPools();
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
