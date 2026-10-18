using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment
{
    // 只处理几何与邻接关系，缓存由同一源网格生成的六面掩码变体。
    internal sealed class VoxelPlatformGeometry
    {
        // 位置键保留局部中心的小数部分，避免把半格布局或不同高度压进同一格。
        internal const int PositionPrecision = 10000;
        internal const int ChunkSize = 8;
        internal static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        private readonly Dictionary<Mesh, Dictionary<int, Mesh>> meshVariants =
            new Dictionary<Mesh, Dictionary<int, Mesh>>();

        internal static bool IsUnitCube(Mesh mesh, Matrix4x4 matrix)
        {
            // 接触面剔除依赖标准单位 Cube；缩放、斜转与镜像几何保留完整表面。
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

        internal static int VisibleMask(VoxelPlatformBlock block,
            Dictionary<Vector3Int, VoxelPlatformBlock> occupied)
        {
            int mask = 0;
            for (int index = 0; index < Directions.Length; index++)
            {
                // 掩码以源网格面为单位，邻居查询方向则转换到平台局部坐标系。
                Vector3Int offset = Vector3Int.RoundToInt(block.Matrix.MultiplyVector(Directions[index]));
                if (!block.Occludes ||
                    !occupied.TryGetValue(block.Position + offset * PositionPrecision,
                        out VoxelPlatformBlock neighbor) ||
                    !neighbor.Active)
                    mask |= 1 << index;
            }
            return mask;
        }

        internal Mesh GetVariant(VoxelPlatformBlock block, int mask)
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
                int face = Array.IndexOf(Directions, Vector3Int.RoundToInt(normal));
                if (face >= 0 && (mask & (1 << face)) == 0) continue;
                visible.Add(triangles[index]);
                visible.Add(triangles[index + 1]);
                visible.Add(triangles[index + 2]);
            }

            // 保留 UV、法线等顶点属性，只替换索引；共享源 Mesh 始终保持原样。
            Mesh mesh = UnityEngine.Object.Instantiate(block.Mesh);
            mesh.name = block.Mesh.name + "_Faces_" + mask;
            mesh.hideFlags = HideFlags.DontSave;
            mesh.SetTriangles(visible, 0);
            variants.Add(mask, mesh);
            return mesh;
        }

        internal void Clear()
        {
            foreach (Dictionary<int, Mesh> variants in meshVariants.Values)
                foreach (Mesh mesh in variants.Values) VoxelPlatformRenderer.Release(mesh);
            meshVariants.Clear();
        }
    }
}
