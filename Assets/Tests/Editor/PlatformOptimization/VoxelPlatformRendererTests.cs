using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class VoxelPlatformRendererTests
{
    private GameObject root;
    private Component surface;
    private Type surfaceType;
    private Material material;

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

    private GameObject AddCube(Vector3 position)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Intact";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localPosition = position;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube;
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
