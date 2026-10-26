using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Destruction
{
    /// <summary>一次攻击爆炸的结算结果。</summary>
    public readonly struct AttackResult
    {
        /// <summary>本次爆炸中心；射线未命中有效平台时为空。</summary>
        public Vector3? ExplosionCenter { get; }

        /// <summary>本次结算实际破坏的方块数量。</summary>
        public int BrokenBlockCount { get; }
        /// <summary>本次爆炸使用的有效世界空间水平半径。</summary>
        public float Radius { get; }

        /// <summary>创建一次攻击结算结果并保存位置、半径和破坏数量。</summary>
        internal AttackResult(Vector3? explosionCenter,
            int brokenBlockCount, float radius)
        {
            ExplosionCenter = explosionCenter;
            BrokenBlockCount = brokenBlockCount;
            Radius = radius;
        }
    }

    /// <summary>
    /// 攻击统一通过位置或射线提交水平爆炸；区域触发器保留独立的碰撞体入口。
    /// </summary>
    public static class VoxelPlatformDestruction
    {
        // 已启用的平台在启用时登记，结算时不必扫描场景。
        private static readonly List<VoxelPlatformRenderer> platforms = new();
        // 区域触发器的体积转换缓冲；结算同步执行，不重入。
        private static readonly List<VoxelPlatformBlast> blasts = new();
        private const int InitialRayHitCapacity = 16;
        private const int MaxRayHitCapacity = 256;
        private static RaycastHit[] rayHits = new RaycastHit[InitialRayHitCapacity];

        /// <summary>
        /// 按世界触发点和水平半径结算圆柱爆炸。
        /// </summary>
        public static AttackResult ExplodeAt(Vector3 triggerPoint, float radius)
        {
            if (!IsFinite(triggerPoint) || !IsFinite(radius)) return default;
            float effectiveRadius = Mathf.Max(0f, radius);
            int broken = BreakActivePlatforms(triggerPoint, effectiveRadius);
            return new AttackResult(triggerPoint, broken, effectiveRadius);
        }

        /// <summary>从射线最近的有效平台命中位置结算水平圆柱爆炸。</summary>
        /// <param name="ray">用于查找可破坏平台的世界空间射线。</param>
        /// <param name="maxDistance">射线检测的最大世界距离。</param>
        /// <param name="layerMask">允许射线检测的物理层；碎块层会自动排除。</param>
        /// <param name="ignoredRoot">射线需要忽略的对象根节点及其子层级。</param>
        /// <param name="radius">以命中位置为中心的世界 XZ 水平爆炸半径。</param>
        /// <returns>包含命中中心、实际半径和破坏数量；没有有效平台命中时中心为空。</returns>
        public static AttackResult ExplodeFromRay(Ray ray, float maxDistance, int layerMask,
            Transform ignoredRoot, float radius)
        {
            // 拒绝无效射线参数，避免无意义的物理查询或非有限坐标进入爆炸筛选。
            if (!IsFinite(ray.origin) || !IsFinite(ray.direction) || !IsFinite(maxDistance) ||
                !IsFinite(radius) || ray.direction.sqrMagnitude <= Mathf.Epsilon || maxDistance <= 0f)
                return default;

            // 负半径按零处理，并规范方向以保证检测距离始终按世界单位计算。
            float effectiveRadius = Mathf.Max(0f, radius);
            ray = new Ray(ray.origin, ray.direction.normalized);
            // 碎块只用于表现，不应遮挡射线寻找可破坏的平台。
            int queryMask = layerMask & ~VoxelPlatformRenderer.DebrisMask;

            // 先用可复用缓冲收集命中，只有缓冲被填满时才扩容重查。
            int count = Physics.RaycastNonAlloc(
                ray, rayHits, maxDistance, queryMask, QueryTriggerInteraction.Ignore);
            // 缓冲被填满说明命中可能被截断；扩容后重查，直到命中完整或达到容量上限。
            while (count == rayHits.Length && rayHits.Length < MaxRayHitCapacity)
            {
                Array.Resize(ref rayHits, Mathf.Min(rayHits.Length * 2, MaxRayHitCapacity));
                count = Physics.RaycastNonAlloc(
                    ray, rayHits, maxDistance, queryMask, QueryTriggerInteraction.Ignore);
            }

            // 达到缓冲上限仍填满时，分配完整结果以免遗漏更近的有效平台命中。
            RaycastHit[] candidates = rayHits;
            int candidateCount = count;
            if (count == rayHits.Length && rayHits.Length == MaxRayHitCapacity)
            {
                // 固定缓冲仍被填满时读取完整命中，确保最近的有效平台不会被截掉。
                candidates = Physics.RaycastAll(ray, maxDistance, queryMask, QueryTriggerInteraction.Ignore);
                candidateCount = candidates.Length;
            }

            if (!TryFindNearestPlatformHit(candidates, candidateCount, ignoredRoot,
                    out VoxelPlatformRenderer platform, out RaycastHit hit))
                return new AttackResult(null, 0, effectiveRadius);

            // 使用最近有效平台的实际命中坐标作为中心，并仅结算该平台。
            int broken = platform.Runtime.BreakBlocksAroundWorldCenter(hit.point, effectiveRadius);
            return new AttackResult(hit.point, broken, effectiveRadius);
        }

        /// <summary>把一组标记碰撞体转换为区域体积，并在活动平台上批量结算。</summary>
        public static int BreakRegions(IReadOnlyList<Collider> markers, float? collisionDuration = null)
        {
            if (markers == null || markers.Count == 0) return 0;
            blasts.Clear();
            for (int index = 0; index < markers.Count; index++)
            {
                Collider marker = markers[index];
                if (marker) blasts.Add(VoxelPlatformBlast.FromCollider(marker));
            }

            int broken = 0;
            for (int index = platforms.Count - 1; index >= 0; index--)
            {
                VoxelPlatformRenderer platform = platforms[index];
                if (!platform)
                {
                    platforms.RemoveAt(index);
                    continue;
                }

                if (platform.isActiveAndEnabled)
                    broken += platform.Runtime.BreakBlocksIn(blasts, collisionDuration);
            }

            return broken;
        }

        /// <summary>从物理命中中选出最近且能解析到活动平台方块的命中。</summary>
        private static bool TryFindNearestPlatformHit(RaycastHit[] candidates, int count,
            Transform ignoredRoot, out VoxelPlatformRenderer nearestPlatform, out RaycastHit nearestHit)
        {
            nearestPlatform = null;
            nearestHit = default;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = candidates[index];
                Collider collider = candidate.collider;
                if (!collider || (ignoredRoot &&
                    (collider.transform == ignoredRoot || collider.transform.IsChildOf(ignoredRoot))) ||
                    candidate.distance >= nearestDistance)
                    continue;

                VoxelPlatformRenderer platform = collider.GetComponentInParent<VoxelPlatformRenderer>();
                if (!platform || !platform.isActiveAndEnabled ||
                    !platform.Runtime.TryGetBlockPosition(candidate, out _))
                    continue;

                nearestDistance = candidate.distance;
                nearestPlatform = platform;
                nearestHit = candidate;
            }

            return nearestPlatform;
        }

        /// <summary>检查三维坐标的分量是否都是有限数值。</summary>
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        /// <summary>检查标量是否不是 NaN 或正负无穷。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>在登记的活动平台上按同一世界中心结算攻击爆炸。</summary>
        private static int BreakActivePlatforms(Vector3 center, float radius)
        {
            int broken = 0;
            for (int index = platforms.Count - 1; index >= 0; index--)
            {
                VoxelPlatformRenderer platform = platforms[index];
                if (!platform)
                {
                    platforms.RemoveAt(index);
                    continue;
                }

                if (platform.isActiveAndEnabled)
                    broken += platform.Runtime.BreakBlocksAroundWorldCenter(center, radius);
            }

            return broken;
        }

        /// <summary>将启用的平台加入跨平台世界爆炸使用的登记表。</summary>
        internal static void Register(VoxelPlatformRenderer platform)
        {
            if (!platform || platforms.Contains(platform)) return;
            platforms.Add(platform);
        }

        /// <summary>从跨平台爆炸登记表移除停用的平台。</summary>
        internal static void Unregister(VoxelPlatformRenderer platform)
        {
            platforms.Remove(platform);
        }
    }
}
