using System;
using System.Collections.Generic;
using Core.Scripts.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Core.Scripts.Scene.VoxelPlatform.Destruction
{
    /// <summary>一次攻击爆炸的结算结果。</summary>
    public readonly struct AttackResult
    {
        /// <summary>本次爆炸中心；射线未命中有效平台时为空。</summary>
        public Vector3? ExplosionCenter { get; }

        /// <summary>本次爆炸中心相对目标平台根节点的位置。</summary>
        public Vector3 PlatformLocalPosition { get; }

        /// <summary>命中并结算的平台稳定标识。</summary>
        public int PlatformId { get; }

        /// <summary>本次结算实际破坏的方块数量。</summary>
        public int BrokenBlockCount { get; }

        /// <summary>本次爆炸使用的有效世界空间水平半径。</summary>
        public float Radius { get; }

        /// <summary>创建包含世界中心、平台局部中心和结算数据的攻击结果。</summary>
        internal AttackResult(Vector3? explosionCenter, Vector3 platformLocalPosition,
            int platformId, int brokenBlockCount, float radius)
        {
            ExplosionCenter = explosionCenter;
            PlatformLocalPosition = platformLocalPosition;
            PlatformId = platformId;
            BrokenBlockCount = brokenBlockCount;
            Radius = radius;
        }
    }

    /// <summary>维护跨场景平台注册表，并执行单平台攻击与区域破坏结算。</summary>
    public sealed class VoxelPlatformDestruction : YSingleton<VoxelPlatformDestruction>
    {
        private const int InitialRayHitCapacity = 16;
        private const int MaxRayHitCapacity = 256;

        /// <summary>保存平台运行时注册信息及其所属场景，用于按场景卸载清理。</summary>
        private sealed class RegisteredPlatform
        {
            public VoxelPlatformRenderer Renderer;
            public int Id;
            public UnityScene Scene;
        }

        [SerializeField] private PlatformRegisterEventSO platformRegisterEvent;

        // 查询缓冲和批处理状态属于场景服务实例，不由静态类跨场景共享。
        private RaycastHit[] rayHits = new RaycastHit[InitialRayHitCapacity];
        private readonly List<RegisteredPlatform> platforms = new();
        private readonly List<VoxelPlatformBlast> blasts = new();
        private readonly HashSet<VoxelPlatformRenderer> uniquePlatforms = new();

        /// <summary>订阅平台注册事件和 Unity 场景卸载事件，建立运行时平台注册表。</summary>
        protected override void OnSingletonInitialized()
        {
            base.OnSingletonInitialized();
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            if (platformRegisterEvent == null)
            {
                Debug.LogError("未配置平台注册事件，跨场景平台不会进入破坏注册表。", this);
                return;
            }

            platformRegisterEvent.PlatformRegistrationRequested += RegisterPlatform;
        }

        /// <summary>解除事件订阅并让单例基类清理当前实例引用。</summary>
        protected override void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (platformRegisterEvent != null)
                platformRegisterEvent.PlatformRegistrationRequested -= RegisterPlatform;

            base.OnDestroy();
        }

        /// <summary>校验平台注册请求并将平台、稳定 ID 和所属场景加入运行时表。</summary>
        private void RegisterPlatform(VoxelPlatformRenderer platform, int platformId, UnityScene scene)
        {
            if (platform == null || platformId <= 0 || !scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("拒绝无效的平台注册请求。", this);
                return;
            }

            RegisteredPlatform existingPlatform = null;
            for (int index = 0; index < platforms.Count; index++)
            {
                RegisteredPlatform registered = platforms[index];
                if (registered.Renderer == platform)
                {
                    existingPlatform = registered;
                    continue;
                }

                if (registered.Id == platformId)
                {
                    Debug.LogError("平台列表存在重复 Platform Id，拒绝注册：" + platformId, platform);
                    return;
                }
            }

            if (existingPlatform != null)
            {
                existingPlatform.Id = platformId;
                existingPlatform.Scene = scene;
                return;
            }

            platforms.Add(new RegisteredPlatform
            {
                Renderer = platform,
                Id = platformId,
                Scene = scene
            });
        }

        /// <summary>场景卸载时由注册表持有方移除该场景注册的平台。</summary>
        private void OnSceneUnloaded(UnityScene unloadedScene)
        {
            for (int index = platforms.Count - 1; index >= 0; index--)
            {
                if (platforms[index].Scene.handle == unloadedScene.handle)
                    platforms.RemoveAt(index);
            }
        }

        /// <summary>按平台根节点局部触发点、世界水平半径和平台注册 ID 结算爆炸。</summary>
        public AttackResult ExplodeAt(Vector3 platformLocalPosition, float radius, int platformId)
        {
            if (!IsFinite(platformLocalPosition) || !IsFinite(radius) || platformId <= 0)
                return default;

            VoxelPlatformRenderer platform = FindRegisteredPlatform(platformId);
            if (!platform) return default;

            float effectiveRadius = Mathf.Max(0f, radius);
            Vector3 worldCenter = platform.transform.TransformPoint(platformLocalPosition);
            int broken = platform.Runtime.BreakBlocksAroundWorldCenter(worldCenter, effectiveRadius);
            return new AttackResult(worldCenter, platformLocalPosition, platformId, broken, effectiveRadius);
        }

        /// <summary>从最近有效的平台射线命中结算爆炸，并返回目标平台标识与表面命中点。</summary>
        /// <param name="ray">用于查找可破坏平台的世界空间射线。</param>
        /// <param name="maxDistance">射线检测的最大世界距离。</param>
        /// <param name="layerMask">允许射线检测的物理层；碎块层会自动排除。</param>
        /// <param name="ignoredRoot">射线需要忽略的对象根节点及其子层级。</param>
        /// <param name="radius">以命中位置为中心的世界 XZ 水平爆炸半径。</param>
        /// <returns>包含命中中心、目标平台、实际半径和破坏数量；无有效平台命中时为空结果。</returns>
        public AttackResult ExplodeFromRay(Ray ray, float maxDistance, int layerMask,
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

            // 达到缓冲上限仍填满时，读取完整结果以免遗漏最近的平台命中。
            RaycastHit[] candidates = rayHits;
            int candidateCount = count;
            if (count == rayHits.Length && rayHits.Length == MaxRayHitCapacity)
            {
                candidates = Physics.RaycastAll(ray, maxDistance, queryMask, QueryTriggerInteraction.Ignore);
                candidateCount = candidates.Length;
            }

            if (!TryFindNearestPlatformHit(candidates, candidateCount, ignoredRoot,
                    out VoxelPlatformRenderer platform, out RaycastHit hit))
                return new AttackResult(null, default, 0, 0, effectiveRadius);

            // 命中点作为中心，只调用命中平台自己的分块结算。
            int broken = platform.Runtime.BreakBlocksAroundWorldCenter(hit.point, effectiveRadius);
            int platformId = GetRegisteredPlatformId(platform);
            Vector3 platformLocalPosition = platform.transform.InverseTransformPoint(hit.point);
            return new AttackResult(hit.point, platformLocalPosition, platformId, broken, effectiveRadius);
        }

        /// <summary>把区域标记转换为体积，再只结算与这些体积物理重叠的平台。</summary>
        public int BreakRegions(IReadOnlyList<Collider> markers, float? collisionDuration = null)
        {
            if (markers == null || markers.Count == 0) return 0;

            blasts.Clear();
            uniquePlatforms.Clear();
            for (int index = 0; index < markers.Count; index++)
            {
                Collider marker = markers[index];
                if (!marker) continue;

                blasts.Add(VoxelPlatformBlast.FromCollider(marker));
            }

            int broken = 0;
            for (int index = 0; index < platforms.Count; index++)
            {
                VoxelPlatformRenderer platform = platforms[index].Renderer;
                if (platform && platform.isActiveAndEnabled && uniquePlatforms.Add(platform))
                    broken += platform.Runtime.BreakBlocksIn(blasts, collisionDuration);
            }

            return broken;
        }

        /// <summary>从物理命中中选出最近且能解析到活动平台方块的命中。</summary>
        private bool TryFindNearestPlatformHit(RaycastHit[] candidates, int count,
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
                    !IsRegistered(platform) ||
                    !platform.Runtime.TryGetBlockPosition(candidate, out _))
                    continue;

                nearestDistance = candidate.distance;
                nearestPlatform = platform;
                nearestHit = candidate;
            }

            return nearestPlatform;
        }

        /// <summary>检查射线命中的平台是否已由场景服务显式登记。</summary>
        private bool IsRegistered(VoxelPlatformRenderer platform)
        {
            for (int index = 0; index < platforms.Count; index++)
            {
                if (platforms[index].Renderer == platform) return true;
            }

            return false;
        }

        /// <summary>返回显式注册的平台 ID；未登记时返回零。</summary>
        private int GetRegisteredPlatformId(VoxelPlatformRenderer platform)
        {
            for (int index = 0; index < platforms.Count; index++)
            {
                RegisteredPlatform registered = platforms[index];
                if (registered.Renderer == platform)
                    return registered.Id;
            }

            return 0;
        }

        /// <summary>按网络 ID 从显式注册表中查找唯一活动平台。</summary>
        private VoxelPlatformRenderer FindRegisteredPlatform(int platformId)
        {
            VoxelPlatformRenderer match = null;
            for (int index = 0; index < platforms.Count; index++)
            {
                RegisteredPlatform registered = platforms[index];
                if (registered.Id != platformId || !registered.Renderer ||
                    !registered.Renderer.isActiveAndEnabled)
                    continue;

                if (match && match != registered.Renderer)
                {
                    Debug.LogError("平台列表中存在重复 Platform Id，拒绝结算：" + platformId, this);
                    return null;
                }

                match = registered.Renderer;
            }

            return match;
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
    }
}
