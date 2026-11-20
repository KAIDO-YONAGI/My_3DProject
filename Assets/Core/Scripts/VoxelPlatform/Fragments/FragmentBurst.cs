using System.Collections.Generic;
using UnityEngine;

namespace Core.Scripts.VoxelPlatform.Fragments
{
    /// <summary>
    /// 破坏后的碎块表现：为每个预切片开启刚体并沿离开容器的方向抛出，寿命结束时交还平台回收。
    /// 挂在 Cube 或纯碎片 Prefab 的 Fragments 节点上，平台创建实例并启用该节点时触发。
    /// </summary>
    [DisallowMultipleComponent]
    public class FragmentBurst : MonoBehaviour
    {
        [Tooltip("每个碎块离开容器中心的速度，单位为米每秒。")] [SerializeField, Min(0f)]
        private float outwardSpeed = 2f;

        [Tooltip("附加在碎块上的向上速度，单位为米每秒。")] [SerializeField, Min(0f)]
        private float upwardSpeed = 1f;

        [Tooltip("碎块飞离原方块体积前关闭碰撞的时长，单位为秒。")] [SerializeField, Min(0f)]
        private float escapeTime = 0.25f;

        [Tooltip("碎块分组从启用到归还平台的时长，单位为秒。")] [SerializeField, Min(0.1f)]
        private float lifetime = 6f;

        private readonly List<Collider> pieceColliders = new();
        // 参与碎裂的预切片、初始局部姿态与对应刚体；复用实例每次启用前按这些记录复位。
        private readonly List<Transform> pieces = new();
        private readonly List<Rigidbody> bodies = new();
        private readonly List<Vector3> restPositions = new();
        private readonly List<Quaternion> restRotations = new();

        // 爆破接口按次指定的有碰撞时长；未指定时沿用 escapeTime 的无碰撞窗口。
        private float collisionDuration = -1f;
        // 平台回收入口与实例根节点；未绑定时保留自销毁路径。
        private VoxelPlatformFragments recycler;
        private GameObject instance;

        private void OnEnable()
        {
            // 编辑模式下平台同样会切换节点启停，只有播放时才需要刚体与抛出。
            if (!Application.isPlaying) return;

            RestorePieces();
            pieceColliders.Clear();
            // 指定了有碰撞时长时，碎块生成即有碰撞，到期再关；否则沿用原来的无碰撞窗口。
            bool collideFromSpawn = collisionDuration > 0f;
            Vector3 origin = transform.position;
            for (int index = 0; index < pieces.Count; index++)
            {
                Transform node = pieces[index];
                // 只有带凸 MeshCollider 的预切片参与碎裂，其余子节点保持原样。
                MeshCollider pieceCollider = node.GetComponent<MeshCollider>();
                if (!pieceCollider) continue;

                // 预切片初始正好填满原方块的体积，与相邻方块零距离贴面。
                pieceCollider.enabled = collideFromSpawn || escapeTime <= 0f;
                pieceColliders.Add(pieceCollider);

                Rigidbody body = bodies[index];
                if (!body)
                {
                    body = node.gameObject.AddComponent<Rigidbody>();
                    bodies[index] = body;
                }

                body.isKinematic = false;
                body.useGravity = true;
                // 外法线取碎片相对容器中心的方向，容器自身不参与物理计算。
                Vector3 outward = node.position - origin;
                if (outward.sqrMagnitude > 0f) outward.Normalize();
                body.AddForce(outward * outwardSpeed + Vector3.up * upwardSpeed, ForceMode.VelocityChange);
            }

            if (collideFromSpawn) Invoke(nameof(DisablePieceCollisions), collisionDuration);
            else if (escapeTime > 0f) Invoke(nameof(EnablePieceCollisions), escapeTime);
            // 碎块实例归平台池管理：寿命结束交还平台停用复位，不再销毁自身。
            Invoke(nameof(Recycle), lifetime);
        }

        private void OnDisable()
        {
            // 停用来自平台回收、方块恢复或平台销毁：取消本轮计时并停掉刚体，复用时不继承上一轮状态。
            CancelInvoke();
            collisionDuration = -1f;
            for (int index = 0; index < bodies.Count; index++)
            {
                Rigidbody body = bodies[index];
                if (!body) continue;
                // kinematic 刚体不接受速度写入：先清零速度再退出物理。
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
            }
        }

        // 首次启用时记录预切片的初始局部姿态，之后每次启用先复位再施力。
        private void RestorePieces()
        {
            if (pieces.Count == 0)
            {
                foreach (Transform node in transform)
                    if (node.GetComponent<MeshCollider>())
                    {
                        pieces.Add(node);
                        bodies.Add(null);
                        restPositions.Add(node.localPosition);
                        restRotations.Add(node.localRotation);
                    }

                return;
            }

            for (int index = 0; index < pieces.Count; index++)
            {
                Rigidbody body = bodies[index];
                if (body)
                {
                    // kinematic 刚体不接受速度写入：先清零速度再退出物理，复位结果不会被上一轮模拟覆盖。
                    if (!body.isKinematic)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }

                    body.isKinematic = true;
                }

                pieces[index].localPosition = restPositions[index];
                pieces[index].localRotation = restRotations[index];
            }
        }

        // 寿命结束时交还平台；独立使用时没有平台可归还，按原行为销毁自身。
        private void Recycle()
        {
            if (recycler != null && instance)
            {
                recycler.RecycleInstance(instance);
                return;
            }

            Destroy(gameObject);
        }

        // 飞离原方块体积后恢复碰撞，碎块才会落在平台上而不是穿过去。
        private void EnablePieceCollisions()
        {
            foreach (Collider pieceCollider in pieceColliders)
                if (pieceCollider)
                    pieceCollider.enabled = true;
        }

        // 有碰撞时长到期：碎块退出物理，但仍保留到寿命结束。
        private void DisablePieceCollisions()
        {
            foreach (Collider pieceCollider in pieceColliders)
                if (pieceCollider)
                    pieceCollider.enabled = false;
        }

        /// <summary>覆盖本次实例的有碰撞时长，供爆破接口按次指定；须在碎片节点启用前调用。</summary>
        public void OverrideCollisionDuration(float seconds)
        {
            collisionDuration = Mathf.Max(0f, seconds);
        }

        /// <summary>绑定平台回收入口与实例根节点；须在碎片节点启用前调用。</summary>
        internal void Bind(VoxelPlatformFragments owner, GameObject fragmentInstance)
        {
            recycler = owner;
            instance = fragmentInstance;
        }
    }
}
