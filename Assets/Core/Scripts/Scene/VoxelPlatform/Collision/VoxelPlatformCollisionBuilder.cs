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
#if UNITY_EDITOR
        // 只在烘焙期间存在，用于复用上一轮烘焙的碰撞对象。
        private Queue<GameObject> bakePool;
#endif

        internal int ColliderCount { get; private set; }

        internal VoxelPlatformCollisionBuilder(VoxelPlatformRenderer owner, VoxelPlatformRuntime runtime)
        {
            this.owner = owner;
            this.runtime = runtime;
        }

        internal void ActivateRoot()
        {
            // 碰撞根与表面根一样按需创建，平台之间互不干扰。
            if (!owner.CollisionRoot) owner.CollisionRoot = owner.CreateRoot("CombinedCollision");
            owner.CollisionRoot.gameObject.SetActive(true);
        }

        internal void UseBakedChunks(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
                // 没有保存碰撞的分块跳过，稍后由兜底分支重新生成。
                if (chunk.Collision == null || !chunk.Collision.Object) continue;
                chunk.Collision.Baked = true;
                Surfaces[chunk.Position] = chunk.Collision;
                chunk.Collision.Object.SetActive(true);
                ColliderCount++;
            }

            // 兼容仅保存表面的烘焙数据；已有碰撞数据时直接复用资产。
            if (Surfaces.Count == 0)
                foreach (Vector3Int chunk in new List<Vector3Int>(runtime.Chunks.Keys))
                    RebuildChunk(chunk);
        }

        internal void RebuildChunk(Vector3Int chunk)
        {
            ClearChunk(chunk);
            // 分块里没有方块时，清完旧碰撞体就结束，不必生成空网格。
            if (!runtime.Chunks.TryGetValue(chunk, out List<VoxelPlatformBlock> chunkBlocks)) return;

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
            Surfaces[chunk] = new VoxelPlatformCollisionSurface { Object = child, Mesh = mesh };
            ColliderCount++;
        }

        // 烘焙时复用旧碰撞对象，避免每轮烘焙都新建一批子对象。
        private GameObject TakeObject(string name)
        {
#if UNITY_EDITOR
            if (bakePool != null && bakePool.Count > 0) return bakePool.Dequeue();
#endif
            return new GameObject(name);
        }

        private void ClearChunk(Vector3Int chunk)
        {
            // 该分块从没生成过碰撞，没有可清理的对象。
            if (!Surfaces.TryGetValue(chunk, out VoxelPlatformCollisionSurface collision)) return;
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

        internal void Clear()
        {
            // 复制键集合再遍历，ClearChunk 会从字典里移除当前项。
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys)) ClearChunk(chunk);
            ColliderCount = 0;
            // 根节点关闭，场景层级里不留空容器。
            if (owner.CollisionRoot) owner.CollisionRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        internal void BeginBake(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            // 先收回旧碰撞对象供重建复用，平台内的碰撞引用因此保持不变。
            bakePool = new Queue<GameObject>();
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
                if (chunk.Collision != null && chunk.Collision.Object)
                    bakePool.Enqueue(chunk.Collision.Object);
        }

        internal void EndBake()
        {
            // 池里剩余的旧对象这次没有被复用，烘焙结束后统一销毁。
            while (bakePool.Count > 0) VoxelPlatformRenderer.Release(bakePool.Dequeue());
            bakePool = null;
        }
#endif
    }
}
