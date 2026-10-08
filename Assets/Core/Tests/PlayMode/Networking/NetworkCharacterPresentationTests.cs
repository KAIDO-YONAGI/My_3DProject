using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class NetworkCharacterPresentationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private Component manager;
    private Component player;
    private Component identity;
    private Component controller;
    private GameObject localPrefab;
    private GameObject remotePrefab;

    [SetUp]
    public void SetUp()
    {
        Type managerType = RuntimeType("NetworkCharacterManager");
        Assert.That(managerType.GetProperty("Instance").GetValue(null), Is.Null);

        localPrefab = CreateVisual("LocalPrefab");
        remotePrefab = CreateVisual("RemotePrefab");
        manager = CreateObject("PresentationManager").AddComponent(managerType);
        SetField(manager, "localCharacterPrefabs", new[] { localPrefab });
        SetField(manager, "characterPrefabs", new[] { remotePrefab });
        SetField(manager, "networkPresentationMode", false);

        GameObject root = CreateObject("NetworkPlayer");
        root.SetActive(false);
        identity = root.AddComponent(RuntimeType("Mirror.NetworkIdentity"));
        controller = root.AddComponent(RuntimeType("PlayerCharacterController"));
        ((Behaviour)controller).enabled = false;
        player = root.AddComponent(RuntimeType("NetworkCharacterSync"));
        root.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(objects[index]);
            }
        }

        objects.Clear();
    }

    [Test]
    public void RepeatedLocalCallbacksReusePresentationAndSkipCameraConfiguration()
    {
        SetLocal(true);
        Invoke(player, "OnStartClient");
        object first = Presentation();
        Camera camera = Instance(first).GetComponentInChildren<Camera>(true);
        Assert.That(camera.enabled, Is.True);
        Assert.That(GetField(controller, "inputSpace"), Is.SameAs(camera.transform));

        camera.enabled = false;
        Invoke(player, "OnStartLocalPlayer");

        Assert.That(Presentation(), Is.SameAs(first));
        Assert.That(camera.enabled, Is.False);
        Assert.That(Instance(first).transform.parent, Is.SameAs(player.transform));
    }

    [Test]
    public void IdentityChangeWithSharedPrefabReusesInstanceAndReconfiguresCachedComponents()
    {
        SetField(manager, "localCharacterPrefabs", new[] { remotePrefab });
        Apply();
        object first = Presentation();
        GameObject instance = Instance(first);
        Camera camera = instance.GetComponentInChildren<Camera>(true);
        AudioListener listener = instance.GetComponentInChildren<AudioListener>(true);
        Behaviour cameraController = FindComponent(instance, "ThirdPersonCamera") as Behaviour;
        Assert.That(camera.enabled, Is.False);
        Assert.That(listener.enabled, Is.False);
        Assert.That(cameraController.enabled, Is.False);
        Assert.That(GetField(controller, "inputSpace"), Is.Null);

        SetLocal(true);
        Invoke(player, "OnStartLocalPlayer");

        Assert.That(Presentation(), Is.SameAs(first));
        Assert.That(camera.enabled, Is.True);
        Assert.That(camera.CompareTag("MainCamera"), Is.True);
        Assert.That(listener.enabled, Is.True);
        Assert.That(cameraController.enabled, Is.True);
        Assert.That(GetField(cameraController, "target"), Is.SameAs(player.transform));
        Assert.That(GetField(controller, "inputSpace"), Is.SameAs(camera.transform));
        Assert.That(GetField(first, "CameraIsLocal"), Is.True);

        SetLocal(false);
        Apply();
        Assert.That(Presentation(), Is.SameAs(first));
        Assert.That(camera.enabled, Is.False);
        Assert.That(listener.enabled, Is.False);
        Assert.That(cameraController.enabled, Is.False);
        Assert.That(GetField(controller, "inputSpace"), Is.Null);
    }

    [UnityTest]
    public IEnumerator IdentityChangeWithDistinctPrefabsReplacesPresentation()
    {
        Apply();
        GameObject previous = Instance(Presentation());
        Assert.That(previous.name, Does.StartWith(remotePrefab.name));

        SetLocal(true);
        Invoke(player, "OnStartLocalPlayer");
        GameObject current = Instance(Presentation());

        Assert.That(current, Is.Not.SameAs(previous));
        Assert.That(current.name, Does.StartWith(localPrefab.name));
        Assert.That(current.GetComponentInChildren<Camera>(true).enabled, Is.True);
        yield return null;
        Assert.That(previous == null, Is.True);
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator CharacterIdChangeReplacesPresentation()
    {
        SetField(manager, "localCharacterPrefabs", new[] { localPrefab, localPrefab });
        SetField(manager, "characterPrefabs", new[] { remotePrefab, remotePrefab });
        Apply();
        GameObject previous = Instance(Presentation());

        Invoke(manager, "ApplyCharacter", player, 1);

        Assert.That(GetField(Presentation(), "CharacterId"), Is.EqualTo(1));
        Assert.That(Instance(Presentation()), Is.Not.SameAs(previous));
        yield return null;
        Assert.That(previous == null, Is.True);
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void HostCharacterSelectionUsesCommandAndValidatesId()
    {
        SetField(manager, "localCharacterPrefabs", new[] { localPrefab, localPrefab });
        SetField(manager, "characterPrefabs", new[] { remotePrefab, remotePrefab });
        SetLocal(true);
        identity.GetType().GetProperty("isServer").SetValue(identity, true);
        identity.GetType().GetProperty("isClient").SetValue(identity, true);

        Invoke(player, "SetLocalCharacter", 1);
        Assert.That(player.GetType().GetProperty("CharacterId").GetValue(player), Is.EqualTo(1));

        Invoke(player, "SetLocalCharacter", 2);
        Assert.That(player.GetType().GetProperty("CharacterId").GetValue(player), Is.EqualTo(1));
        identity.GetType().GetProperty("isServer").SetValue(identity, false);
        identity.GetType().GetProperty("isClient").SetValue(identity, false);
    }

    [UnityTest]
    public IEnumerator PrefabChangeWithSameIdReplacesPresentation()
    {
        SetLocal(true);
        Apply();
        GameObject previous = Instance(Presentation());
        GameObject replacement = CreateVisual("Replacement");
        SetField(manager, "localCharacterPrefabs", new[] { replacement });

        Apply();

        Assert.That(Instance(Presentation()).name, Does.StartWith(replacement.name));
        Assert.That(Instance(Presentation()), Is.Not.SameAs(previous));
        yield return null;
        Assert.That(previous == null, Is.True);
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void DestroyedInstanceIsRecreated()
    {
        Apply();
        object previous = Presentation();
        UnityEngine.Object.DestroyImmediate(Instance(previous));

        Apply();

        Assert.That(Presentation(), Is.Not.SameAs(previous));
        Assert.That(Instance(Presentation()), Is.Not.Null);
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator RemoveCharacterClearsPresentationAndInputBinding()
    {
        SetLocal(true);
        Apply();
        GameObject previous = Instance(Presentation());

        Invoke(manager, "RemoveCharacter", player);
        Invoke(manager, "RemoveCharacter", player);

        Assert.That(Presentations().Count, Is.Zero);
        Assert.That(GetField(controller, "inputSpace"), Is.Null);
        Assert.That(GetField(controller, "animator"), Is.Null);
        yield return null;
        Assert.That(previous == null, Is.True);
    }

    [Test]
    public void VisualControllersAreDisabledAndInactiveCamerasAreCached()
    {
        SetLocal(true);
        Apply();
        object presentation = Presentation();
        GameObject instance = Instance(presentation);

        Assert.That(((Behaviour)FindComponent(instance, "PlayerCharacterController")).enabled,
            Is.False);
        Assert.That(instance.GetComponent<CharacterController>().enabled, Is.False);
        Assert.That(GetField(presentation, "Cameras"),
            Is.EqualTo(instance.GetComponentsInChildren<Camera>(true)));
        Assert.That(GetField(presentation, "Listeners"),
            Is.EqualTo(instance.GetComponentsInChildren<AudioListener>(true)));
        Assert.That(GetField(presentation, "SourcePrefab"), Is.SameAs(localPrefab));
    }

    private GameObject CreateVisual(string name)
    {
        GameObject visual = CreateObject(name);
        visual.SetActive(false);
        visual.AddComponent(RuntimeType("PlayerCharacterController"));
        GameObject camera = CreateObject(name + "Camera");
        camera.transform.SetParent(visual.transform);
        camera.AddComponent<Camera>();
        camera.AddComponent<AudioListener>();
        camera.AddComponent(RuntimeType("ThirdPersonCamera"));
        return visual;
    }

    private GameObject CreateObject(string name)
    {
        var result = new GameObject(name);
        objects.Add(result);
        return result;
    }

    private void SetLocal(bool value)
    {
        identity.GetType().GetProperty("isLocalPlayer").SetValue(identity, value);
    }

    private void Apply()
    {
        Invoke(manager, "ApplyCharacter", player, 0);
    }

    private IDictionary Presentations()
    {
        return (IDictionary)GetField(manager, "presentations");
    }

    private object Presentation()
    {
        Assert.That(Presentations().Count, Is.EqualTo(1));
        return Presentations()[player];
    }

    private static GameObject Instance(object presentation)
    {
        return (GameObject)GetField(presentation, "Instance");
    }

    private static Component FindComponent(GameObject root, string typeName)
    {
        return root.GetComponentInChildren(RuntimeType(typeName), true);
    }

    private static Type RuntimeType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException("Runtime type not found: " + name);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, Fields | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, Fields).SetValue(target, value);
    }

    private static void Invoke(object target, string name, params object[] arguments)
    {
        target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
            .Invoke(target, arguments);
    }
}
