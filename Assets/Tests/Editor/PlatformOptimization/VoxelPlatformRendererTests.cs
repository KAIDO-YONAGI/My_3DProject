using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class VoxelPlatformRendererTests
{
    private GameObject root;
    private Component surface;
    private Type surfaceType;
    private Type destructionType;
    private Material material;
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();

    [SetUp]
    public void SetUp()
    {
        surfaceType = Type.GetType("Core.Scripts.VoxelPlatform.VoxelPlatformRenderer, Assembly-CSharp");
        Assert.That(surfaceType, Is.Not.Null, "平台需要可刷新外表面的渲染组件。");
        destructionType = Type.GetType(
            "Core.Scripts.VoxelPlatform.Destruction.VoxelPlatformDestruction, Assembly-CSharp");
        Assert.That(destructionType, Is.Not.Null, "平台破坏入口需要可用。");
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
    public void BakedSurfacesAreGroupedAndReusedAcrossActivationAndDestruction()
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
        Assert.That((bool)surfaceType.GetMethod("SetBlockActive").Invoke(surface,
            new object[] { new Vector3(8, 0, 0), false }), Is.True);
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That((int)surfaceType.GetProperty("CollisionColliderCount").GetValue(surface), Is.EqualTo(1));
        Assert.That((bool)surfaceType.GetMethod("SetBlockActive").Invoke(surface,
            new object[] { new Vector3(8, 0, 0), true }), Is.True);
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
    public void EditableRegionSurvivesBakeAndReloadWithoutEnteringMergedGeometry()
    {
        var included = AddCube(Vector3.zero);
        var editable = AddEditableRegion(Vector3.right);
        var intact = editable.GetComponentInChildren<MeshRenderer>();
        Invoke("Rebuild");
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That(intact.forceRenderingOff, Is.False);

        bakedMeshes.AddRange((Mesh[])surfaceType.GetMethod("Bake").Invoke(surface, null));
        Assert.That(BakedBlockCount(), Is.EqualTo(1));
        Type baker = Type.GetType("VoxelPlatformRendererBaker, Assembly-CSharp-Editor");
        baker.GetMethod("StripSourceHierarchy").Invoke(null, new object[] { surface });
        Assert.That(included == null, Is.True);
        Assert.That(editable, Is.Not.Null);
        Assert.That(intact.forceRenderingOff, Is.False);
        Assert.That(editable.GetComponentInChildren<BoxCollider>().enabled, Is.True);

        // 只剩被排除的源方块时，重建仍复用外围烘焙结果。
        Invoke("Rebuild");
        ((Behaviour)surface).enabled = false;
        ((Behaviour)surface).enabled = true;
        Assert.That(Triangles(), Is.EqualTo(12));
        Assert.That(intact.forceRenderingOff, Is.False);
        Assert.That((int)surfaceType.GetProperty("CollisionColliderCount").GetValue(surface),
            Is.EqualTo(1));
        Assert.That((bool)surfaceType.GetMethod("SetBlockActive").Invoke(surface,
            new object[] { Vector3.right, false }), Is.False);
    }

    [Test]
    public void InactiveEditableRegionRetainsSourcesAndStaysOutsideBakedData()
    {
        AddCube(Vector3.zero);
        var editable = AddEditableRegion(Vector3.right);
        editable.SetActive(false);
        bakedMeshes.AddRange((Mesh[])surfaceType.GetMethod("Bake").Invoke(surface, null));
        Assert.That(BakedBlockCount(), Is.EqualTo(1));
        Type baker = Type.GetType("VoxelPlatformRendererBaker, Assembly-CSharp-Editor");
        baker.GetMethod("StripSourceHierarchy").Invoke(null, new object[] { surface });
        Assert.That(editable.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1));
        Assert.That(editable.activeSelf, Is.False);
    }

    [Test]
    public void PlatformWithOnlyEditableRegionsHasNoBakeSources()
    {
        AddEditableRegion(Vector3.zero);
        Assert.That((bool)surfaceType.GetProperty("HasBakeSources").GetValue(surface), Is.False);
        var exception = Assert.Throws<TargetInvocationException>(() =>
            surfaceType.GetMethod("Bake").Invoke(surface, null));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
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
    public void NestedPrefabBakeReferencesPureFragmentPrefabWithoutIntact()
    {
        const string cubePath =
            "Assets/Core/Prefabs/MinecraftClassic/Cube_stone.prefab";
        const string fragmentPath =
            "Assets/Core/Prefabs/MinecraftClassic/Fragments_stone.prefab";
        const string wrapperPath = "Assets/Tests/Editor/PlatformOptimization/FragmentReferenceTest.prefab";
        GameObject instance = null;
        try
        {
            var cubePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cubePath);
            var fragmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fragmentPath);
            Assert.That(fragmentPrefab, Is.Not.Null, "Cube 需要配套的纯碎块 Prefab。");
            Assert.That(fragmentPrefab.transform.Find("Intact"), Is.Null);
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
                var storedPrefab = (GameObject)block.GetType().GetField("FragmentPrefab").GetValue(block);
                Assert.That(AssetDatabase.GetAssetPath(storedPrefab), Is.EqualTo(fragmentPath));
                Assert.That(storedPrefab, Is.SameAs(fragmentPrefab));
            }
        }
        finally
        {
            if (instance) UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.DeleteAsset(wrapperPath);
        }
    }

    [Test]
    public void PureFragmentPrefabsOmitIntactAndKeepBurst()
    {
        Type burstType = Type.GetType(
            "Core.Scripts.VoxelPlatform.Fragments.FragmentBurst, Assembly-CSharp");
        Assert.That(burstType, Is.Not.Null, "碎块表现脚本需要存在于运行程序集。");
        string[] names =
        {
            "Fragments_stone",
            "Fragments_stonebrick",
            "Fragments_stonebrick_cracked",
            "Fragments_stonebrick_mossy"
        };
        foreach (string name in names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Core/Prefabs/MinecraftClassic/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name);
            Assert.That(prefab.transform.Find("Intact"), Is.Null, name + " 不应包含完整方块。");
            Assert.That(prefab.GetComponentsInChildren(burstType, true).Length, Is.EqualTo(1), name);
            Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true).Length, Is.EqualTo(16), name);
        }
    }

    [Test]
    public void SetBlockActiveCreatesFragmentsOnlyWhenTheBlockIsDestroyed()
    {
        AddCubeWithFragments(Vector3.zero);
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));
        Invoke("UseBakedSurface");

        Assert.That(root.transform.Find("FragmentsRuntime"), Is.Null);
        var setBlockActive = surfaceType.GetMethod("SetBlockActive", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(setBlockActive, Is.Not.Null);
        Assert.That((bool)setBlockActive.Invoke(surface, new object[] { Vector3.zero, false }), Is.True);

        var runtimeFragments = root.transform.Find("FragmentsRuntime");
        Assert.That(runtimeFragments, Is.Not.Null);
        Assert.That(runtimeFragments.GetComponentsInChildren<Transform>(true)
            .Any(child => child.name == "Node_000"), Is.True);
    }

    [Test]
    public void FragmentPoolReusesInstanceAfterRestoreAndDestroy()
    {
        AddCubeWithFragments(Vector3.zero);
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));
        Invoke("UseBakedSurface");
        var setBlockActive = surfaceType.GetMethod("SetBlockActive", BindingFlags.Public | BindingFlags.Instance);

        Assert.That((bool)setBlockActive.Invoke(surface, new object[] { Vector3.zero, false }), Is.True);
        Transform runtime = root.transform.Find("FragmentsRuntime");
        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime.childCount, Is.EqualTo(1));
        GameObject instance = runtime.GetChild(0).gameObject;

        // 恢复方块：实例停用后留在池里，不再立刻销毁。
        Assert.That((bool)setBlockActive.Invoke(surface, new object[] { Vector3.zero, true }), Is.True);
        Assert.That(runtime.childCount, Is.EqualTo(1));
        Assert.That(runtime.GetChild(0).gameObject, Is.SameAs(instance));
        Assert.That(instance.activeSelf, Is.False);

        // 再次破坏：从池里取回同一个实例，层级里不会多出副本。
        Assert.That((bool)setBlockActive.Invoke(surface, new object[] { Vector3.zero, false }), Is.True);
        Assert.That(runtime.childCount, Is.EqualTo(1));
        Assert.That(runtime.GetChild(0).gameObject, Is.SameAs(instance));
        Assert.That(instance.activeSelf, Is.True);
    }

    [Test]
    public void PlatformClearFlushesFragmentPool()
    {
        AddCubeWithFragments(Vector3.zero);
        var bake = surfaceType.GetMethod("Bake", BindingFlags.Public | BindingFlags.Instance);
        bakedMeshes.AddRange((Mesh[])bake.Invoke(surface, null));
        Invoke("UseBakedSurface");
        var setBlockActive = surfaceType.GetMethod("SetBlockActive", BindingFlags.Public | BindingFlags.Instance);
        Assert.That((bool)setBlockActive.Invoke(surface, new object[] { Vector3.zero, false }), Is.True);
        Assert.That(root.transform.Find("FragmentsRuntime").childCount, Is.EqualTo(1));

        object runtime = GetRuntime();
        MethodInfo clear = runtime.GetType().GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(clear, Is.Not.Null, "运行状态需要统一的清理入口。");
        clear.Invoke(runtime, null);

        // 清空时池内实例一并销毁，平台停用后不残留碎块对象。
        Assert.That(root.transform.Find("FragmentsRuntime").childCount, Is.Zero);
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
        Assert.That((bool)surfaceType.GetMethod("SetBlockActive").Invoke(surface,
            new object[] { new Vector3(3, 1.5f, 2), false }), Is.True);

        var runtimeNode = root.transform.Find("FragmentsRuntime").GetComponentsInChildren<Transform>(true)
            .Single(child => child.name == "Node_000");
        Assert.That(Vector3.Distance(runtimeNode.position, expected), Is.LessThan(0.0001f));
    }

    [TestCase(-1.5f)]
    [TestCase(7.5f)]
    public void RayDestructionIdentifiesAndDestroysOneBlock(float x)
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
        object result = DestroyFromRay(ray, 10, ~0, null, 0.45f);

        Assert.That(Result<Vector3?>(result, "HitPoint").HasValue, Is.True);
        Assert.That(Vector3.Distance(Result<Vector3?>(result, "HitPoint").Value,
            root.transform.TransformPoint(center + Vector3.up * 0.5f)), Is.LessThan(0.0001f));
        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(1));
        Assert.That(Result<float>(result, "Radius"), Is.EqualTo(0.45f));
        Assert.That(Triangles(), Is.EqualTo(0));
        Assert.That(root.transform.Find("FragmentsRuntime").gameObject.activeSelf, Is.True);
        Physics.SyncTransforms();
        Assert.That(root.transform.Find("CombinedCollision")
            .GetComponentsInChildren<MeshCollider>().Length, Is.EqualTo(0));
        object miss = DestroyFromRay(ray, 10, ~0, null, 0.45f);
        Assert.That(Result<Vector3?>(miss, "HitPoint").HasValue, Is.False);
        Assert.That(Result<int>(miss, "DestroyedBlockCount"), Is.Zero);
        Assert.That(Result<float>(miss, "Radius"), Is.EqualTo(0.45f));
    }

    [Test]
    public void CollisionHitLooksUpOnlyBlocksFromItsChunk()
    {
        // 两块方块落在不同空间区块，命中原点区块时只应返回该区块的候选列表。
        AddCube(Vector3.zero);
        AddCube(new Vector3(16, 0, 0));
        Invoke("Rebuild");

        var runtimeField = surfaceType.GetField("runtime", BindingFlags.NonPublic | BindingFlags.Instance);
        object runtime = runtimeField.GetValue(surface);
        object meshes = runtime.GetType().GetProperty("Meshes", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(runtime);
        object collisions = meshes.GetType().GetProperty("Collisions", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(meshes);
        MethodInfo tryGetChunk = collisions.GetType().GetMethod("TryGetChunk", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(tryGetChunk, Is.Not.Null);
        object blocks = runtime.GetType().GetField("blocks", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(runtime);
        Assert.That(blocks, Is.Not.Null);
        MethodInfo tryGetChunkBlocks = blocks.GetType().GetMethod("TryGetChunkBlocks",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(tryGetChunkBlocks, Is.Not.Null);

        Physics.SyncTransforms();
        var collider = root.transform.Find("CombinedCollision")
            .GetComponentsInChildren<MeshCollider>()
            .Single(candidate => candidate.Raycast(
                new Ray(new Vector3(0, 3, 0), Vector3.down), out _, 10));
        object[] arguments = { collider, Vector3Int.zero };
        Assert.That((bool)tryGetChunk.Invoke(collisions, arguments), Is.True);
        Assert.That((Vector3Int)arguments[1], Is.EqualTo(Vector3Int.zero));

        object[] blockArguments = { (Vector3Int)arguments[1], null };
        Assert.That((bool)tryGetChunkBlocks.Invoke(blocks, blockArguments), Is.True);
        var blockList = (System.Collections.ICollection)blockArguments[1];
        Assert.That(blockList.Count, Is.EqualTo(1));
    }

    [Test]
    public void RuntimeStoresBlocksOnlyInsideItsChunkIndex()
    {
        AddCube(new Vector3(-1.5f, 0, 0));
        AddCube(new Vector3(16, 0, 0));
        Invoke("Rebuild");

        object runtime = GetRuntime();
        Type runtimeType = runtime.GetType();
        Assert.That(runtimeType.GetField("Blocks", BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance), Is.Null);
        var blocksField = runtimeType.GetField("blocks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(blocksField, Is.Not.Null);
        object blocks = blocksField.GetValue(runtime);
        Assert.That(blocks.GetType().GetField("Blocks", BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance), Is.Null);
        var chunksField = blocks.GetType().GetField("chunks", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(chunksField, Is.Not.Null);
        var chunks = (System.Collections.IDictionary)chunksField.GetValue(blocks);
        Assert.That(chunks.Count, Is.EqualTo(2));
        Assert.That(chunks.Contains(new Vector3Int(-1, 0, 0)), Is.True);
    }

    [Test]
    public void HorizontalDestructionDestroysBlockCentersAcrossAllHeightChunks()
    {
        root.transform.SetPositionAndRotation(new Vector3(20, 5, -10), Quaternion.Euler(0, 35, 0));
        root.transform.localScale = new Vector3(2, 1.5f, 2);
        var center = AddCubeWithFragments(new Vector3(-7.75f, 0, -7.75f));
        var adjacentHeight = AddCubeWithFragments(new Vector3(-8.25f, 40, -7.75f));
        var adjacentHeightSameColumn = AddCubeWithFragments(new Vector3(-7.75f, 81, -7.75f));
        var outsideRadius = AddCubeWithFragments(new Vector3(-6.75f, 0, -7.75f));
        var far = AddCubeWithFragments(new Vector3(16, 0, 0));
        Invoke("Rebuild");

        Vector3 worldCenter = root.transform.TransformPoint(new Vector3(-7.75f, 0, -7.75f));
        object result = DestroyAt(worldCenter, 1.1f);

        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(3));
        Assert.That(center.activeSelf, Is.False);
        Assert.That(adjacentHeight.activeSelf, Is.False);
        Assert.That(adjacentHeightSameColumn.activeSelf, Is.False);
        Assert.That(outsideRadius.activeSelf, Is.True);
        Assert.That(far.activeSelf, Is.True);
    }

    [Test]
    public void HorizontalDestructionCrossesNegativeWorldChunkCornerWithoutDuplicates()
    {
        var blocks = new[]
        {
            AddCubeWithFragments(new Vector3(-8.25f, 0, -8.25f)),
            AddCubeWithFragments(new Vector3(-7.75f, 0, -8.25f)),
            AddCubeWithFragments(new Vector3(-8.25f, 0, -7.75f)),
            AddCubeWithFragments(new Vector3(-7.75f, 0, -7.75f))
        };
        var outsideCorner = AddCubeWithFragments(new Vector3(-7.5f, 0, -7.5f));
        Invoke("Rebuild");

        object result = DestroyAt(new Vector3(-8, 0, -8), 0.36f);

        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(4));
        Assert.That(blocks.All(block => !block.activeSelf), Is.True);
        Assert.That(outsideCorner.activeSelf, Is.True);
    }

    [Test]
    public void HorizontalDestructionDoesNotVisitDistantWorldChunkColumns()
    {
        var nearby = AddCubeWithFragments(Vector3.zero);
        var farX = AddCubeWithFragments(new Vector3(80, 0, 0));
        var farZ = AddCubeWithFragments(new Vector3(0, 0, -80));
        Invoke("Rebuild");

        object result = DestroyAt(Vector3.zero, 0.5f);

        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(1));
        Assert.That(nearby.activeSelf, Is.False);
        Assert.That(farX.activeSelf, Is.True);
        Assert.That(farZ.activeSelf, Is.True);
        object runtime = GetRuntime();
        object destructionTargeting = runtime.GetType()
            .GetField("destructionTargeting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
        Assert.That(destructionTargeting, Is.Not.Null);
        PropertyInfo candidateCount = destructionTargeting.GetType().GetProperty(
            "LastCandidateColumnCount",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(candidateCount, Is.Not.Null,
            "运行时应记录本次圆形查询实际检查的水平 chunk 列数。");
        Assert.That((int)candidateCount.GetValue(destructionTargeting), Is.EqualTo(1));
    }

    [Test]
    public void RuntimeDestroyAroundWorldCenterUsesOnlyNearbyColumns()
    {
        var nearby = AddCube(Vector3.zero);
        var farX = AddCube(new Vector3(80, 0, 0));
        var farZ = AddCube(new Vector3(0, 0, -80));
        Invoke("Rebuild");

        object runtime = GetRuntime();
        MethodInfo destroyAround = runtime.GetType().GetMethod("DestroyBlocksAroundWorldCenter",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(destroyAround, Is.Not.Null, "运行状态需要按世界中心结算破坏。");
        int destroyed = (int)destroyAround.Invoke(runtime, new object[] { Vector3.zero, 0.5f, null });

        Assert.That(destroyed, Is.EqualTo(1));
        Assert.That(nearby.activeSelf, Is.False);
        Assert.That(farX.activeSelf, Is.True);
        Assert.That(farZ.activeSelf, Is.True);
        object destructionTargeting = runtime.GetType()
            .GetField("destructionTargeting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
        PropertyInfo candidateCount = destructionTargeting.GetType().GetProperty(
            "LastCandidateColumnCount",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(candidateCount, Is.Not.Null,
            "运行时应记录本次圆形查询实际检查的水平 chunk 列数。");
        Assert.That((int)candidateCount.GetValue(destructionTargeting), Is.EqualTo(1));
    }

    [Test]
    public void AttackPathHelpersAreNotPublicPlatformApi()
    {
        Assert.That(surfaceType.GetMethod("DestroyBlocks", BindingFlags.Public | BindingFlags.Instance),
            Is.Null);
        Assert.That(surfaceType.GetMethod("DestroyBlocksIn", BindingFlags.Public | BindingFlags.Instance),
            Is.Null);
        Assert.That(surfaceType.GetMethod("TryGetBlockPosition", BindingFlags.Public | BindingFlags.Instance),
            Is.Null);
        Assert.That(surfaceType.GetMethod("DestroyBlockAtHit", BindingFlags.Public | BindingFlags.Instance),
            Is.Null);
        Assert.That(destructionType.GetMethod("Destroy", BindingFlags.Public | BindingFlags.Static),
            Is.Null);
        Assert.That(destructionType.GetMethod("DestroyAt", BindingFlags.Public | BindingFlags.Instance),
            Is.Not.Null);
        Assert.That(destructionType.GetMethod("DestroyFromRay", BindingFlags.Public | BindingFlags.Instance),
            Is.Not.Null);
    }

    [Test]
    public void RuntimeCollaboratorsAreInternal()
    {
        string[] typeNames =
        {
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformRuntime",
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformBlockTable",
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformChunkUpdates",
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformDestructionTargeting",
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformBlockDestroyer",
            "Core.Scripts.VoxelPlatform.Runtime.VoxelPlatformSourceSync"
        };
        foreach (string typeName in typeNames)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, typeName + " 应存在于运行程序集。");
            Assert.That(type.IsPublic, Is.False, typeName + " 不应成为公开 API。");
        }
    }

    [Test]
    public void RuntimeClearRestoresSourceRenderingAndIsRepeatable()
    {
        var intact = AddCube(Vector3.zero);
        var sourceRenderer = intact.GetComponent<MeshRenderer>();
        Invoke("Rebuild");
        Assert.That(sourceRenderer.forceRenderingOff, Is.True);
        Assert.That(Triangles(), Is.EqualTo(12));

        object runtime = GetRuntime();
        MethodInfo clear = runtime.GetType().GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(clear, Is.Not.Null, "运行状态需要统一的清理入口。");
        clear.Invoke(runtime, null);

        Assert.That(sourceRenderer.forceRenderingOff, Is.False);
        Assert.That(Triangles(), Is.Zero);
        Assert.That(root.transform.Find("CombinedSurface").gameObject.activeSelf, Is.False);

        // 重复清理不抛异常，也不残留运行资源。
        Assert.DoesNotThrow(() => clear.Invoke(runtime, null));
        Assert.That(sourceRenderer.forceRenderingOff, Is.False);
        Assert.That(Triangles(), Is.Zero);

        Invoke("Rebuild");
        Assert.That(sourceRenderer.forceRenderingOff, Is.True);
        Assert.That(Triangles(), Is.EqualTo(12));
    }

    [Test]
    public void DestructionTriggerRegionApiStillDestroysBlocks()
    {
        var intact = AddCubeWithFragments(Vector3.zero);
        Invoke("Rebuild");
        var regionObject = new GameObject("DestructionRegion");
        var region = regionObject.AddComponent<SphereCollider>();
        region.radius = 0.6f;

        MethodInfo destroyInRegions = destructionType.GetMethod("DestroyInRegions",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(destroyInRegions, Is.Not.Null);
        int destroyed = (int)destroyInRegions.Invoke(null,
            new object[] { new Collider[] { region }, 0.25f });

        Assert.That(destroyed, Is.EqualTo(1));
        Assert.That(intact.activeSelf, Is.False);
        UnityEngine.Object.DestroyImmediate(regionObject);
    }

    [Test]
    public void VectorDestructionReturnsInputCenter()
    {
        AddCubeWithFragments(new Vector3(7.75f, 0, 0));
        AddCubeWithFragments(new Vector3(8, 24, 0));
        Invoke("Rebuild");

        Vector3 center = new Vector3(7.75f, 3.25f, 0);
        object result = DestroyAt(center, 0.45f);

        Assert.That(Result<Vector3?>(result, "HitPoint").HasValue, Is.True);
        Assert.That(Result<Vector3?>(result, "HitPoint").Value, Is.EqualTo(center));
        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(2));
        Assert.That(Result<float>(result, "Radius"), Is.EqualTo(0.45f));
    }

    [Test]
    public void RayDestructionMissReturnsEmptyResultWithoutDestroyingBlocks()
    {
        var intact = AddCubeWithFragments(Vector3.zero);
        Invoke("Rebuild");

        object result = DestroyFromRay(
            new Ray(new Vector3(0, 3, 0), Vector3.up), 10, ~0, null, 0.45f);

        Assert.That(Result<Vector3?>(result, "HitPoint").HasValue, Is.False);
        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.Zero);
        Assert.That(Result<float>(result, "Radius"), Is.EqualTo(0.45f));
        Assert.That(intact.activeSelf, Is.True);
    }

    [Test]
    public void RayDestructionSkipsSelfFragmentsAndNonPlatformHits()
    {
        var intact = AddCubeWithFragments(Vector3.zero);
        Invoke("Rebuild");

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 4, 0);
        player.AddComponent<BoxCollider>();
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "NonPlatformBlocker";
        blocker.transform.position = new Vector3(0, 2, 0);
        var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fragment.name = "FragmentBlocker";
        fragment.layer = LayerMask.NameToLayer("Fragment");
        Assert.That(fragment.layer, Is.GreaterThanOrEqualTo(0), "项目需要配置 Fragment 层。");
        fragment.transform.position = new Vector3(0, 1.5f, 0);
        Physics.SyncTransforms();

        object result = DestroyFromRay(
            new Ray(new Vector3(0, 6, 0), Vector3.down), 10, ~0, player.transform, 0.45f);

        Assert.That(Result<Vector3?>(result, "HitPoint").HasValue, Is.True);
        Assert.That(Result<Vector3?>(result, "HitPoint").Value.y, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(1));
        Assert.That(intact.activeSelf, Is.False);

        UnityEngine.Object.DestroyImmediate(player);
        UnityEngine.Object.DestroyImmediate(blocker);
        UnityEngine.Object.DestroyImmediate(fragment);
    }

    [Test]
    public void AttackReturnsTheRayDestructionResultToItsCaller()
    {
        AddCubeWithFragments(Vector3.zero);
        Invoke("Rebuild");
        Physics.SyncTransforms();

        var attackObject = new GameObject("Attack");
        attackObject.SetActive(false);
        Component attack = attackObject.AddComponent(
            Type.GetType("Core.Scripts.Character.Interactions.Attack, Assembly-CSharp"));
        attackObject.transform.position = new Vector3(0, 3, 0);
        attackObject.transform.forward = Vector3.down;
        SetField(attack, "playerRoot", attackObject.transform);
        MethodInfo attackFromCrosshair = attack.GetType().GetMethod("AttackFromCrosshair",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(attackFromCrosshair, Is.Not.Null, "Attack 应返回其委托给破坏接口的结果。");
        object result = attackFromCrosshair.Invoke(attack, null);

        Assert.That(Result<Vector3?>(result, "HitPoint").HasValue, Is.True);
        Assert.That(Result<int>(result, "DestroyedBlockCount"), Is.EqualTo(1));

        UnityEngine.Object.DestroyImmediate(attackObject);
    }

    [Test]
    public void PrefabStageBakePreservesAuthoringSources()
    {
        const string authoringPath =
            "Assets/Tests/Editor/PlatformOptimization/StageBake_Authoring.prefab";
        const string runtimePath = "Assets/Tests/Editor/PlatformOptimization/StageBake.prefab";
        var beforeAssets = new HashSet<string>(AssetDatabase.FindAssets("t:Mesh",
            new[] { "Assets/Materials/MinecraftClassic/Generated" }).Select(AssetDatabase.GUIDToAssetPath));
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
                new[] { "Assets/Materials/MinecraftClassic/Generated" }).Select(AssetDatabase.GUIDToAssetPath)
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

    private GameObject AddEditableRegion(Vector3 position)
    {
        Type exclusionType = Type.GetType(
            "Core.Scripts.VoxelPlatform.Baking.VoxelPlatformBakeExclusion, Assembly-CSharp");
        Assert.That(exclusionType, Is.Not.Null, "可编辑区域需要显式排除烘焙的标记。");
        var group = new GameObject("CenterDisk");
        group.transform.SetParent(root.transform, false);
        group.AddComponent(exclusionType);
        AddCube(position).transform.SetParent(group.transform, false);
        return group;
    }

    private int BakedBlockCount()
    {
        return ((System.Collections.ICollection)surfaceType.GetField("bakedBlocks",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(surface)).Count;
    }

    private object GetRuntime()
    {
        var runtimeField = surfaceType.GetField("runtime", BindingFlags.NonPublic | BindingFlags.Instance);
        return runtimeField.GetValue(surface);
    }

    private void Invoke(string method)
    {
        surfaceType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(surface, null);
    }

    private object DestroyAt(Vector3 triggerPoint, float radius)
    {
        MethodInfo method = destructionType.GetMethod("DestroyAt", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Vector3), typeof(float) }, null);
        Assert.That(method, Is.Not.Null, "触发点攻击入口应按位置和半径结算。");
        return method.Invoke(null, new object[] { triggerPoint, radius });
    }

    private object DestroyFromRay(Ray ray, float maxDistance, int layerMask, Transform ignoredRoot, float radius)
    {
        MethodInfo method = destructionType.GetMethod("DestroyFromRay", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Ray), typeof(float), typeof(int), typeof(Transform), typeof(float) }, null);
        Assert.That(method, Is.Not.Null, "Ray 攻击入口应在接口内完成命中筛选。");
        return method.Invoke(null, new object[] { ray, maxDistance, layerMask, ignoredRoot, radius });
    }

    private static T Result<T>(object result, string propertyName)
    {
        Assert.That(result, Is.Not.Null);
        PropertyInfo property = result.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"AttackResult 缺少 {propertyName}。");
        return (T)property.GetValue(result);
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"测试需要设置 {fieldName}。");
        field.SetValue(component, value);
    }

    private int Triangles()
    {
        return (int)surfaceType.GetProperty("RenderedTriangleCount").GetValue(surface);
    }
}
