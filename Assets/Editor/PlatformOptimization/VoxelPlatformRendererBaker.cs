using System.IO;
using System.Linq;
using Core.Scripts.Environment;
using Core.Scripts.Environment.VoxelPlatform.Baking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VoxelPlatformRendererBaker
{
    private const string OutputFolder = "Assets/Art/MinecraftClassic/Generated";
    private const string AuthoringSuffix = "_Authoring";

    [MenuItem("CONTEXT/VoxelPlatformRenderer/Bake Platform Surface")]
    private static void BakeContext(MenuCommand command)
    {
        // 烘焙会改资产和场景层级，播放中的状态不能作为输入。
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var renderer = (VoxelPlatformRenderer)command.context;
        // Prefab 编辑模式里的对象属于预览场景，只能按资产路径烘焙。
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            BakePrefab(stage.assetPath);
            return;
        }
        // 资产里的组件不能直接改动，交给资产路径分支处理。
        if (EditorUtility.IsPersistent(renderer))
        {
            string path = AssetDatabase.GetAssetPath(renderer);
            BakePrefab(path);
            return;
        }
        // 场景实例先解包，烘焙结果才能直接写回场景层级。
        UnpackPlatform(renderer);
        BakeToAsset(renderer, SceneAssetPath(renderer));
        StripSourceHierarchy(renderer);
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }

    [MenuItem("My_3DProject/Platform/Bake Active Scene Surfaces")]
    public static void BakeActiveScene()
    {
        // 批量入口：遍历场景里的所有平台逐个重烤，最后统一保存一次场景。
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var renderer in scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<VoxelPlatformRenderer>(true)))
        {
            // 已经没有源 Cube 的平台不需要重烤，跳过才能反复执行整场景烘焙。
            if (!renderer.HasBakeSources) continue;
            UnpackPlatform(renderer);
            BakeToAsset(renderer, SceneAssetPath(renderer));
            StripSourceHierarchy(renderer);
        }
        EditorSceneManager.SaveScene(scene);
    }

    public static void BakePrefab(string prefabPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        // 允许直接传入 _Authoring 源 Prefab，最终产物仍然落到烘焙 Prefab 上。
        if (name.EndsWith(AuthoringSuffix))
            prefabPath = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/" +
                name.Substring(0, name.Length - AuthoringSuffix.Length) + ".prefab";
        // 首次烘焙时留下 _Authoring 副本：源 Cube 层级保存在副本里，产物 Prefab 只存合并结果。
        string authoringPath = GetAuthoringPrefabPath(prefabPath);
        if (authoringPath != prefabPath &&
            !AssetDatabase.LoadAssetAtPath<GameObject>(authoringPath))
        {
            if (!AssetDatabase.CopyAsset(prefabPath, authoringPath))
                throw new IOException("无法创建平台源 Prefab：" + authoringPath);
            AssetDatabase.ImportAsset(authoringPath);
        }

        // 在隔离的预览场景里改 Prefab，避免污染当前打开的场景。
        GameObject contents = PrefabUtility.LoadPrefabContents(authoringPath);
        try
        {
            var renderer = contents.GetComponent<VoxelPlatformRenderer>();
            if (!renderer) return;
            string assetPath = GetPrefabAssetPath(prefabPath);
            BakeToAsset(renderer, assetPath);
            StripSourceHierarchy(renderer);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            AssetDatabase.SaveAssets();
            // 新网格是刚加入的子资产，强制重新导入才能立刻被资源库识别。
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    public static void BakeToAsset(VoxelPlatformRenderer renderer, string assetPath)
    {
        // 运行时改资产会与场景里的运行状态冲突，这里直接拒绝。
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("运行时不能覆盖平台烘焙资源。");
        EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
        // 复用已有容器资产并替换其子资产，资产 GUID 和所有外部引用保持不变。
        Object container = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (!container)
        {
            container = new Mesh { name = "PlatformSurface" };
            AssetDatabase.CreateAsset(container, assetPath);
        }
        // 先记下旧子资产，等新网格写入后再删除，中间不会出现引用悬空。
        Object[] previous = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .Where(asset => asset != container).ToArray();
        Mesh[] meshes = renderer.Bake();
        // 网格必须挂在容器资产下才能持久化，单独建资产会产生大量碎文件。
        foreach (Mesh mesh in meshes)
        {
            AssetDatabase.AddObjectToAsset(mesh, container);
            EditorUtility.SetDirty(mesh);
        }
        EditorUtility.SetDirty(renderer);
        // 代码直接改子对象属性不会自动生成 Prefab 覆盖记录，逐个登记后保存才能保留。
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        Transform group = renderer.transform.Find("CombinedSurface");
        foreach (Transform child in group)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.GetComponent<MeshFilter>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.GetComponent<MeshRenderer>());
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(group.gameObject);
        Transform collision = renderer.transform.Find("CombinedCollision");
        if (collision)
        {
            foreach (Transform child in collision)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.GetComponent<MeshCollider>());
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(collision.gameObject);
        }
        // 新网格已写入容器，这时才删除旧子资产。
        foreach (Object asset in previous) Object.DestroyImmediate(asset, true);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }

    public static void StripSourceHierarchy(VoxelPlatformRenderer renderer)
    {
        // 剥离源层级，场景里只保留合并表面；带排除标记的方块继续留作直接编辑。
        var sourceRoots = new System.Collections.Generic.HashSet<GameObject>();
        foreach (MeshFilter filter in renderer.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Intact") continue;
            // 与收集阶段保持同一排除范围，保留场景中需要直接编辑的方块。
            if (filter.GetComponentInParent<VoxelPlatformBakeExclusion>(true)) continue;
            Transform parent = filter.transform.parent;
            if (!parent || parent == renderer.transform)
            {
                sourceRoots.Add(filter.gameObject);
                continue;
            }

            // 源 Cube 的根是 Cube_ 容器，删掉容器才能把 Intact 和碎块一起清走。
            if (parent.Find("Fragments") || parent.name.StartsWith("Cube_"))
                sourceRoots.Add(parent.gameObject);
            else
                sourceRoots.Add(filter.gameObject);
        }

        foreach (GameObject sourceRoot in sourceRoots)
            Object.DestroyImmediate(sourceRoot);
        // 分组容器在子节点删空后一并移除，场景里不留空组。
        foreach (string sourceGroupName in new[] { "CenterDisk", "OuterDisk" })
        {
            Transform sourceGroup = renderer.transform.Find(sourceGroupName);
            if (sourceGroup && sourceGroup.childCount == 0)
                Object.DestroyImmediate(sourceGroup.gameObject);
        }
        renderer.UseBakedSurface();
        EditorUtility.SetDirty(renderer);
    }

    // 场景里的平台没有 Prefab 资产路径，用场景名和全局对象 ID 拼出稳定的资产路径。
    private static string SceneAssetPath(VoxelPlatformRenderer renderer)
    {
        return OutputFolder + "/" + renderer.gameObject.scene.name + "_" +
            GlobalObjectId.GetGlobalObjectIdSlow(renderer.gameObject) + ".asset";
    }

    // 解包后平台层级归场景所有，烘焙改动能直接写回场景。
    private static void UnpackPlatform(VoxelPlatformRenderer renderer)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
        if (!root) return;
        if (root != renderer.gameObject)
            throw new System.InvalidOperationException("请先将平台作为独立 Prefab 实例处理。");
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot,
            InteractionMode.AutomatedAction);
    }

    private static string GetAuthoringPrefabPath(string prefabPath)
    {
        string directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        if (name.EndsWith(AuthoringSuffix))
            return prefabPath;
        return directory + "/" + name + AuthoringSuffix + ".prefab";
    }

    // 用路径哈希命名生成资产，同一 Prefab 始终对应同一文件，重新烘焙只替换内容。
    private static string GetPrefabAssetPath(string prefabPath)
    {
        string normalized = prefabPath.Replace('\\', '/');
        string hash = Hash128.Compute(normalized).ToString();
        return OutputFolder + "/" + Path.GetFileNameWithoutExtension(prefabPath) + "_" + hash + ".asset";
    }

    // AssetDatabase.CreateFolder 只建一层，父级要递归补齐。
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
