using System.Collections.Generic;
using Core.Scripts.Environment.VoxelPlatform.Data;
using Core.Scripts.Environment.VoxelPlatform.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Environment.VoxelPlatform.Collision
{
    // 使用完整方块网格生成静态分块碰撞，与可见面剔除保持独立。
    internal sealed class VoxelPlatformCollisionBuilder
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformRuntime runtime;
        internal readonly Dictionary<Vector3Int, VoxelPlatformCollisionSurface> Surfaces =
            new Dictionary<Vector3Int, VoxelPlatformCollisionSurface>();
#if UNITY_EDITOR
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
            if (!owner.CollisionRoot) owner.CollisionRoot = owner.CreateRoot("CombinedCollision");
            owner.CollisionRoot.gameObject.SetActive(true);
        }

        internal void UseBakedChunks(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
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
            if (!runtime.Chunks.TryGetValue(chunk, out List<VoxelPlatformBlock> chunkBlocks)) return;

            var instances = new List<CombineInstance>();
            VoxelPlatformBlock source = null;
            foreach (VoxelPlatformBlock block in chunkBlocks)
            {
                if (!block.Active || !block.Mesh) continue;
                source = source ?? block;
                instances.Add(new CombineInstance { mesh = block.Mesh, transform = block.Matrix });
            }

            if (instances.Count == 0) return;
            var mesh = new Mesh
            {
                name = "PlatformCollision_" + chunk,
                hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt32
            };
            mesh.CombineMeshes(instances.ToArray(), true, true);

            GameObject child = TakeObject(mesh.name);
            child.name = mesh.name;
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(owner.CollisionRoot, false);
            child.layer = source != null ? source.Layer : owner.gameObject.layer;
            child.SetActive(true);
            MeshCollider collider = child.GetComponent<MeshCollider>();
            if (!collider) collider = child.AddComponent<MeshCollider>();
            collider.convex = false;
            // 清空后重新赋值，使复用的碰撞对象采用本次生成的网格。
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            Surfaces[chunk] = new VoxelPlatformCollisionSurface { Object = child, Mesh = mesh };
            ColliderCount++;
        }

        private GameObject TakeObject(string name)
        {
#if UNITY_EDITOR
            if (bakePool != null && bakePool.Count > 0) return bakePool.Dequeue();
#endif
            return new GameObject(name);
        }

        private void ClearChunk(Vector3Int chunk)
        {
            if (!Surfaces.TryGetValue(chunk, out VoxelPlatformCollisionSurface collision)) return;
            if (collision.Object) collision.Object.SetActive(false);
            ColliderCount--;
            if (!collision.Baked)
            {
                VoxelPlatformRenderer.Release(collision.Object);
                VoxelPlatformRenderer.Release(collision.Mesh);
            }
            Surfaces.Remove(chunk);
        }

        internal void Clear()
        {
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys)) ClearChunk(chunk);
            ColliderCount = 0;
            if (owner.CollisionRoot) owner.CollisionRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        internal void BeginBake(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            bakePool = new Queue<GameObject>();
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
                if (chunk.Collision != null && chunk.Collision.Object)
                    bakePool.Enqueue(chunk.Collision.Object);
        }

        internal void EndBake()
        {
            while (bakePool.Count > 0) VoxelPlatformRenderer.Release(bakePool.Dequeue());
            bakePool = null;
        }
#endif
    }
}
