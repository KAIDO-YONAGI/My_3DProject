#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Core.Scripts.Environment.VoxelPlatform.Data;
using Core.Scripts.Environment.VoxelPlatform.Rendering;
using Core.Scripts.Environment.VoxelPlatform.Runtime;
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Baking
{
    // 生成可序列化的烘焙结果；AssetDatabase 持久化由编辑器 Baker 负责。
    static class VoxelPlatformBaking
    {
        internal static Mesh[] Bake(VoxelPlatformRenderer owner, VoxelPlatformRuntime runtime)
        {
            // 烘焙会写序列化字段和资产，播放中执行会让运行状态和磁盘数据分叉。
            if (Application.isPlaying)
                throw new InvalidOperationException("请在编辑模式预生成平台网格。");
            // 没有源 Cube 就没有可采集的几何，重复烘焙只会清掉已有结果。
            if (!owner.HasBakeSources)
                throw new InvalidOperationException("请从保留 Intact 的编辑用平台生成烘焙数据。");

            // 先清掉上一轮运行表面，避免旧表面混进本次烘焙的分块统计。
            runtime.Clear();
            VoxelPlatformMeshBuilder builder = runtime.Meshes;
            // 复用旧烘焙子对象，维持平台内的序列化对象引用。
            builder.BeginBake(owner.BakedChunks);
            // 先清旧记录，本次烘焙只写当前源布局的结果。
            owner.BakedBlocks.Clear();
            owner.BakedChunks.Clear();
            var meshes = new List<Mesh>();
            try
            {
                runtime.Rebuild();
                // 根节点创建时带 DontSave，烘焙产物要写进资产，这里必须去掉该标记。
                owner.SurfaceRoot.gameObject.hideFlags = HideFlags.None;
                owner.CollisionRoot.gameObject.hideFlags = HideFlags.None;
                // 存档只保留运行时需要的字段，源组件引用不写入烘焙数据。
                foreach (VoxelPlatformBlock block in runtime.Blocks)
                    owner.BakedBlocks.Add(block.ToBaked());

                foreach (KeyValuePair<Vector3Int, List<VoxelPlatformSurface>> chunk in builder.Surfaces)
                {
                    // 按分块保存：一个分块一份表面列表加一份碰撞，重建时以分块为单位整体替换。
                    foreach (VoxelPlatformSurface surface in chunk.Value)
                    {
                        // 去掉 DontSave，网格随后作为子资产写入平台资产。
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
                // 失败时同样要收回烘焙池并清空运行表面，否则残留对象会污染下一次烘焙。
                builder.EndBake();
                runtime.Clear();
            }
            return meshes.ToArray();
        }
    }
}
#endif
