#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment
{
    // 生成可序列化的烘焙结果；AssetDatabase 持久化由编辑器 Baker 负责。
    internal static class VoxelPlatformBaking
    {
        internal static Mesh[] Bake(VoxelPlatformRenderer owner, VoxelPlatformRuntime runtime)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("请在编辑模式预生成平台网格。");
            if (!VoxelPlatformSourceCollector.HasSourceGeometry(owner))
                throw new InvalidOperationException("请从保留 Intact 的编辑用平台生成烘焙数据。");

            runtime.Clear();
            VoxelPlatformMeshBuilder builder = runtime.Meshes;
            // 复用旧烘焙子对象，维持平台内的序列化对象引用。
            builder.BeginBake(owner.BakedChunks);
            owner.BakedBlocks.Clear();
            owner.BakedChunks.Clear();
            var meshes = new List<Mesh>();
            try
            {
                runtime.Rebuild();
                owner.SurfaceRoot.gameObject.hideFlags = HideFlags.None;
                owner.CollisionRoot.gameObject.hideFlags = HideFlags.None;
                foreach (VoxelPlatformBlock block in runtime.Blocks)
                    owner.BakedBlocks.Add(block.ToBaked());

                foreach (KeyValuePair<Vector3Int, List<VoxelPlatformSurface>> chunk in builder.Surfaces)
                {
                    foreach (VoxelPlatformSurface surface in chunk.Value)
                    {
                        surface.Baked = true;
                        surface.Object.hideFlags = HideFlags.None;
                        surface.Mesh.hideFlags = HideFlags.None;
                        meshes.Add(surface.Mesh);
                    }

                    VoxelPlatformCollisionSurface collision = null;
                    if (builder.Collisions.Surfaces.TryGetValue(chunk.Key,
                            out VoxelPlatformCollisionSurface generatedCollision))
                    {
                        collision = generatedCollision;
                        collision.Baked = true;
                        collision.Object.hideFlags = HideFlags.None;
                        collision.Mesh.hideFlags = HideFlags.None;
                        meshes.Add(collision.Mesh);
                    }

                    owner.BakedChunks.Add(new VoxelPlatformBakedChunk
                    {
                        Position = chunk.Key,
                        Surfaces = chunk.Value,
                        Collision = collision
                    });
                }
            }
            finally
            {
                builder.EndBake();
                runtime.Clear();
            }
            return meshes.ToArray();
        }
    }
}
#endif
