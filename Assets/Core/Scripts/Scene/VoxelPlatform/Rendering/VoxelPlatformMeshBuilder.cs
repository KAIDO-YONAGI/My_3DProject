using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Collision;
using Core.Scripts.Scene.VoxelPlatform.Data;
using Core.Scripts.Scene.VoxelPlatform.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Scene.VoxelPlatform.Rendering
{
    // 按分块和材质生成可见表面，协调同一分块的碰撞刷新。
    class VoxelPlatformMeshBuilder
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformRuntime runtime;
        // 几何缓存按平台共享，同一分块重建时能直接命中已生成的变体。
        private readonly VoxelPlatformGeometry geometry = new();
        // 分块到表面对象的映射，清空与替换都以分块为单位进行。
        internal readonly Dictionary<Vector3Int, List<VoxelPlatformSurface>> Surfaces =new();
        internal VoxelPlatformCollisionBuilder Collisions { get; }
#if UNITY_EDITOR
        // 只在烘焙期间存在：先回收旧表面，再按分块重建，用完即销毁。
        private Queue<GameObject> bakeSurfacePool;
#endif

        // 两个计数随生成与释放同步增减，清空时归零，外部可以随时读取。
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
            // 根节点首次使用时才创建，平台之间不共享同一批子对象。
            if (!owner.SurfaceRoot) owner.SurfaceRoot = owner.CreateRoot("CombinedSurface");
            owner.SurfaceRoot.gameObject.SetActive(true);
            // 表面与碰撞必须同步启用，只开一半会出现看得见站不上或相反的情况。
            Collisions.ActivateRoot();
        }

        internal void UseBakedChunks(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            // 烘焙表面已经合并完成，直接登记引用；标记 Baked 让后续替换只隐藏，不销毁属于资产的网格。
            // 旧烘焙数据可能只保存方块记录，分块没有表面资产时退回按数据重生成。
            foreach (VoxelPlatformBakedChunk chunk in bakedChunks)
            {
                // 复制一份表面列表，运行期替换分块时不会改到组件上的序列化数据。
                var chunkSurfaces = new List<VoxelPlatformSurface>(chunk.Surfaces);
                Surfaces.Add(chunk.Position, chunkSurfaces);
                foreach (VoxelPlatformSurface surface in chunkSurfaces)
                {
                    // 引用为空的记录说明烘焙数据不完整，跳过而不是抛异常。
                    if (!surface.Object) continue;
                    surface.Baked = true;
                    // 烘焙对象在编辑时被隐藏过，加载时统一打开。
                    surface.Object.SetActive(true);
                    RenderedTriangleCount += surface.Triangles;
                    SurfaceRendererCount++;
                }
            }

            // 兼容仅保存方块记录的旧烘焙数据：没有表面资产时按数据重生成一遍。
            if (Surfaces.Count == 0)
                foreach (Vector3Int chunk in new List<Vector3Int>(runtime.Chunks.Keys))
                    RebuildChunk(chunk);

            ActivateRoots();
            Collisions.UseBakedChunks(bakedChunks);
        }

        internal void RebuildChunk(Vector3Int chunk)
        {
            // 先清掉旧表面再生成，否则同一分块会残留上一次的渲染对象。
            ClearChunk(chunk);
            // 分块里没有方块时，清完旧表面就结束，不必生成空网格。
            if (!runtime.Chunks.TryGetValue(chunk, out List<VoxelPlatformBlock> chunkBlocks))
                return;

            // 按材质分组：分块的绘制批次数量取决于材质种类，与方块数量无关。
            var groups = new Dictionary<Material, List<CombineInstance>>();
            // 每种材质记一个代表方块，稍后复制它的渲染配置。
            var sources = new Dictionary<Material, VoxelPlatformBlock>();
            foreach (VoxelPlatformBlock block in chunkBlocks)
            {
                // 已破坏、缺网格或缺材质的方块不产生表面。
                if (!block.Active || !block.Mesh || !block.Material) continue;
                int mask = VoxelPlatformGeometry.VisibleMask(block, runtime.Occupied);
                // 掩码为 0 表示六个面都被邻居挡住，整块方块不需要绘制。
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
            // 先登记空列表再填充，生成中途出错时已建好的对象仍能被清理逻辑回收。
            Surfaces[chunk] = created;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh
                {
                    name = "PlatformSurface_" + chunk + "_" + group.Key.name,
                    hideFlags = HideFlags.DontSave,
                    // 合并后顶点数通常超过 16 位索引上限，32 位索引才能完整表达。
                    indexFormat = IndexFormat.UInt32
                };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                GameObject child = TakeSurfaceObject(mesh.name);
                child.name = mesh.name;
                // 生成物随平台销毁，不被场景或 Prefab 保存。
                child.hideFlags = HideFlags.DontSave;
                child.transform.SetParent(owner.SurfaceRoot, false);
                child.SetActive(true);

                // 同材质组沿用代表方块的渲染配置，布局内应保持该配置一致。
                // 烘焙把这份渲染配置连同网格一起存进资产，运行加载时直接还原。
                VoxelPlatformBlock source = sources[group.Key];
                child.layer = source.Layer;
                // 复用的池对象可能已经带组件，缺哪个补哪个。
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
                // 三角形数在生成时算好，卸载时直接回减，不必再遍历网格。
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

        // 烘焙时复用旧表面对象，避免每轮烘焙都新建一批子对象。
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
            // 该分块从没生成过表面，没有可清理的对象。
            if (!Surfaces.TryGetValue(chunk, out List<VoxelPlatformSurface> list)) return;
            foreach (VoxelPlatformSurface surface in list)
            {
                RenderedTriangleCount -= surface.Triangles;
                SurfaceRendererCount--;
                // 先关闭再销毁：Destroy 要等到帧末才生效，提前关闭可避免这一帧继续参与渲染。
                if (surface.Object) surface.Object.SetActive(false);
                // 属于资产的烘焙对象只隐藏不销毁，销毁会破坏平台资产的引用。
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
            // 复制键集合再遍历，ClearChunk 会从字典里移除当前项。
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys))
                ClearChunk(chunk);
            // 碰撞与表面一起收，否则会留下看不见但能挡路的碰撞体。
            Collisions.Clear();
            // 变体网格是运行时临时对象，必须随平台一起释放。
            geometry.Clear();
            RenderedTriangleCount = 0;
            SurfaceRendererCount = 0;
            // 根节点关闭，场景层级里不留空容器。
            if (owner.SurfaceRoot) owner.SurfaceRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        internal void BeginBake(List<VoxelPlatformBakedChunk> bakedChunks)
        {
            // 烘焙会重建同一分块的表面，先收回旧对象供重建复用，平台内的序列化引用因此保持不变。
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
            // 池里剩余的旧对象这次没有被复用，烘焙结束后统一销毁。
            while (bakeSurfacePool.Count > 0) VoxelPlatformRenderer.Release(bakeSurfacePool.Dequeue());
            bakeSurfacePool = null;
            Collisions.EndBake();
        }
#endif
    }
}
