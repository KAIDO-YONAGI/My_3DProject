using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class VoxelPlatformRendererTests
{
    private GameObject root;
    private Component surface;
    private Type surfaceType;
    private Material material;
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();

    [SetUp]
    public void SetUp()
    {
        surfaceType = Type.GetType("Core.Scripts.Environment.VoxelPlatformRenderer, Assembly-CSharp");
        Assert.That(surfaceType, Is.Not.Null, "平台需要可刷新外表面的渲染组件。");
        root = new GameObject("Platform");
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        surface = root.AddComponent(surfaceType);
    }

    [TearDown]
    public void TearDown()
    {
        if (root) UnityEngine.Object.DestroyImmediate(root);
        foreach (var mesh in bakedMeshes)
            if (mesh && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
        bakedMeshes.Clear();
        if (material) UnityEngine.Object.DestroyImmediate(material);
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void TouchingCubesRemoveBothInternalFaces(int rotation)
    {
        AddCube(Vector3.zero);
        AddCube(Vector3.right).transform.localRotation = Quaternion.Euler(0, rotation, 0);
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(20));
    }

    [TestCase(1.01f)]
    [TestCase(0.99f)]
    public void GapsAndOverlapsKeepCompleteFaces(float spacing)
    {
        AddCube(Vector3.zero);
        AddCube(Vector3.right * spacing);
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(24));
    }

    [Test]
    public void DifferentHeightsKeepExposedSides()
    {
        AddCube(Vector3.zero);
        AddCube(new Vector3(1, 6.9f, 0));
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(24));
    }

    [Test]
    public void VerticallyTouchingCubesRemoveBothInternalFaces()
    {
        AddCube(Vector3.zero);
        AddCube(Vector3.up);
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(20));
    }

    [Test]
    public void RemovingAndRestoringCubeRestoresItsNeighborsFace()
    {
        AddCube(new Vector3(7, 0, 0));
        var removed = AddCube(new Vector3(8, 0, 0));
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(20));
        removed.SetActive(false);
        Invoke("RefreshChangedBlocks");
        Assert.That(Triangles(), Is.EqualTo(12));
        removed.SetActive(true);
        Invoke("RefreshChangedBlocks");
        Assert.That(Triangles(), Is.EqualTo(20));
    }

    [Test]
    public void MovingCubeAcrossChunkBoundaryRestoresBothSurfaces()
    {
        AddCube(new Vector3(7, 0, 0));
        var moved = AddCube(new Vector3(8, 0, 0));
        Invoke("Rebuild");
        moved.transform.localPosition += Vector3.right;
        Invoke("RefreshChangedBlocks");
        Assert.That(Triangles(), Is.EqualTo(24));
    }

    [Test]
    public void DestroyingCubeRestoresItsNeighborsFace()
    {
        AddCube(new Vector3(7, 0, 0));
        var removed = AddCube(new Vector3(8, 0, 0));
        Invoke("Rebuild");
        UnityEngine.Object.DestroyImmediate(removed);
        Invoke("RefreshChangedBlocks");
        Assert.That(Triangles(), Is.EqualTo(12));
    }

    [Test]
    public void DisabledRendererStopsOccludingAndDisablingSurfaceRestoresSources()
    {
        var first = AddCube(Vector3.zero).GetComponent<MeshRenderer>();
        var second = AddCube(Vector3.right).GetComponent<MeshRenderer>();
        Invoke("Rebuild");
        Assert.That(first.forceRenderingOff, Is.True);
        second.enabled = false;
        Invoke("RefreshChangedBlocks");
        Assert.That(Triangles(), Is.EqualTo(12));
        ((Behaviour)surface).enabled = false;
        Assert.That(first.forceRenderingOff, Is.False);
        Assert.That(second.enabled, Is.False);
        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(2));
    }

    [Test]
    public void RebuildPreservesSourceMeshesAndIgnoresFragments()
    {
        var intact = AddCube(Vector3.zero);
        Mesh original = intact.GetComponent<MeshFilter>().sharedMesh;
        var fragments = AddCube(Vector3.right);
        fragments.name = "Fragments";
        Invoke("Rebuild");
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That(original.triangles.Length, Is.EqualTo(36));
        Assert.That(intact.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(original));
        Assert.That(fragments.GetComponent<MeshRenderer>().forceRenderingOff, Is.False);
        Assert.That(intact.GetComponent<BoxCollider>().enabled, Is.True);
    }

    [Test]
    public void RuntimePlatformPrefabUsesMergedDataAndAuthoringPrefabRetainsFragments()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(surfaceType), Is.Not.Null);
        var intact = prefab.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.name == "Intact").ToArray();
        Assert.That(intact.Length, Is.EqualTo(0));
        Assert.That(prefab.transform.Find("CenterDisk"), Is.Null);
        Assert.That(prefab.transform.Find("OuterDisk"), Is.Null);
        Assert.That(prefab.transform.Find("CombinedSurface"), Is.Not.Null);
        var collision = prefab.transform.Find("CombinedCollision");
        Assert.That(collision, Is.Not.Null);
        Assert.That(collision.GetComponentsInChildren<MeshCollider>(true).Length, Is.GreaterThan(0));

        var authoring = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25_Authoring.prefab");
        Assert.That(authoring, Is.Not.Null);
        var authoringIntact = authoring.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.name == "Intact").ToArray();
        Assert.That(authoringIntact.Length, Is.GreaterThan(0));
        foreach (var filter in authoringIntact)
        {
            Assert.That(filter.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(filter.sharedMesh.triangles.Length, Is.EqualTo(36));
            var fragments = filter.transform.parent.Find("Fragments");
            Assert.That(fragments, Is.Not.Null);
            Assert.That(fragments.gameObject.activeSelf, Is.False);
            Assert.That(fragments.GetComponentsInChildren<MeshCollider>(true)
                .All(collider => collider.convex), Is.True);
        }
    }

    [Test]
    public void BakedSurfacesAreGroupedAndReusedAcrossActivationAndDamage()
    {
        AddCube(new Vector3(7, 0, 0));
        AddCube(new Vector3(8, 0, 0));
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(bake, Is.Not.Null, "外表面应支持编辑器预生成。");
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));
        var group = root.transform.Find("CombinedSurface");
        Assert.That(group, Is.Not.Null);
        Assert.That(group.gameObject.activeSelf, Is.False);
        Assert.That(root.transform.Find("CombinedCollision"), Is.Not.Null);
        var original = group.GetComponentsInChildren<MeshFilter>(true)
            .Select(filter => filter.sharedMesh).ToArray();

        Invoke("UseBakedSurface");
        Assert.That(Triangles(), Is.EqualTo(20));
        CollectionAssert.AreEquivalent(original, group.GetComponentsInChildren<MeshFilter>()
            .Select(filter => filter.sharedMesh).ToArray());
        Assert.That((bool)surfaceType.GetMethod("BreakBlock").Invoke(surface,
            new object[] { new Vector3(8, 0, 0) }), Is.True);
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That((int)surfaceType.GetProperty("CollisionColliderCount").GetValue(surface), Is.EqualTo(1));
        Assert.That((bool)surfaceType.GetMethod("RestoreBlock").Invoke(surface,
            new object[] { new Vector3(8, 0, 0) }), Is.True);
        Assert.That(Triangles(), Is.EqualTo(20));
        Assert.That((int)surfaceType.GetProperty("CollisionColliderCount").GetValue(surface), Is.EqualTo(2));
        ((Behaviour)surface).enabled = false;
        Assert.That(original.All(mesh => mesh), Is.True);
        Assert.That(group.gameObject.activeSelf, Is.False);
        Invoke("UseBakedSurface");
        Assert.That(Triangles(), Is.EqualTo(20));
        CollectionAssert.AreEquivalent(original, group.GetComponentsInChildren<MeshFilter>()
            .Select(filter => filter.sharedMesh).ToArray());
    }

    [Test]
    public void BakedBlockDataDoesNotRetainSourceComponents()
    {
        var bakedBlockType = surfaceType.GetField("bakedBlocks", BindingFlags.NonPublic | BindingFlags.Instance)
            .FieldType.GetGenericArguments()[0];
        Assert.That(bakedBlockType, Is.Not.Null, "烘焙方块数据应使用独立的轻量类型。");
        Assert.That(bakedBlockType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Any(field => typeof(Component).IsAssignableFrom(field.FieldType)), Is.False);
    }

    [Test]
    public void BakeCreatesOneMergedCollisionPerSpatialChunk()
    {
        AddCube(Vector3.zero);
        AddCube(Vector3.right);
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));

        var collisionRoot = root.transform.Find("CombinedCollision");
        Assert.That(collisionRoot, Is.Not.Null);
        Assert.That(collisionRoot.GetComponentsInChildren<MeshCollider>(true).Length, Is.EqualTo(1));
        Assert.That(collisionRoot.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(0));
    }

    [Test]
    public void StrippedPlatformRebuildReusesBakedGeometryAndCannotOverwriteBake()
    {
        var cube = AddCube(Vector3.zero);
        bakedMeshes.AddRange((Mesh[])surfaceType.GetMethod("Bake").Invoke(surface, null));
        UnityEngine.Object.DestroyImmediate(cube);
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That((int)surfaceType.GetProperty("CollisionColliderCount").GetValue(surface), Is.EqualTo(1));

        var exception = Assert.Throws<TargetInvocationException>(() =>
            surfaceType.GetMethod("Bake").Invoke(surface, null));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That((bool)surfaceType.GetProperty("HasBakedSurface").GetValue(surface), Is.True);
    }

    [Test]
    public void NestedPrefabBakeReferencesOriginalCubeAssetForFragments()
    {
        const string cubePath =
            "Assets/Core/Prefabs/MinecraftClassic/Cube_stone.prefab";
        const string wrapperPath = "Assets/Tests/Editor/PlatformOptimization/FragmentReferenceTest.prefab";
        GameObject instance = null;
        try
        {
            var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cubePath);
            var nestedCube = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab);
            nestedCube.transform.SetParent(root.transform, false);
            var wrapper = PrefabUtility.SaveAsPrefabAsset(root, wrapperPath);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(wrapper);
            var instanceSurface = instance.GetComponent(surfaceType);
            bakedMeshes.AddRange((Mesh[])surfaceType.GetMethod("Bake").Invoke(instanceSurface, null));
            var saved = (System.Collections.IEnumerable)surfaceType.GetField("bakedBlocks",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instanceSurface);
            foreach (var block in saved)
            {
                var fragmentPrefab = (GameObject)block.GetType().GetField("FragmentPrefab").GetValue(block);
                Assert.That(AssetDatabase.GetAssetPath(fragmentPrefab), Is.EqualTo(cubePath));
                Assert.That(fragmentPrefab, Is.SameAs(cubePrefab));
            }
        }
        finally
        {
            if (instance) UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.DeleteAsset(wrapperPath);
        }
    }

    [Test]
    public void BreakBlockCreatesFragmentsOnlyWhenTheBlockIsBroken()
    {
        AddCubeWithFragments(Vector3.zero);
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));
        Invoke("UseBakedSurface");

        Assert.That(root.transform.Find("FragmentsRuntime"), Is.Null);
        var breakBlock = surfaceType.GetMethod("BreakBlock", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(breakBlock, Is.Not.Null);
        Assert.That((bool)breakBlock.Invoke(surface, new object[] { Vector3.zero }), Is.True);

        var runtimeFragments = root.transform.Find("FragmentsRuntime");
        Assert.That(runtimeFragments, Is.Not.Null);
        Assert.That(runtimeFragments.GetComponentsInChildren<Transform>(true)
            .Any(child => child.name == "Node_000"), Is.True);
    }

    [Test]
    public void FragmentsAlignWithOffsetIntactUnderTransformedPlatform()
    {
        root.transform.SetPositionAndRotation(new Vector3(10, 5, -8), Quaternion.Euler(0, 35, 0));
        root.transform.localScale = new Vector3(2, 1.5f, 2);
        var intact = AddCubeWithFragments(new Vector3(3, 1, 2));
        intact.transform.localPosition = Vector3.up * 0.5f;
        var fragments = intact.transform.parent.Find("Fragments");
        fragments.localPosition = intact.transform.localPosition;
        Vector3 expected = fragments.GetChild(0).position;
        Invoke("Rebuild");
        Assert.That((bool)surfaceType.GetMethod("BreakBlock").Invoke(surface,
            new object[] { new Vector3(3, 1.5f, 2) }), Is.True);

        var runtimeNode = root.transform.Find("FragmentsRuntime").GetComponentsInChildren<Transform>(true)
            .Single(child => child.name == "Node_000");
        Assert.That(Vector3.Distance(runtimeNode.position, expected), Is.LessThan(0.0001f));
    }

    [TestCase(-1.5f)]
    [TestCase(7.5f)]
    public void CollisionHitIdentifiesAndBreaksOneBlock(float x)
    {
        root.transform.SetPositionAndRotation(new Vector3(40, 20, -30), Quaternion.Euler(0, 35, 0));
        root.transform.localScale = new Vector3(2, 1.5f, 2);
        Vector3 center = new Vector3(x, 6.4f, -4.5f);
        var cube = AddCubeWithFragments(center);
        bakedMeshes.AddRange((Mesh[])surfaceType.GetMethod("Bake").Invoke(surface, null));
        cube.transform.parent.gameObject.SetActive(false);
        Invoke("UseBakedSurface");
        Physics.SyncTransforms();
        var ray = new Ray(root.transform.TransformPoint(center + Vector3.up * 3), -root.transform.up);
        var collider = root.transform.Find("CombinedCollision").GetComponentInChildren<MeshCollider>();
        RaycastHit hit;
        Assert.That(collider.Raycast(ray, out hit, 10), Is.True);

        var identify = surfaceType.GetMethod("TryGetBlockPosition");
        Assert.That(identify, Is.Not.Null, "命中合并碰撞后需要返回平台局部方块中心。");
        object[] arguments = { hit, Vector3.zero };
        Assert.That((bool)identify.Invoke(surface, arguments), Is.True);
        Assert.That(Vector3.Distance((Vector3)arguments[1], center), Is.LessThan(0.0001f));
        var breakAtHit = surfaceType.GetMethod("BreakBlockAtHit");
        Assert.That(breakAtHit, Is.Not.Null);
        Assert.That((bool)breakAtHit.Invoke(surface, new object[] { hit }), Is.True);
        Assert.That(Triangles(), Is.EqualTo(0));
        Assert.That(root.transform.Find("FragmentsRuntime").gameObject.activeSelf, Is.True);
        Physics.SyncTransforms();
        Assert.That(root.transform.Find("CombinedCollision")
            .GetComponentsInChildren<MeshCollider>().Length, Is.EqualTo(0));
        Assert.That((bool)breakAtHit.Invoke(surface, new object[] { hit }), Is.False);
    }

    [Test]
    public void PrefabStageBakePreservesAuthoringSources()
    {
        const string authoringPath =
            "Assets/Tests/Editor/PlatformOptimization/StageBake_Authoring.prefab";
        const string runtimePath = "Assets/Tests/Editor/PlatformOptimization/StageBake.prefab";
        var beforeAssets = new HashSet<string>(AssetDatabase.FindAssets("t:Mesh",
            new[] { "Assets/Art/MinecraftClassic/Generated" }).Select(AssetDatabase.GUIDToAssetPath));
        try
        {
            AddCube(Vector3.zero);
            const string materialPath =
                "Assets/Tests/Editor/PlatformOptimization/StageBakeMaterial.mat";
            AssetDatabase.DeleteAsset(materialPath);
            var prefabMaterial = new Material(material);
            AssetDatabase.CreateAsset(prefabMaterial, materialPath);
            root.GetComponentInChildren<MeshRenderer>().sharedMaterial = prefabMaterial;
            PrefabUtility.SaveAsPrefabAsset(root, authoringPath);
            var stage = PrefabStageUtility.OpenPrefab(authoringPath);
            var stageSurface = stage.prefabContentsRoot.GetComponent(surfaceType);
            Type baker = Type.GetType("VoxelPlatformRendererBaker, Assembly-CSharp-Editor");
            baker.GetMethod("BakeContext", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { new MenuCommand(stageSurface) });
            Assert.That(stage.prefabContentsRoot.GetComponentsInChildren<MeshFilter>(true)
                .Count(filter => filter.name == "Intact"), Is.EqualTo(1));
            var runtime = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.GetComponentsInChildren<MeshFilter>(true)
                .Count(filter => filter.name == "Intact"), Is.EqualTo(0));
        }
        finally
        {
            StageUtility.GoToMainStage();
            AssetDatabase.DeleteAsset(authoringPath);
            AssetDatabase.DeleteAsset(runtimePath);
            AssetDatabase.DeleteAsset(
                "Assets/Tests/Editor/PlatformOptimization/StageBakeMaterial.mat");
            foreach (string path in AssetDatabase.FindAssets("t:Mesh",
                new[] { "Assets/Art/MinecraftClassic/Generated" }).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().Where(path => !beforeAssets.Contains(path)).ToArray())
                AssetDatabase.DeleteAsset(path);
        }
    }

    [Test]
    public void SameNamedPrefabsKeepIndependentBakedMeshes()
    {
        const string testFolder = "Assets/Tests/Editor/PlatformOptimization";
        string[] folders = { testFolder + "/BakeIsolationA", testFolder + "/BakeIsolationB" };
        var generatedPaths = new HashSet<string>();
        try
        {
            AddCube(Vector3.zero);
            const string materialPath =
                testFolder + "/BakeIsolationMaterial.mat";
            AssetDatabase.DeleteAsset(materialPath);
            var prefabMaterial = new Material(material);
            AssetDatabase.CreateAsset(prefabMaterial, materialPath);
            root.GetComponentInChildren<MeshRenderer>().sharedMaterial = prefabMaterial;
            Type baker = Type.GetType("VoxelPlatformRendererBaker, Assembly-CSharp-Editor");
            Mesh firstMesh = null;
            string firstPath = null;
            foreach (string folder in folders)
            {
                AssetDatabase.CreateFolder(testFolder, System.IO.Path.GetFileName(folder));
                string runtimePath = folder + "/BakeIsolation.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/BakeIsolation_Authoring.prefab");
                baker.GetMethod("BakePrefab").Invoke(null, new object[] { runtimePath });
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath);
                Assert.That(prefab, Is.Not.Null, runtimePath + " 应生成运行版 Prefab。");
                var combinedSurface = prefab.transform.Find("CombinedSurface");
                Assert.That(combinedSurface, Is.Not.Null, runtimePath + " 应包含合并视觉根节点。");
                Mesh mesh = combinedSurface.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                string path = AssetDatabase.GetAssetPath(mesh);
                generatedPaths.Add(path);
                if (firstMesh == null && firstPath == null)
                {
                    firstMesh = mesh;
                    firstPath = path;
                    continue;
                }
                Assert.That(firstMesh, Is.Not.Null);
                Assert.That(path, Is.Not.EqualTo(firstPath));
                Assert.That(firstMesh.triangles.Length, Is.EqualTo(36));
            }
        }
        finally
        {
            foreach (string folder in folders) AssetDatabase.DeleteAsset(folder);
            AssetDatabase.DeleteAsset(testFolder + "/BakeIsolationMaterial.mat");
            foreach (string path in generatedPaths) AssetDatabase.DeleteAsset(path);
        }
    }

    private GameObject AddCube(Vector3 position)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Intact";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localPosition = position;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube;
    }

    private GameObject AddCubeWithFragments(Vector3 position)
    {
        var container = new GameObject("Cube");
        container.transform.SetParent(root.transform, false);
        container.transform.localPosition = position;

        var intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
        intact.name = "Intact";
        intact.transform.SetParent(container.transform, false);
        intact.GetComponent<MeshRenderer>().sharedMaterial = material;

        var fragments = new GameObject("Fragments");
        fragments.transform.SetParent(container.transform, false);
        fragments.SetActive(false);
        var node = GameObject.CreatePrimitive(PrimitiveType.Cube);
        node.name = "Node_000";
        node.transform.SetParent(fragments.transform, false);
        node.GetComponent<MeshRenderer>().sharedMaterial = material;
        return intact;
    }

    private void Invoke(string method)
    {
        surfaceType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(surface, null);
    }

    private int Triangles()
    {
        return (int)surfaceType.GetProperty("RenderedTriangleCount").GetValue(surface);
    }
}
