using System.Collections.Generic;
using Core.Scripts.VoxelPlatform.Data;
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
#endif

namespace Core.Scripts.VoxelPlatform.Fragments
{
    /// <summary>按需创建、复用和回收方块碎块实例，并管理碎块相对平台的装配位置。</summary>
    class VoxelPlatformFragments
    {
        // 闲置实例上限按来源 Prefab 分别计算，超出上限的实例直接销毁，避免长期驻留占用内存。
        private const int PoolCapacity = 16;

        private readonly VoxelPlatformRenderer owner;
        // 已破坏方块到碎块实例的映射，恢复和清空时据此回收。
        private readonly Dictionary<Vector3Int, GameObject> spawned = new();
        // 活动实例到方块位置与来源 Prefab 的反查，寿命结束回调时据此归还到对应池。
        private readonly Dictionary<GameObject, ActiveFragment> active = new();
        // 闲置实例按来源 Prefab 分池，不同材质类型的碎块不会互相顶替。
        private readonly Dictionary<GameObject, Stack<GameObject>> pool = new();

        private readonly struct ActiveFragment
        {
            public readonly Vector3Int Position;
            public readonly GameObject Prefab;

            public ActiveFragment(Vector3Int position, GameObject prefab)
            {
                Position = position;
                Prefab = prefab;
            }
        }

        internal VoxelPlatformFragments(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
        }

        internal void Spawn(VoxelPlatformBlock block, float? collisionDuration = null)
        {
            // 同一方块重复破坏只保留一个实例，避免碎块叠加。
            if (spawned.ContainsKey(block.Position) || !block.FragmentPrefab)
                return;
            // 容器首次破坏时才创建，整片平台不破坏就不产生碎块节点。
            if (!owner.FragmentRoot)
                owner.FragmentRoot = owner.CreateRoot("FragmentsRuntime");

            GameObject instance = Take(block.FragmentPrefab);
            if (!instance) return;
            instance.name = "Fragments_" + block.Position;
            // 碎块整体换到专属层，废墟里瞄准时射线不会被这些碰撞体占满。
            ApplyLayer(instance.transform, VoxelPlatformRenderer.FragmentLayer);
            // 先挂到容器再摆位，变换计算始终在平台局部空间里进行。
            instance.transform.SetParent(owner.FragmentRoot, false);
            ApplyLocalMatrix(instance.transform, ResolveFragmentMatrix(block));

            // 碎块节点启用前写入本轮的碰撞时长与回收入口，OnEnable 才用得到。
            Transform intact = FindChild(instance.transform, "Intact");
            if (intact) intact.gameObject.SetActive(false);
            Transform fragments = FindChild(instance.transform, "Fragments");
            if (fragments)
            {
                FragmentBurst burst = fragments.GetComponent<FragmentBurst>();
                if (burst)
                {
                    if (collisionDuration.HasValue) burst.OverrideCollisionDuration(collisionDuration.Value);
                    burst.Bind(this, instance);
                }
            }

            // 池内实例的碎块节点保持启用，激活根节点会立即触发 OnEnable；摆位与回收绑定已在前面完成。
            instance.SetActive(true);
            if (fragments && !fragments.gameObject.activeSelf) fragments.gameObject.SetActive(true);

            owner.FragmentRoot.gameObject.SetActive(true);
            spawned.Add(block.Position, instance);
            active.Add(instance, new ActiveFragment(block.Position, block.FragmentPrefab));
        }

        internal void Remove(Vector3Int position)
        {
            // 没有记录说明该方块没被破坏过，直接返回。
            if (!spawned.TryGetValue(position, out GameObject instance)) return;
            spawned.Remove(position);
            if (active.TryGetValue(instance, out ActiveFragment entry))
            {
                active.Remove(instance);
                Return(instance, entry.Prefab);
            }
            else if (instance)
            {
                // 活动表缺失说明实例已不再受平台管理，按无池路径释放。
                VoxelPlatformRenderer.Release(instance);
            }

            // 实例全部收回后关闭容器，场景里不留下空的运行时节点。
            if (spawned.Count == 0 && owner.FragmentRoot)
                owner.FragmentRoot.gameObject.SetActive(false);
        }

        /// <summary>寿命结束的实例交还平台：清理映射、停用并放回来源 Prefab 的闲置池。</summary>
        internal void RecycleInstance(GameObject instance)
        {
            if (!instance || !active.TryGetValue(instance, out ActiveFragment entry)) return;
            active.Remove(instance);
            // 恢复流程可能已经先回收该位置，映射存在且仍指向本实例时才移除。
            if (spawned.TryGetValue(entry.Position, out GameObject current) && current == instance)
                spawned.Remove(entry.Position);
            Return(instance, entry.Prefab);
            if (spawned.Count == 0 && owner.FragmentRoot)
                owner.FragmentRoot.gameObject.SetActive(false);
        }

