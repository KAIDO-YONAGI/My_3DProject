using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Scripts.Environment
{
    // 按方块延迟创建预切 Cube，只管理实例与装配位置；受力和刚体由表现脚本负责。
    internal sealed class VoxelPlatformFragments
    {
        private readonly VoxelPlatformRenderer owner;
        private readonly Dictionary<Vector3Int, GameObject> spawned =
            new Dictionary<Vector3Int, GameObject>();

        internal VoxelPlatformFragments(VoxelPlatformRenderer owner)
        {
            this.owner = owner;
        }

        internal void Spawn(VoxelPlatformBlock block)
        {
            if (spawned.ContainsKey(block.Position) || !block.FragmentPrefab)
                return;
            if (!owner.FragmentRoot)
                owner.FragmentRoot = owner.CreateRoot("FragmentsRuntime");
            GameObject instance = UnityEngine.Object.Instantiate(block.FragmentPrefab);
            instance.name = "Fragments_" + block.Position;
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
            if (!spawned.TryGetValue(position, out GameObject instance)) return;
            if (instance) VoxelPlatformRenderer.Release(instance);
            spawned.Remove(position);
            if (spawned.Count == 0 && owner.FragmentRoot)
                owner.FragmentRoot.gameObject.SetActive(false);
        }

        internal void Clear()
        {
            foreach (Vector3Int position in new List<Vector3Int>(spawned.Keys))
                Remove(position);
            if (owner.FragmentRoot) owner.FragmentRoot.gameObject.SetActive(false);
        }

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
            Transform container = intact ? intact.parent : null;
            return container && container.Find("Fragments") ? container : null;
        }
    }
}
