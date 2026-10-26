using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Destruction
{
    /// <summary>
    /// 世界空间的破坏体积。标记碰撞体按世界包围盒参与判定，球形碰撞体使用缩放后的真实半径；
    /// 水平圆按世界 XZ 圆形加固定高度区间判定。
    /// 方块一侧取旋转后的轴对齐包围盒，整数 90 度旋转时与方块本身完全重合。
    /// </summary>
    public readonly struct VoxelPlatformBlast
    {
        private readonly Bounds bounds;
        private readonly Vector3 center;
        private readonly float radius;
        private readonly bool spherical;
        private readonly bool horizontal;
        private readonly float minHeight;
        private readonly float maxHeight;

        /// <summary>按世界空间轴对齐盒构造。</summary>
        public VoxelPlatformBlast(Bounds worldBounds)
            : this(worldBounds, worldBounds.center, 0f, false, false, 0f, 0f)
        {
        }

        /// <summary>按世界空间球构造，半径为零时只命中包含该点的方块。</summary>
        public VoxelPlatformBlast(Vector3 center, float radius)
            : this(new Bounds(center, Vector3.one * (Mathf.Max(0f, radius) * 2f)),
                center, Mathf.Max(0f, radius), true, false, 0f, 0f)
        {
        }

        private VoxelPlatformBlast(Bounds bounds, Vector3 center, float radius, bool spherical, bool horizontal,
            float minHeight, float maxHeight)
        {
            this.bounds = bounds;
            this.center = center;
            this.radius = radius;
            this.spherical = spherical;
            this.horizontal = horizontal;
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
        }

        /// <summary>世界水平圆：XZ 内为圆，垂直方向限定在给定高度区间内。</summary>
        internal static VoxelPlatformBlast Horizontal(Vector3 center, float radius, float minHeight, float maxHeight)
        {
            // 半径与高度区间都按非负处理，退化体积只命中真正覆盖到的方块。
            float clamped = Mathf.Max(0f, radius);
            float top = Mathf.Max(minHeight, maxHeight);
            return new VoxelPlatformBlast(
                new Bounds(new Vector3(center.x, (minHeight + top) * 0.5f, center.z),
                    new Vector3(clamped * 2f, top - minHeight, clamped * 2f)),
                center, clamped, false, true, minHeight, top);
        }

        /// <summary>由标记碰撞体构造，球形按真实半径，其余形状取世界包围盒。</summary>
        public static VoxelPlatformBlast FromCollider(Collider marker)
        {
            if (marker is SphereCollider sphere)
            {
                Transform owner = sphere.transform;
                Vector3 scale = owner.lossyScale;
                float scaled = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                return new VoxelPlatformBlast(owner.TransformPoint(sphere.center), sphere.radius * scaled);
            }

            return new VoxelPlatformBlast(marker.bounds);
        }

        /// <summary>体积的世界包围盒，用于把多份体积合并成保守的查询范围。</summary>
        internal Bounds WorldBounds => bounds;

        // 方块传入旋转后的世界包围盒：球体积用点到盒的距离，盒体积用相交判定。
        internal bool Overlaps(Bounds block)
        {
            if (horizontal) return OverlapsHorizontal(block);
            return spherical
                ? block.SqrDistance(center) <= radius * radius
                : bounds.Intersects(block);
        }

        // 水平圆与方块包围盒：XZ 取圆到盒的最近距离，高度取区间重叠。
        private bool OverlapsHorizontal(Bounds block)
        {
            float x = Mathf.Clamp(center.x, block.min.x, block.max.x);
            float z = Mathf.Clamp(center.z, block.min.z, block.max.z);
            float dx = center.x - x;
            float dz = center.z - z;
            if (dx * dx + dz * dz > radius * radius) return false;
            // 高度区间必须与方块重叠，相邻层的方块不会被同一次爆炸带走。
            return block.max.y >= minHeight && block.min.y <= maxHeight;
        }
    }
}
