using System;
using System.Collections.Generic;
using Core.Scripts.Scene.VoxelPlatform.Data;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Rendering
{
    // 只处理几何与邻接关系，缓存由同一源网格生成的六面掩码变体。
    class VoxelPlatformGeometry
    {
        // 位置键保留局部中心的小数部分，避免把半格布局或不同高度压进同一格。
        internal const int PositionPrecision = 10000;
        // 分块是重建的最小单位；8 格一块让一次改动只重算附近表面，同时保留足够的合并批量。
        internal const int ChunkSize = 8;
        // 顺序与掩码位一一对应，调整顺序会让已缓存的变体和烘焙网格对不上。
        internal static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back
        };

        // 变体按源网格加掩码缓存，同一素材的所有方块共用一份剔除后的网格。
        private readonly Dictionary<Mesh, Dictionary<int, Mesh>> meshVariants =new();

        internal static bool IsUnitCube(Mesh mesh, Matrix4x4 matrix)
        {
            // 接触面剔除依赖标准单位 Cube；缩放、斜转与镜像几何保留完整表面。
            if (mesh.vertexCount != 24 || mesh.triangles.Length != 36 ||
                (mesh.bounds.size - Vector3.one).sqrMagnitude > 0.000001f ||
                mesh.bounds.center.sqrMagnitude > 0.000001f)
                return false;
            // 三个基轴变换后必须仍是整数坐标的单位向量，方块才是轴对齐等距排布。
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
                // 位为 1 表示该面朝向空位需要绘制，六个面刚好占满一个 int 的低六位。
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
            // 六面全可见时没有面可剔，直接返回源网格，省掉一份完全相同的副本。
            if (mask == 63) return block.Mesh;
            // 首次用到该源网格时建立一层子缓存，按掩码分桶。
            if (!meshVariants.TryGetValue(block.Mesh, out Dictionary<int, Mesh> variants))
                meshVariants.Add(block.Mesh, variants = new Dictionary<int, Mesh>());
            // 同一掩码只需生成一次，同素材的其他方块直接复用。
            if (variants.TryGetValue(mask, out Mesh cached)) return cached;

            Vector3[] vertices = block.Mesh.vertices;
            int[] triangles = block.Mesh.triangles;
            var visible = new List<int>();
            // 按三角形法线归入六个轴向面；找不到对应方向的斜转表面无法参与剔除，原样保留。
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
            // 变体是平台自己创建的临时网格，清空时必须逐个释放。
            foreach (Dictionary<int, Mesh> variants in meshVariants.Values)
                foreach (Mesh mesh in variants.Values) VoxelPlatformRenderer.Release(mesh);
            meshVariants.Clear();
        }
    }
}
