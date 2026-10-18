using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Environment
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class VoxelPlatformRenderer : MonoBehaviour
    {
        private const int PositionPrecision = 10000;
        private const int ChunkSize = 8;
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        private sealed class Block
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Transform Transform;
            public GameObject Object;
            public Mesh Mesh;
            public Material Material;
            public Matrix4x4 Matrix;
            public Vector3Int Position;
            public Vector3Int Chunk;
            public bool Occludes;
            public bool Active;
            public bool OriginalForceOff;
        }

        private sealed class Surface
        {
            public GameObject Object;
            public Mesh Mesh;
            public int Triangles;
        }

        private readonly List<Block> blocks = new List<Block>();
        private readonly Dictionary<Vector3Int, Block> occupied = new Dictionary<Vector3Int, Block>();
        private readonly Dictionary<Vector3Int, List<Block>> chunks = new Dictionary<Vector3Int, List<Block>>();
        private readonly Dictionary<Vector3Int, List<Surface>> surfaces = new Dictionary<Vector3Int, List<Surface>>();
        private readonly Dictionary<Mesh, Dictionary<int, Mesh>> meshVariants = new Dictionary<Mesh, Dictionary<int, Mesh>>();
        private readonly HashSet<Vector3Int> dirtyChunks = new HashSet<Vector3Int>();
        private float nextRefresh;

        public int RenderedTriangleCount { get; private set; }
        public int SurfaceRendererCount { get; private set; }

        private void OnEnable()
        {
            if (Application.isPlaying) Rebuild();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.05f;
            RefreshChangedBlocks();
        }

        private void OnDisable()
        {
            Clear();
        }

        [ContextMenu("Rebuild Platform Surface")]
        public void Rebuild()
        {
            Clear();
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "Intact" || !filter.sharedMesh || !filter.sharedMesh.isReadable)
                    continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || filter.sharedMesh.subMeshCount != 1 ||
                    renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterial ||
                    renderer.lightmapIndex >= 0)
                    continue;

                Matrix4x4 matrix = worldToLocal * filter.transform.localToWorldMatrix;
                Vector3 center = matrix.MultiplyPoint3x4(filter.sharedMesh.bounds.center);
                var block = new Block
                {
                    Filter = filter,
                    Renderer = renderer,
                    Transform = filter.transform,
                    Object = filter.gameObject,
                    Mesh = filter.sharedMesh,
                    Material = renderer.sharedMaterial,
                    Matrix = matrix,
                    Position = Vector3Int.RoundToInt(center * PositionPrecision),
                    Chunk = new Vector3Int(Mathf.FloorToInt(center.x / ChunkSize),
                        Mathf.FloorToInt(center.y / ChunkSize), Mathf.FloorToInt(center.z / ChunkSize)),
                    Occludes = IsUnitCube(filter.sharedMesh, matrix),
                    OriginalForceOff = renderer.forceRenderingOff
                };
                block.Active = IsActive(block);
                blocks.Add(block);
                if (!chunks.TryGetValue(block.Chunk, out List<Block> list))
                    chunks.Add(block.Chunk, list = new List<Block>());
                list.Add(block);
                renderer.forceRenderingOff = true;
                block.Transform.hasChanged = false;
            }

            UpdateOccupancy();
            foreach (Vector3Int chunk in chunks.Keys) RebuildChunk(chunk);
        }

        // 破坏逻辑切换 Intact 后可立即调用；定时检查也兼容 Inspector 中的启停和移动。
        public void RefreshChangedBlocks()
        {
            if (blocks.Count == 0) return;
            dirtyChunks.Clear();
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            foreach (Block block in blocks)
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
                bool active = IsActive(block);
                if (active == block.Active) continue;
                block.Active = active;
                dirtyChunks.Add(block.Chunk);
                foreach (Vector3Int direction in Directions)
                {
                    if (occupied.TryGetValue(block.Position + direction * PositionPrecision, out Block neighbor))
                        dirtyChunks.Add(neighbor.Chunk);
                }
            }
            if (dirtyChunks.Count == 0) return;
            UpdateOccupancy();
            foreach (Vector3Int chunk in dirtyChunks) RebuildChunk(chunk);
        }

        private static bool IsActive(Block block)
        {
            return block.Object && block.Renderer && block.Object.activeInHierarchy &&
                block.Renderer.enabled && !block.OriginalForceOff;
        }

        private void UpdateOccupancy()
        {
            occupied.Clear();
            foreach (Block block in blocks)
                if (block.Occludes) occupied[block.Position] = block;
        }

        private static bool IsUnitCube(Mesh mesh, Matrix4x4 matrix)
        {
            if (mesh.vertexCount != 24 || mesh.triangles.Length != 36 ||
                (mesh.bounds.size - Vector3.one).sqrMagnitude > 0.000001f ||
                mesh.bounds.center.sqrMagnitude > 0.000001f)
                return false;
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                Vector3 transformed = matrix.MultiplyVector(axis);
                if ((transformed - (Vector3)Vector3Int.RoundToInt(transformed)).sqrMagnitude > 0.000001f ||
                    Mathf.Abs(transformed.sqrMagnitude - 1) > 0.0001f)
                    return false;
            }
            return matrix.determinant > 0;
        }

        private int VisibleMask(Block block)
        {
            int mask = 0;
            for (int index = 0; index < Directions.Length; index++)
            {
                Vector3Int offset = Vector3Int.RoundToInt(block.Matrix.MultiplyVector(Directions[index]));
                if (!block.Occludes ||
                    !occupied.TryGetValue(block.Position + offset * PositionPrecision, out Block neighbor) ||
                    !neighbor.Active)
                    mask |= 1 << index;
            }
            return mask;
        }

        private Mesh GetVariant(Block block, int mask)
        {
            if (mask == 63) return block.Mesh;
            if (!meshVariants.TryGetValue(block.Mesh, out Dictionary<int, Mesh> variants))
                meshVariants.Add(block.Mesh, variants = new Dictionary<int, Mesh>());
            if (variants.TryGetValue(mask, out Mesh cached)) return cached;

            Vector3[] vertices = block.Mesh.vertices;
            int[] triangles = block.Mesh.triangles;
            var visible = new List<int>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 normal = Vector3.Cross(vertices[triangles[index + 1]] - vertices[triangles[index]],
                    vertices[triangles[index + 2]] - vertices[triangles[index]]).normalized;
                int face = System.Array.IndexOf(Directions, Vector3Int.RoundToInt(normal));
                if (face >= 0 && (mask & (1 << face)) == 0) continue;
                visible.Add(triangles[index]);
                visible.Add(triangles[index + 1]);
                visible.Add(triangles[index + 2]);
            }
            Mesh mesh = Instantiate(block.Mesh);
            mesh.name = block.Mesh.name + "_Faces_" + mask;
            mesh.hideFlags = HideFlags.DontSave;
            mesh.SetTriangles(visible, 0);
            variants.Add(mask, mesh);
            return mesh;
        }

        private void RebuildChunk(Vector3Int chunk)
        {
            ClearChunk(chunk);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new Dictionary<Material, MeshRenderer>();
            foreach (Block block in chunks[chunk])
            {
                if (!block.Active) continue;
                int mask = VisibleMask(block);
                if (mask == 0) continue;
                if (!groups.TryGetValue(block.Material, out List<CombineInstance> instances))
                {
                    groups.Add(block.Material, instances = new List<CombineInstance>());
                    sources.Add(block.Material, block.Renderer);
                }
                instances.Add(new CombineInstance { mesh = GetVariant(block, mask), transform = block.Matrix });
            }

            var created = new List<Surface>();
            surfaces[chunk] = created;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh { name = "PlatformSurface_" + chunk, hideFlags = HideFlags.DontSave,
                    indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                var child = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(transform, false);
                MeshRenderer source = sources[group.Key];
                child.layer = source.gameObject.layer;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.lightProbeUsage = source.lightProbeUsage;
                renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                renderer.renderingLayerMask = source.renderingLayerMask;
                renderer.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
                int triangles = (int)mesh.GetIndexCount(0) / 3;
                created.Add(new Surface { Object = child, Mesh = mesh, Triangles = triangles });
                RenderedTriangleCount += triangles;
                SurfaceRendererCount++;
            }
        }

        private void ClearChunk(Vector3Int chunk)
        {
            if (!surfaces.TryGetValue(chunk, out List<Surface> list)) return;
            foreach (Surface surface in list)
            {
                RenderedTriangleCount -= surface.Triangles;
                SurfaceRendererCount--;
                surface.Object.SetActive(false);
                Release(surface.Object);
                Release(surface.Mesh);
            }
            surfaces.Remove(chunk);
        }

        private void Clear()
        {
            foreach (Block block in blocks)
                if (block.Renderer) block.Renderer.forceRenderingOff = block.OriginalForceOff;
            foreach (Vector3Int chunk in new List<Vector3Int>(surfaces.Keys)) ClearChunk(chunk);
            foreach (Dictionary<int, Mesh> variants in meshVariants.Values)
                foreach (Mesh mesh in variants.Values) Release(mesh);
            blocks.Clear();
            chunks.Clear();
            occupied.Clear();
            meshVariants.Clear();
            RenderedTriangleCount = 0;
            SurfaceRendererCount = 0;
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
