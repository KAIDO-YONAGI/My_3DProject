using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Destruction
{
    /// <summary>
    /// 世界空间的破坏体积。标记碰撞体按世界包围盒参与判定，球形碰撞体使用缩放后的真实半径；
    /// 方块一侧取旋转后的轴对齐包围盒，整数 90 度旋转时与方块本身完全重合。
    /// </summary>
    public readonly struct VoxelPlatformBlast
    {
        private readonly Bounds bounds;
        private readonly Vector3 center;
        private readonly float radius;
        private readonly bool spherical;

        /// <summary>按世界空间轴对齐盒构造。</summary>
        public VoxelPlatformBlast(Bounds worldBounds)
        {
            bounds = worldBounds;
            center = worldBounds.center;
            radius = 0f;
            spherical = false;
        }

        /// <summary>按世界空间球构造，半径为零时只命中包含该点的方块。</summary>
        public VoxelPlatformBlast(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = Mathf.Max(0f, radius);
            bounds = new Bounds(center, Vector3.one * (this.radius * 2f));
            spherical = true;
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

        // 方块传入旋转后的世界包围盒：球体积用点到盒的距离，盒体积用相交判定。
        internal bool Overlaps(Bounds block)
        {
            return spherical
                ? block.SqrDistance(center) <= radius * radius
                : bounds.Intersects(block);
        }
    }
}
