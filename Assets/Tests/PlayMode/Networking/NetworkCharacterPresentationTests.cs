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
        ConfigureCharacters(new[] { localPrefab });
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
    public void StandaloneControllerCachesMissingNetworkIdentity()
    {
        GameObject root = CreateObject("StandalonePlayer");
        root.SetActive(false);
        Component standalone = root.AddComponent(RuntimeType("PlayerCharacterController"));
        ((Behaviour)standalone).enabled = false;
        root.SetActive(true);
        Assert.That(GetField(standalone, "networkIdentity"), Is.Null);

        // 首次查询后再添加身份，用来检测后续初始化是否偷偷重复查找。
        root.AddComponent(RuntimeType("Mirror.NetworkIdentity"));
        MethodInfo ensureRuntimeState = standalone.GetType().GetMethod(
            "EnsureRuntimeState", Fields);
        for (int index = 0; index < 3; index++)
        {
            Assert.That(ensureRuntimeState.Invoke(standalone, null), Is.True);
            Assert.That(GetField(standalone, "networkIdentity"), Is.Null);
        }
    }

    [Test]
    public void NetworkControllerCachesIdentityAvailableBeforeAwake()
    {
        Assert.That(GetField(controller, "networkIdentity"), Is.SameAs(identity));
        MethodInfo ensureRuntimeState = controller.GetType().GetMethod(
            "EnsureRuntimeState", Fields);
        for (int index = 0; index < 3; index++)
        {
            ensureRuntimeState.Invoke(controller, null);
            Assert.That(GetField(controller, "networkIdentity"), Is.SameAs(identity));
        }

        SetLocal(true);
        Assert.That(((Component)GetField(controller, "networkIdentity")).GetType()
            .GetProperty("isLocalPlayer").GetValue(identity), Is.True);
        SetLocal(false);
        Assert.That(((Component)GetField(controller, "networkIdentity")).GetType()
            .GetProperty("isLocalPlayer").GetValue(identity), Is.False);
    }

    [Test]
    public void AnimatorMountedAfterIdentityLookupStillInitializes()
    {
        Assert.That(GetField(controller, "animator"), Is.Null);
        Assert.That(GetField(controller, "animationDriver"), Is.Null);
        RuntimeAnimatorController configuredController =
            Resources.Load<RuntimeAnimatorController>("CharacterLocomotion");
        Assert.That(configuredController, Is.Not.Null);
        localPrefab.GetComponent<Animator>().runtimeAnimatorController = configuredController;
        localPrefab.GetComponentInChildren<Camera>(true).gameObject.SetActive(false);
        localPrefab.SetActive(true);
        SetLocal(true);

        Apply();

        Animator mountedAnimator = Instance(Presentation()).GetComponentInChildren<Animator>(true);
        Assert.That(mountedAnimator, Is.Not.Null);
        Assert.That(GetField(controller, "animator"), Is.SameAs(mountedAnimator));
        Assert.That(GetField(controller, "animationDriver"), Is.Not.Null);
        Assert.That(mountedAnimator.runtimeAnimatorController, Is.SameAs(configuredController));
        Assert.That(mountedAnimator.GetFloat("IdleWeight"), Is.EqualTo(1f));
        Assert.That(GetField(controller, "networkIdentity"), Is.SameAs(identity));
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
        ConfigureCharacters(new[] { remotePrefab });
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
    public IEnumerator IdentityChangeKeepsSharedCharacterPrefab()
    {
        Apply();
        GameObject previous = Instance(Presentation());
        Assert.That(previous.name, Does.StartWith(localPrefab.name));

        SetLocal(true);
        Invoke(player, "OnStartLocalPlayer");
        GameObject current = Instance(Presentation());

        Assert.That(current, Is.SameAs(previous));
        Assert.That(current.name, Does.StartWith(localPrefab.name));
        Assert.That(current.GetComponentInChildren<Camera>(true).enabled, Is.True);
        yield return null;
        Assert.That(previous != null, Is.True);
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator CharacterIdChangeReplacesPresentation()
    {
        ConfigureCharacters(new[] { localPrefab, remotePrefab });
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
        ConfigureCharacters(new[] { localPrefab, remotePrefab });
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
        ConfigureCharacters(new[] { replacement });

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
    public void NetworkCharacterDisablesOwnMovementAndUsesExplicitReferences()
    {
        SetLocal(true);
        Apply();
        object presentation = Presentation();
        GameObject instance = Instance(presentation);

        Assert.That(((Behaviour)FindComponent(instance, "PlayerCharacterController")).enabled, Is.False);
        Assert.That(instance.GetComponent<CharacterController>().enabled, Is.False);
        Assert.That(GetField(presentation, "Cameras"),
            Is.EqualTo(instance.GetComponentsInChildren<Camera>(true)));
        Assert.That(GetField(presentation, "Listeners"),
            Is.EqualTo(instance.GetComponentsInChildren<AudioListener>(true)));
        Assert.That(GetField(presentation, "SourcePrefab"), Is.SameAs(localPrefab));
    }

    [Test]
    public void InvalidVisualKeepsExistingPresentationAndBindings()
    {
        SetLocal(true);
        Apply();
        object previous = Presentation();
        object previousAnimator = GetField(controller, "animator");
        GameObject invalid = CreateVisual("InvalidVisual");
        SetField(invalid.GetComponent(RuntimeType("CharacterVisual")), "animator", null);
        ConfigureCharacters(new[] { invalid });
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            "角色 0 的资源 InvalidVisual 配置无效"));

        Apply();

        Assert.That(Presentation(), Is.SameAs(previous));
        Assert.That(GetField(controller, "animator"), Is.SameAs(previousAnimator));
        Assert.That(player.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void InputUsesConfiguredPrimaryCameraRatherThanArrayOrder()
    {
        Component visual = localPrefab.GetComponent(RuntimeType("CharacterVisual"));
        Camera primary = localPrefab.GetComponentInChildren<Camera>(true);
        GameObject auxiliary = CreateObject("AuxiliaryCamera");
        auxiliary.transform.SetParent(localPrefab.transform, false);
        Camera first = auxiliary.AddComponent<Camera>();
        SetField(visual, "cameras", new[] { first, primary });
        SetLocal(true);

        Apply();

        Component mounted = Instance(Presentation()).GetComponent(RuntimeType("CharacterVisual"));
        var configured = (Camera)GetField(mounted, "inputCamera");
        Assert.That(GetField(controller, "inputSpace"), Is.SameAs(configured.transform));
        Assert.That(configured.name, Is.EqualTo(primary.name));
        Assert.That(first.CompareTag("MainCamera"), Is.False);
    }

    [UnityTest]
    public IEnumerator ReplacementDisablesPreviousCameraBeforeDeferredDestruction()
    {
        localPrefab.SetActive(true);
        remotePrefab.SetActive(true);
        SetLocal(true);
        Apply();
        GameObject previous = Instance(Presentation());
        ConfigureCharacters(new[] { remotePrefab });

        Apply();

        Assert.That(previous.activeSelf, Is.False);
        Assert.That(Instance(Presentation()).activeSelf, Is.True);
        yield return null;
        Assert.That(previous == null, Is.True);
    }

    [Test]
    public void StandaloneSelectionUsesSharedPrefabAndPreservesPose()
    {
        GameObject standalone = CreateVisual("StandalonePrefab");
        ConfigureCharacters(new[] { standalone });
        GameObject previous = CreateObject("PreviousStandalone");
        previous.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(0f, 47f, 0f));
        SetField(manager, "standalonePlayer", previous);

        Invoke(manager, "RequestCharacter", 0);

        var current = (GameObject)GetField(manager, "standalonePlayer");
        Assert.That(current.GetComponent<Animator>(), Is.Not.Null);
        Assert.That(((Behaviour)FindComponent(current, "PlayerCharacterController")).enabled, Is.True);
        Assert.That(current.GetComponent<CharacterController>().enabled, Is.True);
        Assert.That(current.transform.position, Is.EqualTo(previous.transform.position));
        Assert.That(Quaternion.Angle(current.transform.rotation, previous.transform.rotation),
            Is.LessThan(0.001f));
        Assert.That(current.GetComponentInChildren<Camera>(true).name,
            Does.StartWith(standalone.name));
        objects.Add(current);
    }

    private GameObject CreateVisual(string name)
    {
        GameObject visual = CreateObject(name);
        visual.SetActive(false);
        CharacterController movementCollider = visual.AddComponent<CharacterController>();
        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("CharacterLocomotion");
        Component movement = visual.AddComponent(RuntimeType("PlayerCharacterController"));
        SetField(movement, "animator", animator);
        GameObject camera = CreateObject(name + "Camera");
        camera.transform.SetParent(visual.transform);
        camera.tag = "MainCamera";
        Camera cameraComponent = camera.AddComponent<Camera>();
        cameraComponent.enabled = false;
        AudioListener listener = camera.AddComponent<AudioListener>();
        listener.enabled = false;
        var cameraController = (Behaviour)camera.AddComponent(RuntimeType("ThirdPersonCamera"));
        cameraController.enabled = false;
        Component configuration = visual.AddComponent(RuntimeType("CharacterVisual"));
        SetField(configuration, "animator", animator);
        SetField(configuration, "standaloneController", movement);
        SetField(configuration, "movementCollider", movementCollider);
        SetField(configuration, "inputCamera", cameraComponent);
        SetField(configuration, "cameras", new[] { cameraComponent });
        SetField(configuration, "listeners", new[] { listener });
        Array controllers = Array.CreateInstance(RuntimeType("ThirdPersonCamera"), 1);
        controllers.SetValue(cameraController, 0);
        SetField(configuration, "cameraControllers", controllers);
        SetField(GetField(configuration, "ponytailFollower"), "enabled", false);
        return visual;
    }

    private void ConfigureCharacters(GameObject[] prefabs)
    {
        SetField(manager, "characters", prefabs);
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
        string qualifiedName = name switch
        {
            "PlayerCharacterController" => "Core.Scripts.Character.Control.PlayerCharacterController",
            "ThirdPersonCamera" => "Core.Scripts.Camera.Runtime.ThirdPersonCamera",
            "CharacterVisual" => "Core.Scripts.Character.Presentation.CharacterVisual",
            "NetworkCharacterManager" => "Core.Scripts.Networking.NetworkCharacterManager",
            "NetworkCharacterSync" => "Core.Scripts.Networking.NetworkCharacterSync",
            _ => name
        };

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(qualifiedName);
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
