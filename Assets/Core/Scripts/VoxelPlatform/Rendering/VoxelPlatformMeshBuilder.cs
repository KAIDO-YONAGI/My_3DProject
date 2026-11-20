using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Collision;
using Core.Scripts.VoxelPlatform.Data;
using Core.Scripts.VoxelPlatform.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.VoxelPlatform.Rendering
{
    /// <summary>按分块和材质生成可见表面，并协调对应分块的碰撞刷新。</summary>
    class VoxelPlatformMeshBuilder
    {
        // 回收池上限：稳态下破坏与重建的对象数量持平，超过上限说明碰上短时集中重建，继续留住只占内存。
        private const int PoolCapacity = 64;

        private readonly VoxelPlatformRenderer owner;
        private readonly VoxelPlatformBlockTable blocks;
        // 几何缓存按平台共享，同一分块重建时能直接命中已生成的变体。
        private readonly VoxelPlatformGeometry geometry = new();
        // 分块到表面对象的映射，清空与替换都以分块为单位进行。
        internal readonly Dictionary<Vector3Int, List<VoxelPlatformSurface>> Surfaces =new();
        internal VoxelPlatformCollisionBuilder Collisions { get; }

        // 重建用的分组容器按轮复用，避免每次重建都分配字典与列表。
        private readonly Dictionary<Material, List<CombineInstance>> groups = new();
        private readonly Dictionary<Material, VoxelPlatformBlock> sources = new();
        private readonly Stack<List<CombineInstance>> groupLists = new();
        private readonly VoxelPlatformCombineBuffer combineBuffer = new();
        // 运行时生成的表面对象与网格回收后留在池里，下一次重建直接取用。
        private readonly Stack<GameObject> surfacePool = new();
        private readonly Stack<Mesh> meshPool = new();
#if UNITY_EDITOR
        // 只在烘焙期间存在：先回收旧表面，再按分块重建，用完即销毁。
        private Queue<GameObject> bakeSurfacePool;
#endif

        // 两个计数随生成与释放同步增减，清空时归零，外部可以随时读取。
        internal int RenderedTriangleCount { get; private set; }
        internal int SurfaceRendererCount { get; private set; }
        internal int CollisionColliderCount => Collisions.ColliderCount;

        /// <summary>绑定平台与方块表，并创建对应的碰撞构建器。</summary>
        internal VoxelPlatformMeshBuilder(VoxelPlatformRenderer owner, VoxelPlatformBlockTable blocks)
        {
            this.owner = owner;
            this.blocks = blocks;
            Collisions = new VoxelPlatformCollisionBuilder(owner, blocks);
        }

        /// <summary>创建并启用表面与碰撞根节点。</summary>
        internal void ActivateRoots()
        {
            // 根节点首次使用时才创建，平台之间不共享同一批子对象。
            if (!owner.SurfaceRoot) owner.SurfaceRoot = owner.CreateRoot("CombinedSurface");
            owner.SurfaceRoot.gameObject.SetActive(true);
            // 表面与碰撞必须同步启用，只开一半会出现看得见站不上或相反的情况。
            Collisions.ActivateRoot();
        }

        /// <summary>登记烘焙表面并加载对应 chunk 的碰撞。</summary>
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
            if (Surfaces.Count == 0) blocks.ForEachChunk(RebuildChunk);

            ActivateRoots();
            Collisions.UseBakedChunks(bakedChunks);
        }

        /// <summary>按材质重建一个 chunk 的可见表面和完整碰撞。</summary>
        internal void RebuildChunk(Vector3Int chunk)
        {
            // 上一轮的分组容器先还池，即便上一次重建中途抛异常，这一轮也从干净状态开始。
            ReleaseGroups();
            // 先清掉旧表面再生成，否则同一分块会残留上一次的渲染对象。
            ClearChunk(chunk);
            // 分块里没有方块时，清完旧表面就结束，不必生成空网格。
            if (!blocks.TryGetChunkBlocks(chunk, out List<VoxelPlatformBlock> chunkBlocks))
                return;

            // 按材质分组：分块的绘制批次数量取决于材质种类，与方块数量无关。
            foreach (VoxelPlatformBlock block in chunkBlocks)
            {
                // 已破坏、缺网格或缺材质的方块不产生表面。
                if (!block.Active || !block.Mesh || !block.Material) continue;
                int mask = VoxelPlatformGeometry.VisibleMask(block, blocks.Occupied);
                // 掩码为 0 表示六个面都被邻居挡住，整块方块不需要绘制。
                if (mask == 0) continue;
                if (!groups.TryGetValue(block.Material, out List<CombineInstance> instances))
                {
                    groups.Add(block.Material, instances = TakeGroupList());
                    sources.Add(block.Material, block);
                }
                instances.Add(new CombineInstance
                {
                    mesh = geometry.GetVariant(block, mask),
                    transform = block.Matrix
                });
            }

            // 表面列表会被烘焙数据直接引用，每轮新建一份，不参与回收复用。
            var created = new List<VoxelPlatformSurface>();
            // 先登记空列表再填充，生成中途出错时已建好的对象仍能被清理逻辑回收。
            Surfaces[chunk] = created;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                Mesh mesh = TakeMesh();
                // 复用的网格先清空，合并结果只保留本轮实例。
                mesh.Clear();
                mesh.name = "PlatformSurface_" + chunk + "_" + group.Key.name;
                // 生成物随平台销毁，不被场景或 Prefab 保存。
                mesh.hideFlags = HideFlags.DontSave;
                // 合并后顶点数通常超过 16 位索引上限，32 位索引才能完整表达。
                mesh.indexFormat = IndexFormat.UInt32;
                // 合并数组按实例数从池里取用，长度与实例数一致才能交给 CombineMeshes。
                CombineInstance[] combine = combineBuffer.Rent(group.Value.Count);
                group.Value.CopyTo(combine);
                mesh.CombineMeshes(combine, true, true);
                combineBuffer.Return(combine);

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

            // 本轮分组列表清空还池，字典保留容量供下一轮重建。
            ReleaseGroups();
            Collisions.RebuildChunk(chunk);
        }

        // 烘焙时复用旧表面对象，避免每轮烘焙都新建一批子对象。
        /// <summary>从复用池取表面对象，池空时创建对象。</summary>
        private GameObject TakeSurfaceObject(string name)
        {
#if UNITY_EDITOR
            // 烘焙复用池优先：烘焙要保持平台内的序列化引用，运行时池在这条路径上已清空。
            if (bakeSurfacePool != null && bakeSurfacePool.Count > 0)
                return bakeSurfacePool.Dequeue();
#endif
            return surfacePool.Count > 0 ? surfacePool.Pop() : new GameObject(name);
        }

        /// <summary>从网格池取合并用网格，池空时创建。</summary>
        private Mesh TakeMesh()
        {
            return meshPool.Count > 0 ? meshPool.Pop() : new Mesh();
        }

        /// <summary>从分组列表池取一个空列表，池空时创建。</summary>
        private List<CombineInstance> TakeGroupList()
        {
            return groupLists.Count > 0 ? groupLists.Pop() : new List<CombineInstance>();
        }

        /// <summary>把本轮分组列表清空后还池，字典只保留容量。</summary>
        private void ReleaseGroups()
        {
            foreach (List<CombineInstance> list in groups.Values)
            {
                list.Clear();
                groupLists.Push(list);
            }
            groups.Clear();
            sources.Clear();
        }

        /// <summary>把运行时表面对象放回复用池，池满时销毁。</summary>
        private void RecycleSurface(GameObject surface)
        {
            if (!surface) return;
            if (surfacePool.Count >= PoolCapacity)
            {
                VoxelPlatformRenderer.Release(surface);
                return;
            }
            // 池里的占位对象不再持有渲染资源，网格与材质由下一轮重建重新赋值。
            if (surface.TryGetComponent(out MeshFilter filter)) filter.sharedMesh = null;
            if (surface.TryGetComponent(out MeshRenderer renderer)) renderer.sharedMaterial = null;
            surfacePool.Push(surface);
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
            while (surfacePool.Count > 0) VoxelPlatformRenderer.Release(surfacePool.Pop());
            while (meshPool.Count > 0) VoxelPlatformRenderer.Release(meshPool.Pop());
        }

        /// <summary>停用并回收一个 chunk 的旧表面。</summary>
        private void ClearChunk(Vector3Int chunk)
        {
            // 该分块从没生成过表面，没有可清理的对象。
            if (!Surfaces.TryGetValue(chunk, out List<VoxelPlatformSurface> list)) return;
            foreach (VoxelPlatformSurface surface in list)
            {
                RenderedTriangleCount -= surface.Triangles;
                SurfaceRendererCount--;
                // 先关闭再回收：销毁要等到帧末才生效，提前关闭可避免这一帧继续参与渲染。
                if (surface.Object) surface.Object.SetActive(false);
                // 属于资产的烘焙对象只隐藏不销毁，销毁会破坏平台资产的引用。
                if (surface.Baked) continue;
                RecycleSurface(surface.Object);
                RecycleMesh(surface.Mesh);
            }
            // 列表可能被烘焙数据直接引用，这里只解除分块映射，不清空内容。
            Surfaces.Remove(chunk);
        }

        /// <summary>释放所有表面、碰撞和网格变体并关闭根节点。</summary>
        internal void Clear()
        {
            // 复制键集合再遍历，ClearChunk 会从字典里移除当前项。
            foreach (Vector3Int chunk in new List<Vector3Int>(Surfaces.Keys))
                ClearChunk(chunk);
            // 碰撞与表面一起收，否则会留下看不见但能挡路的碰撞体。
            Collisions.Clear();
            // 先断开分组列表对变体网格的引用，再释放变体网格。
            ReleaseGroups();
            // 变体网格是运行时临时对象，必须随平台一起释放。
            geometry.Clear();
            // 重建缓存与对象池都属于平台运行时资源，随平台一起丢弃。
            combineBuffer.Clear();
            FlushPools();
            RenderedTriangleCount = 0;
            SurfaceRendererCount = 0;
            // 根节点关闭，场景层级里不留空容器。
            if (owner.SurfaceRoot) owner.SurfaceRoot.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>收集本轮烘焙可复用的旧表面对象并启动碰撞复用。</summary>
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

        /// <summary>销毁未复用的旧表面对象并结束表面与碰撞复用。</summary>
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
