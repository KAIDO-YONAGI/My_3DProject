using System.Collections.Generic;
using Core.Scripts.Environment.VoxelPlatform.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Core.Scripts.Environment.VoxelPlatform.Fragments
{
    // 按方块延迟创建预切 Cube，只管理实例与装配位置；受力和刚体由表现脚本负责。
    class VoxelPlatformFragments
    {
        private readonly VoxelPlatformRenderer owner;
        // 已破坏方块到碎块实例的映射，恢复和清空时据此回收。
        private readonly Dictionary<Vector3Int, GameObject> spawned = new();

        internal VoxelPlatformFragments(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
        }

        internal void Spawn(VoxelPlatformBlock block)
        {
            // 同一方块重复破坏只保留一个实例，避免碎块叠加。
            if (spawned.ContainsKey(block.Position) || !block.FragmentPrefab)
                return;
            // 容器首次破坏时才创建，整片平台不破坏就不产生碎块节点。
            if (!owner.FragmentRoot)
                owner.FragmentRoot = owner.CreateRoot("FragmentsRuntime");
            GameObject instance = UnityEngine.Object.Instantiate(block.FragmentPrefab);
            instance.name = "Fragments_" + block.Position;
            // 先挂到容器再摆位，变换计算始终在平台局部空间里进行。
            instance.transform.SetParent(owner.FragmentRoot, false);
            instance.SetActive(true);
            ApplyLocalMatrix(instance.transform, block.FragmentMatrix);

            // 位置设置完成后再启用 Fragments，其 OnEnable 可安全读取碎块世界位置。
            Transform intact = FindChild(instance.transform, "Intact");
            if (intact) intact.gameObject.SetActive(false);
            Transform fragments = FindChild(instance.transform, "Fragments");
            if (fragments) fragments.gameObject.SetActive(true);
            owner.FragmentRoot.gameObject.SetActive(true);
            spawned.Add(block.Position, instance);
        }

        internal void Remove(Vector3Int position)
        {
            // 没有记录说明该方块没被破坏过，直接返回。
            if (!spawned.TryGetValue(position, out GameObject instance)) return;
            if (instance) VoxelPlatformRenderer.Release(instance);
            spawned.Remove(position);
            // 实例全部收回后关闭容器，场景里不留下空的运行时节点。
            if (spawned.Count == 0 && owner.FragmentRoot)
                owner.FragmentRoot.gameObject.SetActive(false);
        }

        internal void Clear()
        {
            // 复制键集合再遍历，Remove 会在遍历过程中修改字典。
            foreach (Vector3Int position in new List<Vector3Int>(spawned.Keys))
                Remove(position);
            // 逐个移除后容器可能已经自动关闭，这里再确认一次。
            if (owner.FragmentRoot) owner.FragmentRoot.gameObject.SetActive(false);
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
            target.localRotation = Quaternion.LookRotation(forward.normalized, up.normalized);
            target.localScale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
        }

        internal static GameObject FindPrefab(Transform intact)
        {
            Transform container = FindContainer(intact);
            if (!container) return null;
            GameObject source = container.gameObject;
#if UNITY_EDITOR
            // 指向原始 Cube 资源，避免平台剥离源层级后留下失效的场景对象引用。
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);
            if (prefab) return prefab;
#endif
            return source;
        }

        internal static Transform FindContainer(Transform intact)
        {
            // 父级不挂 Fragments 子节点说明这块 Cube 不可碎，返回空让上层退回整块矩阵。
            Transform container = intact ? intact.parent : null;
            return container && container.Find("Fragments") ? container : null;
        }
    }
}