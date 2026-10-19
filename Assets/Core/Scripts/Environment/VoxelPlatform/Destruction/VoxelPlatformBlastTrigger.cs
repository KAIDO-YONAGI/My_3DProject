using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Destruction
{
    /// <summary>
    /// 触发器驱动的破坏入口：本物体上的触发器负责开火，标记碰撞体负责划出破碎区域，
    /// 触发时同步结算，不依赖每帧轮询。触发器要与静态平台产生物理事件，
    /// 触发器自身或进入它的物体上需要有 Rigidbody。
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Environment/Voxel Platform Blast Trigger")]
    public sealed class VoxelPlatformBlastTrigger : MonoBehaviour
    {
        [SerializeField] private Rigidbody platform;
        [Tooltip("划出破碎区域的标记碰撞体，留空时使用子物体上的碰撞体。")]
        [SerializeField] private Collider[] regions = System.Array.Empty<Collider>();
        [Tooltip("碎块从生成起保持碰撞的时长，单位为秒，到期后关闭碰撞。")]
        [SerializeField] private float collisionDuration = 0.25f;
        [Tooltip("触发一次后忽略后续事件，平台重建碰撞体后不会重复破坏。")]
        [SerializeField] private bool oneShot = true;

        private readonly List<Collider> resolved = new();
        private bool fired;

        private void OnTriggerEnter(Collider other)
        {
            Fire();
            platform.isKinematic = false;
        }

        /// <summary>立即按区域结算一次破坏，返回实际破坏的方块数量。</summary>
        private int Fire()
        {
            if (oneShot && fired) return 0;
            fired = true;
            CollectRegions(resolved);
            return resolved.Count == 0 ? 0 : VoxelPlatformDestruction.Break(resolved, collisionDuration);
        }

        // 留空时退到子物体上的碰撞体；仍然找不到就把触发器自己当成区域。
        private void CollectRegions(List<Collider> results)
        {
            results.Clear();
            if (regions != null && regions.Length > 0)
            {
                foreach (Collider region in regions)
                    if (region) results.Add(region);
                return;
            }

            Collider self = GetComponent<Collider>();
            foreach (Collider child in GetComponentsInChildren<Collider>(true))
            {
                if (child == self) continue;
                results.Add(child);
            }

            if (results.Count == 0 && self) results.Add(self);
        }
    }
}
