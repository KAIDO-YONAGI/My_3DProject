using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Fragments
{
    /// <summary>
    /// 破坏后的碎块表现：为每个预切片开启刚体并沿离开容器的方向抛出，超时后连同整个碎块分组一起销毁。
    /// 挂在源 Cube Prefab 的 Fragments 节点上，平台创建实例并启用该节点时触发。
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentBurst : MonoBehaviour
    {
        [Tooltip("每个碎块离开容器中心的速度，单位为米每秒。")]
        [SerializeField, Min(0f)] private float outwardSpeed = 2f;
        [Tooltip("附加在碎块上的向上速度，单位为米每秒。")]
        [SerializeField, Min(0f)] private float upwardSpeed = 1f;
        [Tooltip("碎块飞离原方块体积前关闭碰撞的时长，单位为秒。")]
        [SerializeField, Min(0f)] private float escapeTime = 0.25f;
        [Tooltip("碎块分组从启用到销毁的时长，单位为秒。")]
        [SerializeField, Min(0.1f)] private float lifetime = 6f;

        private readonly List<Collider> pieceColliders = new();
        // 爆破接口按次指定的有碰撞时长；未指定时沿用 escapeTime 的无碰撞窗口。
        private float collisionDuration = -1f;

        private void OnEnable()
        {
            // 编辑模式下平台同样会切换节点启停，只有播放时才需要刚体与抛出。
            if (!Application.isPlaying) return;

            pieceColliders.Clear();
            // 指定了有碰撞时长时，碎块生成即有碰撞，到期再关；否则沿用原来的无碰撞窗口。
            bool collideFromSpawn = collisionDuration >= 0f;
            Vector3 origin = transform.position;
            foreach (Transform node in transform)
            {
                // 只有带凸 MeshCollider 的预切片参与碎裂，其余子节点保持原样。
                MeshCollider pieceCollider = node.GetComponent<MeshCollider>();
                if (!pieceCollider) continue;

                // 预切片初始正好填满原方块的体积，与相邻方块零距离贴面。
                pieceCollider.enabled = collideFromSpawn || escapeTime <= 0f;
                pieceColliders.Add(pieceCollider);

                Rigidbody body = node.GetComponent<Rigidbody>();
                if (!body) body = node.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = false;
                body.useGravity = true;

                // 外法线取碎片相对容器中心的方向，容器自身不参与物理计算。
                Vector3 outward = node.position - origin;
                if (outward.sqrMagnitude > 0f) outward.Normalize();
                body.AddForce(outward * outwardSpeed + Vector3.up * upwardSpeed, ForceMode.VelocityChange);
            }

            if (collideFromSpawn) Invoke(nameof(DisablePieceCollisions), collisionDuration);
            else if (escapeTime > 0f) Invoke(nameof(EnablePieceCollisions), escapeTime);
            // 碎块分组归平台管理，超时统一销毁，场景里不留下持续的刚体。
            Destroy(gameObject, lifetime);
        }

        // 飞离原方块体积后恢复碰撞，碎块才会落在平台上而不是穿过去。
        private void EnablePieceCollisions()
        {
            foreach (Collider pieceCollider in pieceColliders)
                if (pieceCollider) pieceCollider.enabled = true;
        }

        // 有碰撞时长到期：碎块退出物理，但仍保留到 lifetime 结束。
        private void DisablePieceCollisions()
        {
            foreach (Collider pieceCollider in pieceColliders)
                if (pieceCollider) pieceCollider.enabled = false;
        }

        /// <summary>覆盖本次实例的有碰撞时长，供爆破接口按次指定；须在碎片节点启用前调用。</summary>
        public void OverrideCollisionDuration(float seconds)
        {
            collisionDuration = Mathf.Max(0f, seconds);
        }
    }
}
