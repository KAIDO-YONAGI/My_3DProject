using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Scene.VoxelPlatform.Data
{
    /// <summary>保存单个方块的运行状态、几何索引、渲染设置和可选源对象引用。</summary>
    class VoxelPlatformBlock
    {
        // 源引用只在编辑布局里有效，烘焙时不会写进数据。
        public MeshRenderer Renderer;
        public Transform Transform;
        public GameObject Object;
        // 碎块 Prefab 要保留到烘焙数据里，运行时破坏后还要实例化。
        public GameObject FragmentPrefab;
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Matrix; // Intact 网格局部空间到平台局部空间。
        public Matrix4x4 FragmentMatrix; // Cube 容器到平台局部空间，保留 Intact 自身偏移。
        // 位置键是放大后的整数，用于索引和掩码比较；对外接口仍使用局部中心坐标。
        public Vector3Int Position;
        // 分块由位置键换算而来，是重建和烘焙的单位。
        public Vector3Int Chunk;
        // 只有标准单位 Cube 才遮挡邻居，其余几何始终保留全部表面。
        public bool Occludes;
        public bool Active;
        // 源渲染的原始开关，退出预览时按它恢复。
        public bool OriginalForceOff;
        // 合并后的表面沿用源方块的渲染配置，这里逐项抄一份。
        public int Layer;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public uint RenderingLayerMask;
        public bool AllowOcclusionWhenDynamic;

        public VoxelPlatformBakedBlock ToBaked()
        {
            // 转烘焙记录时丢弃源引用，只保留可序列化的落格和渲染配置。
            return new VoxelPlatformBakedBlock
            {
                FragmentPrefab = FragmentPrefab,
                Mesh = Mesh,
                Material = Material,
                Matrix = Matrix,
                FragmentMatrix = FragmentMatrix,
                Position = Position,
                Chunk = Chunk,
                Occludes = Occludes,
                Active = Active,
                Layer = Layer,
                ShadowCastingMode = ShadowCastingMode,
                ReceiveShadows = ReceiveShadows,
                LightProbeUsage = LightProbeUsage,
                ReflectionProbeUsage = ReflectionProbeUsage,
                RenderingLayerMask = RenderingLayerMask,
                AllowOcclusionWhenDynamic = AllowOcclusionWhenDynamic
            };
        }
    }

    /// <summary>保存可内联序列化到平台组件的单块烘焙数据。</summary>
    [Serializable]
    class VoxelPlatformBakedBlock
    {
        public GameObject FragmentPrefab;
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Matrix;
        public Matrix4x4 FragmentMatrix;
        public Vector3Int Position;
        public Vector3Int Chunk;
        public bool Occludes;
        public bool Active;
        public int Layer;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public uint RenderingLayerMask;
        public bool AllowOcclusionWhenDynamic;

        public VoxelPlatformBlock ToRuntimeBlock()
        {
            // 从烘焙数据还原运行记录，源对象引用保持为空。
            return new VoxelPlatformBlock
            {
                FragmentPrefab = FragmentPrefab,
                Mesh = Mesh,
                Material = Material,
                Matrix = Matrix,
                FragmentMatrix = FragmentMatrix,
                Position = Position,
                Chunk = Chunk,
                Occludes = Occludes,
                Active = Active,
                Layer = Layer,
                ShadowCastingMode = ShadowCastingMode,
                ReceiveShadows = ReceiveShadows,
                LightProbeUsage = LightProbeUsage,
                ReflectionProbeUsage = ReflectionProbeUsage,
                RenderingLayerMask = RenderingLayerMask,
                AllowOcclusionWhenDynamic = AllowOcclusionWhenDynamic
            };
        }
    }

    /// <summary>记录一个分块表面对象及其网格、三角形统计和烘焙资产状态。</summary>
    [Serializable]
    class VoxelPlatformSurface
    {
        public GameObject Object;
        public Mesh Mesh;
        // 生成时算好，卸载时直接回减统计，不再遍历网格。
        public int Triangles;
        // true 表示对象属于烘焙资产，替换时只能隐藏。
        public bool Baked;
    }

    /// <summary>记录一个分块碰撞对象及其网格和烘焙资产状态。</summary>
    [Serializable]
    class VoxelPlatformCollisionSurface
    {
        public GameObject Object;
        public Mesh Mesh;
        // true 表示碰撞对象属于烘焙资产，替换时只能隐藏。
        public bool Baked;
    }

    /// <summary>保存一个 chunk 的全部材质表面和合并碰撞记录。</summary>
    [Serializable]
    class VoxelPlatformBakedChunk
    {
        public Vector3Int Position;
        // 同一分块可能按多种材质拆成多个表面，所以这里存列表。
        public List<VoxelPlatformSurface> Surfaces;
        // 分块内所有方块合成一份碰撞，没有方块时为空。
        public VoxelPlatformCollisionSurface Collision;
    }
}
