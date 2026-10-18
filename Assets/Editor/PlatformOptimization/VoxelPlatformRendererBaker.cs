using System.IO;
using System.Linq;
using Core.Scripts.Environment;
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
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var renderer = (VoxelPlatformRenderer)command.context;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            BakePrefab(stage.assetPath);
            return;
        }
        if (EditorUtility.IsPersistent(renderer))
        {
            string path = AssetDatabase.GetAssetPath(renderer);
            BakePrefab(path);
            return;
        }
        UnpackPlatform(renderer);
        BakeToAsset(renderer, SceneAssetPath(renderer));
        StripSourceHierarchy(renderer);
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }

    [MenuItem("My_3DProject/Platform/Bake Active Scene Surfaces")]
    public static void BakeActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var renderer in scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<VoxelPlatformRenderer>(true)))
        {
            if (!renderer.GetComponentsInChildren<MeshFilter>(true)
                .Any(filter => filter.name == "Intact"))
                continue;
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
        if (name.EndsWith(AuthoringSuffix))
            prefabPath = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/" +
                name.Substring(0, name.Length - AuthoringSuffix.Length) + ".prefab";
        string authoringPath = GetAuthoringPrefabPath(prefabPath);
        if (authoringPath != prefabPath &&
            !AssetDatabase.LoadAssetAtPath<GameObject>(authoringPath))
        {
            if (!AssetDatabase.CopyAsset(prefabPath, authoringPath))
                throw new IOException("无法创建平台源 Prefab：" + authoringPath);
            AssetDatabase.ImportAsset(authoringPath);
        }

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
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    public static void BakeToAsset(VoxelPlatformRenderer renderer, string assetPath)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("运行时不能覆盖平台烘焙资源。");
        EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
        Object container = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (!container)
        {
            container = new Mesh { name = "PlatformSurface" };
            AssetDatabase.CreateAsset(container, assetPath);
        }
        Object[] previous = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .Where(asset => asset != container).ToArray();
        Mesh[] meshes = renderer.Bake();
        foreach (Mesh mesh in meshes)
        {
            AssetDatabase.AddObjectToAsset(mesh, container);
            EditorUtility.SetDirty(mesh);
        }
        EditorUtility.SetDirty(renderer);
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
        foreach (Object asset in previous) Object.DestroyImmediate(asset, true);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }

    public static void StripSourceHierarchy(VoxelPlatformRenderer renderer)
    {
        var sourceRoots = new System.Collections.Generic.HashSet<GameObject>();
        foreach (MeshFilter filter in renderer.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Intact") continue;
            Transform parent = filter.transform.parent;
            if (!parent || parent == renderer.transform)
            {
                sourceRoots.Add(filter.gameObject);
                continue;
            }

            if (parent.Find("Fragments") || parent.name.StartsWith("Cube_"))
                sourceRoots.Add(parent.gameObject);
            else
                sourceRoots.Add(filter.gameObject);
        }

        foreach (GameObject sourceRoot in sourceRoots)
            Object.DestroyImmediate(sourceRoot);
        foreach (string sourceGroupName in new[] { "CenterDisk", "OuterDisk" })
        {
            Transform sourceGroup = renderer.transform.Find(sourceGroupName);
            if (sourceGroup && sourceGroup.childCount == 0)
                Object.DestroyImmediate(sourceGroup.gameObject);
        }
        renderer.UseBakedSurface();
        EditorUtility.SetDirty(renderer);
    }

    private static string SceneAssetPath(VoxelPlatformRenderer renderer)
    {
        return OutputFolder + "/" + renderer.gameObject.scene.name + "_" +
            GlobalObjectId.GetGlobalObjectIdSlow(renderer.gameObject) + ".asset";
    }

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

    private static string GetPrefabAssetPath(string prefabPath)
    {
        string normalized = prefabPath.Replace('\\', '/');
        string hash = Hash128.Compute(normalized).ToString();
        return OutputFolder + "/" + Path.GetFileNameWithoutExtension(prefabPath) + "_" + hash + ".asset";
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
