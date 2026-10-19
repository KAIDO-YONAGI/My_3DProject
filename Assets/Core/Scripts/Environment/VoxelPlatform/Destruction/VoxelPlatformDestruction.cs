using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Destruction
{
    /// <summary>
    /// 破坏体积的结算入口。调用方提交标记碰撞体或体积后立即结算，
    /// 每个已启用平台只遍历一次方块、只重建一次受影响分块。
    /// </summary>
    public static class VoxelPlatformDestruction
    {
        // 已启用的平台在启用时登记，结算时不必扫描场景。
        private static readonly List<VoxelPlatformRenderer> platforms = new();
        // 单份体积与标记数组共用的转换缓冲；结算同步执行，不重入。
        private static readonly List<VoxelPlatformBlast> blasts = new();

        /// <summary>
        /// 按一组标记碰撞体破坏它们覆盖的方块，返回实际破坏数量。
        /// collisionDuration 为碎块从生成起保持碰撞的时长，省略时沿用 Prefab 上的配置。
        /// </summary>
        public static int Break(IReadOnlyList<Collider> markers, float? collisionDuration = null)
        {
            if (markers == null || markers.Count == 0) return 0;
            blasts.Clear();
            for (int index = 0; index < markers.Count; index++)
            {
                Collider marker = markers[index];
                if (marker) blasts.Add(VoxelPlatformBlast.FromCollider(marker));
            }

            return Break(blasts, collisionDuration);
        }

        /// <summary>按单个标记碰撞体破坏，返回实际破坏数量。</summary>
        public static int Break(Collider marker, float? collisionDuration = null)
        {
            if (!marker) return 0;
            blasts.Clear();
            blasts.Add(VoxelPlatformBlast.FromCollider(marker));
            return Break(blasts, collisionDuration);
        }

        /// <summary>按世界空间盒破坏，返回实际破坏数量。</summary>
        public static int Break(Bounds worldBounds, float? collisionDuration = null)
        {
            blasts.Clear();
            blasts.Add(new VoxelPlatformBlast(worldBounds));
            return Break(blasts, collisionDuration);
        }

        /// <summary>按世界空间球破坏，返回实际破坏数量。</summary>
        public static int Break(Vector3 center, float radius, float? collisionDuration = null)
        {
            blasts.Clear();
            blasts.Add(new VoxelPlatformBlast(center, radius));
            return Break(blasts, collisionDuration);
        }

        /// <summary>按一组世界空间体积破坏，返回实际破坏数量。</summary>
        public static int Break(IReadOnlyList<VoxelPlatformBlast> volumes, float? collisionDuration = null)
        {
            if (volumes == null || volumes.Count == 0) return 0;
            int broken = 0;
            for (int index = platforms.Count - 1; index >= 0; index--)
            {
                VoxelPlatformRenderer platform = platforms[index];
                // 已销毁的平台从登记表清理；停用的平台运行数据已清空，不参与结算。
                if (!platform)
                {
                    platforms.RemoveAt(index);
                    continue;
                }

                if (!platform.isActiveAndEnabled) continue;
                broken += platform.BreakBlocksIn(volumes, collisionDuration);
            }

            return broken;
        }

        internal static void Register(VoxelPlatformRenderer platform)
        {
            if (!platform || platforms.Contains(platform)) return;
            platforms.Add(platform);
        }

        internal static void Unregister(VoxelPlatformRenderer platform)
        {
            platforms.Remove(platform);
        }
    }
}
