using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Destruction
{
    /// <summary>一次攻击爆炸的结算结果。</summary>
    public readonly struct AttackResult
    {
        /// <summary>本次爆炸用于结算的世界空间中心。</summary>
        public Vector3 ExplosionCenter { get; }
        /// <summary>是否存在有效爆炸中心。</summary>
        public bool HasExplosionCenter { get; }
        /// <summary>射线命中的真实表面世界坐标。</summary>
        public Vector3 SurfaceHitPoint { get; }
        /// <summary>本次结果是否包含真实表面命中点。</summary>
        public bool HasSurfaceHitPoint { get; }
        /// <summary>本次结算实际破坏的方块数量。</summary>
        public int BrokenBlockCount { get; }

        /// <summary>创建一次攻击结算结果并保存有效性标记。</summary>
        internal AttackResult(Vector3 explosionCenter, bool hasExplosionCenter,
            Vector3 surfaceHitPoint, bool hasSurfaceHitPoint, int brokenBlockCount)
        {
            ExplosionCenter = explosionCenter;
            HasExplosionCenter = hasExplosionCenter;
            SurfaceHitPoint = surfaceHitPoint;
            HasSurfaceHitPoint = hasSurfaceHitPoint;
            BrokenBlockCount = brokenBlockCount;
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
        /// 按世界位置结算水平圆柱爆炸。中心有效，但 Vector3 入口没有实际表面命中点。
        /// </summary>
        public static AttackResult Explode(Vector3 center, float radius)
        {
            if (!IsFinite(center) || !IsFinite(radius)) return default;
            int broken = BreakActivePlatforms(center, radius);
            return new AttackResult(center, true, default, false, broken);
        }

        /// <summary>
        /// 从射线最近的有效平台表面结算水平圆柱爆炸。
        /// 自身层级、碎块层、非平台碰撞体与无法解析到方块的命中都会被跳过。
        /// </summary>
        public static AttackResult Explode(Ray ray, float maxDistance, int layerMask,
            Transform ignoredRoot, float radius)
        {
            if (!IsFinite(ray.origin) || !IsFinite(ray.direction) || !IsFinite(maxDistance) ||
                !IsFinite(radius) || ray.direction.sqrMagnitude <= Mathf.Epsilon || maxDistance <= 0f)
                return default;

            ray = new Ray(ray.origin, ray.direction.normalized);
            int queryMask = layerMask & ~VoxelPlatformRenderer.DebrisMask;
            if (queryMask == 0) return default;

            float distance = maxDistance;
            int count = Physics.RaycastNonAlloc(
                ray, rayHits, distance, queryMask, QueryTriggerInteraction.Ignore);
            while (count == rayHits.Length && rayHits.Length < MaxRayHitCapacity)
            {
                Array.Resize(ref rayHits, Mathf.Min(rayHits.Length * 2, MaxRayHitCapacity));
                count = Physics.RaycastNonAlloc(
                    ray, rayHits, distance, queryMask, QueryTriggerInteraction.Ignore);
            }

            // 达到缓冲上限仍填满时，分配完整结果以免遗漏更近的有效平台命中。
            RaycastHit[] candidates = count == rayHits.Length && rayHits.Length == MaxRayHitCapacity
                ? Physics.RaycastAll(ray, distance, queryMask, QueryTriggerInteraction.Ignore)
                : rayHits;
            int candidateCount = candidates == rayHits ? count : candidates.Length;

            if (!TryFindNearestPlatformHit(candidates, candidateCount, ignoredRoot,
                    out VoxelPlatformRenderer platform, out RaycastHit hit))
                return default;

            int broken = platform.Runtime.BreakBlocksAroundWorldCenter(hit.point, radius);
            return new AttackResult(hit.point, true, hit.point, true, broken);
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
                if (!collider || IsIgnored(collider.transform, ignoredRoot) ||
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

        /// <summary>判断命中对象是否属于调用方指定的忽略层级。</summary>
        private static bool IsIgnored(Transform hitTransform, Transform ignoredRoot)
        {
            return ignoredRoot && (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot));
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
