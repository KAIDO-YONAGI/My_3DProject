using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Environment
{
    // 按分块和材质生成可见表面，协调同一分块的碰撞刷新。
    internal sealed class VoxelPlatformMeshBuilder
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformRuntime runtime;
        private readonly VoxelPlatformGeometry geometry = new VoxelPlatformGeometry();
        internal readonly Dictionary<Vector3Int, List<VoxelPlatformSurface>> Surfaces =
            new Dictionary<Vector3Int, List<VoxelPlatformSurface>>();
        internal VoxelPlatformCollisionBuilder Collisions { get; }
#if UNITY_EDITOR
        private Queue<GameObject> bakeSurfacePool;
#endif

        internal int RenderedTriangleCount { get; private set; }
        internal int SurfaceRendererCount { get; private set; }
        internal int CollisionColliderCount => Collisions.ColliderCount;

        internal VoxelPlatformMeshBuilder(VoxelPlatformRenderer owner, VoxelPlatformRuntime runtime)
        {
            this.owner = owner;
            this.runtime = runtime;
            Collisions = new VoxelPlatformCollisionBuilder(owner, runtime);
        }

        internal void ActivateRoots()
        {
            if (!owner.SurfaceRoot) owner.SurfaceRoot = owner.CreateRoot("CombinedSurface");
            owner.SurfaceRoot.gameObject.SetActive(true);
            Collisions.ActivateRoot();
        }

        internal void UseBakedChunks(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
                var chunkSurfaces = new List<VoxelPlatformSurface>(chunk.Surfaces);
                Surfaces.Add(chunk.Position, chunkSurfaces);
                foreach (VoxelPlatformSurface surface in chunkSurfaces)
                {
                    if (!surface.Object) continue;
                    surface.Baked = true;
                    surface.Object.SetActive(true);
                    RenderedTriangleCount += surface.Triangles;
                    SurfaceRendererCount++;
                }
            }

            ActivateRoots();
            Collisions.UseBakedChunks(bakedChunks);
        }

        internal void RebuildChunk(Vector3Int chunk)
        {
            ClearChunk(chunk);
            if (!runtime.Chunks.TryGetValue(chunk, out List<VoxelPlatformBlock> chunkBlocks))
                return;

            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new Dictionary<Material, VoxelPlatformBlock>();
            foreach (VoxelPlatformBlock block in chunkBlocks)
            {
                if (!block.Active || !block.Mesh || !block.Material) continue;
                int mask = VoxelPlatformGeometry.VisibleMask(block, runtime.Occupied);
                if (mask == 0) continue;
                if (!groups.TryGetValue(block.Material, out List<CombineInstance> instances))
                {
                    groups.Add(block.Material, instances = new List<CombineInstance>());
                    sources.Add(block.Material, block);
                }
                instances.Add(new CombineInstance
                {
                    mesh = geometry.GetVariant(block, mask),
                    transform = block.Matrix
                });
            }

            var created = new List<VoxelPlatformSurface>();
            Surfaces[chunk] = created;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh
                {
                    name = "PlatformSurface_" + chunk + "_" + group.Key.name,
                    hideFlags = HideFlags.DontSave,
                    indexFormat = IndexFormat.UInt32
                };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                GameObject child = TakeSurfaceObject(mesh.name);
                child.name = mesh.name;
                child.hideFlags = HideFlags.DontSave;
                child.transform.SetParent(owner.SurfaceRoot, false);
                child.SetActive(true);

                // 同材质组沿用代表方块的渲染配置，布局内应保持该配置一致。
                VoxelPlatformBlock source = sources[group.Key];
                child.layer = source.Layer;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (!filter) filter = child.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (!renderer) renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = source.ShadowCastingMode;
                renderer.receiveShadows = source.ReceiveShadows;
                renderer.lightProbeUsage = source.LightProbeUsage;
                renderer.reflectionProbeUsage = source.ReflectionProbeUsage;
                renderer.renderingLayerMask = source.RenderingLayerMask;
                renderer.allowOcclusionWhenDynamic = source.AllowOcclusionWhenDynamic;
                int triangles = (int)mesh.GetIndexCount(0) / 3;
                created.Add(new VoxelPlatformSurface
                {
                    Object = child,
                    Mesh = mesh,
                    Triangles = triangles
                });
                RenderedTriangleCount += triangles;
                SurfaceRendererCount++;
            }

            Collisions.RebuildChunk(chunk);
        }

        private GameObject TakeSurfaceObject(string name)
        {
#if UNITY_EDITOR
            if (bakeSurfacePool != null && bakeSurfacePool.Count > 0)
                return bakeSurfacePool.Dequeue();
#endif
            return new GameObject(name);
        }

        private void ClearChunk(Vector3Int chunk)
        {
            if (!Surfaces.TryGetValue(chunk, out List<VoxelPlatformSurface> list)) return;
            foreach (VoxelPlatformSurface surface in list)
            {
                RenderedTriangleCount -= surface.Triangles;
                SurfaceRendererCount--;
                if (surface.Object) surface.Object.SetActive(false);
                if (!surface.Baked)
                {
                    VoxelPlatformRenderer.Release(surface.Object);
                    VoxelPlatformRenderer.Release(surface.Mesh);
                }
            }
            Surfaces.Remove(chunk);
        }

        internal void Clear()
        {
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys))
                ClearChunk(chunk);
            Collisions.Clear();
            geometry.Clear();
            RenderedTriangleCount = 0;
            SurfaceRendererCount = 0;
            if (owner.SurfaceRoot) owner.SurfaceRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        internal void BeginBake(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            bakeSurfacePool = new Queue<GameObject>();
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
                foreach (VoxelPlatformSurface surface in chunk.Surfaces)
                    if (surface.Object) bakeSurfacePool.Enqueue(surface.Object);
            }
            Collisions.BeginBake(bakedChunks);
        }

        internal void EndBake()
        {
            while (bakeSurfacePool.Count > 0) VoxelPlatformRenderer.Release(bakeSurfacePool.Dequeue());
            bakeSurfacePool = null;
            Collisions.EndBake();
        }
#endif
    }
}