        internal void Clear()
        {
            // 复制键集合再遍历，Remove 会在遍历过程中修改字典。
            foreach (Vector3Int position in new List<Vector3Int>(spawned.Keys))
                Remove(position);
            // 活动实例已全部归还，闲置实例在这里统一销毁，平台停用后不残留 DontSave 对象。
            foreach (Stack<GameObject> instances in pool.Values)
                while (instances.Count > 0) VoxelPlatformRenderer.Release(instances.Pop());
            pool.Clear();
            active.Clear();
            // 逐个移除后容器可能已经自动关闭，这里再确认一次。
            if (owner.FragmentRoot) owner.FragmentRoot.gameObject.SetActive(false);
        }

        // 从来源 Prefab 的闲置池取实例，池空时实例化新对象完成动态扩容。
        private GameObject Take(GameObject prefab)
        {
            if (pool.TryGetValue(prefab, out Stack<GameObject> instances))
            {
                while (instances.Count > 0)
                {
                    GameObject pooled = instances.Pop();
                    if (pooled) return pooled;
                }
            }

            // 新实例先保持关闭，装配与摆位完成后再启用。
            GameObject created = UnityEngine.Object.Instantiate(prefab);
            created.SetActive(false);
            return created;
        }

        // 停用后放回来源 Prefab 的闲置池，超过上限时直接销毁。
        private void Return(GameObject instance, GameObject prefab)
        {
            if (!instance) return;
            // 停用触发 FragmentBurst.OnDisable，取消寿命计时并停掉刚体，复用时不继承上一轮状态。
            instance.SetActive(false);
            if (!pool.TryGetValue(prefab, out Stack<GameObject> instances))
                pool.Add(prefab, instances = new Stack<GameObject>());
            if (instances.Count >= PoolCapacity)
            {
                VoxelPlatformRenderer.Release(instance);
                return;
            }

            instances.Push(instance);
        }

        // 资源层级深度由素材决定，按名字递归查找，避免写死路径。
        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found) return found;
            }

            return null;
        }

        // 矩阵无法直接写入 Transform，拆成位移、旋转和缩放；旋转由前向与上向重建。
        private static void ApplyLocalMatrix(Transform target, Matrix4x4 matrix)
        {
            Vector3 right = matrix.MultiplyVector(Vector3.right);
            Vector3 up = matrix.MultiplyVector(Vector3.up);
            Vector3 forward = matrix.MultiplyVector(Vector3.forward);
            target.localPosition = matrix.MultiplyPoint3x4(Vector3.zero);
            target.localScale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            // 前向或上向退化时 LookRotation 会报错并退回单位旋转，此时保留实例原有朝向。
            if (Vector3.Cross(forward, up).sqrMagnitude > 0f)
                target.localRotation = Quaternion.LookRotation(forward, up);
        }

        // 层作用到每个预切片上，只设容器根节点不会改变带碰撞体子节点的层。
        private static void ApplyLayer(Transform root, int layer)
        {
            if (layer < 0) return;
            root.gameObject.layer = layer;
            foreach (Transform child in root) ApplyLayer(child, layer);
        }

        // 早期烘焙结果没有写入碎块矩阵，退回方块自身矩阵，碎块仍落在方块原来的位置与朝向上。
        private static Matrix4x4 ResolveFragmentMatrix(VoxelPlatformBlock block)
        {
            return IsDegenerate(block.FragmentMatrix) ? block.Matrix : block.FragmentMatrix;
        }

        // 任一基向量为零的矩阵给不出可用的位置、朝向与缩放，实例会缩到零而看不见。
        private static bool IsDegenerate(Matrix4x4 matrix)
        {
            return matrix.MultiplyVector(Vector3.right).sqrMagnitude <= 0f
                || matrix.MultiplyVector(Vector3.up).sqrMagnitude <= 0f
                || matrix.MultiplyVector(Vector3.forward).sqrMagnitude <= 0f;
        }

        internal static GameObject FindPrefab(Transform intact)
        {
            Transform container = FindContainer(intact);
            if (!container) return null;
            GameObject source = container.gameObject;
#if UNITY_EDITOR
            // 指向原始 Cube 资源，避免平台剥离源层级后留下失效的场景对象引用。
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);
            if (prefab) return PureFragmentPrefab(prefab);
#endif
            return source;
        }

        internal static Transform FindContainer(Transform intact)
        {
            // 父级不挂 Fragments 子节点说明这块 Cube 不可碎，返回空让上层退回整块矩阵。
            Transform container = intact ? intact.parent : null;
            return container && container.Find("Fragments") ? container : null;
        }

#if UNITY_EDITOR
        // 烘焙记录指向同目录的纯碎块 Prefab：Cube_stone → Fragments_stone。
        private static GameObject PureFragmentPrefab(GameObject cubePrefab)
        {
            string path = AssetDatabase.GetAssetPath(cubePrefab);
            string name = Path.GetFileNameWithoutExtension(path);
            // 不遵循 Cube_ 命名的资源保持原引用，避免把无关 Prefab 当成碎块来源。
            if (!name.StartsWith("Cube_", StringComparison.Ordinal)) return cubePrefab;
            string fragmentPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                "Fragments_" + name.Substring("Cube_".Length) + ".prefab");
            GameObject fragmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fragmentPath);
            // 纯碎块 Prefab 缺失时退回原 Cube，旧资源仍可完成烘焙。
            return fragmentPrefab ? fragmentPrefab : cubePrefab;
        }
#endif
    }
}
