using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Core.Scripts.Environment.VoxelPlatform.Data
{
    // 运行记录兼容源对象预览；从烘焙数据加载时，源组件引用保持为空。
    internal sealed class VoxelPlatformBlock
    {
        public MeshRenderer Renderer;
        public Transform Transform;
        public GameObject Object;
        public GameObject FragmentPrefab;
        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 Matrix; // Intact 网格局部空间到平台局部空间。
        public Matrix4x4 FragmentMatrix; // Cube 容器到平台局部空间，保留 Intact 自身偏移。
        public Vector3Int Position;
        public Vector3Int Chunk;
        public bool Occludes;
        public bool Active;
        public bool OriginalForceOff;
        public int Layer;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public uint RenderingLayerMask;
        public bool AllowOcclusionWhenDynamic;

        public VoxelPlatformBakedBlock ToBaked()
        {
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

    // 直接内联序列化到平台组件，字段名与现有烘焙资源保持一致。
    [Serializable]
    internal sealed class VoxelPlatformBakedBlock
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

    // Baked 网格属于资产；运行中替换表面时只隐藏它，临时网格才执行销毁。
    [Serializable]
    internal sealed class VoxelPlatformSurface
    {
        public GameObject Object;
        public Mesh Mesh;
        public int Triangles;
        public bool Baked;
    }

    [Serializable]
    internal sealed class VoxelPlatformCollisionSurface
    {
        public GameObject Object;
        public Mesh Mesh;
        public bool Baked;
    }

    [Serializable]
    internal sealed class VoxelPlatformBakedChunk
    {
        public Vector3Int Position;
        public List<VoxelPlatformSurface> Surfaces;
        public VoxelPlatformCollisionSurface Collision;
    }
}
