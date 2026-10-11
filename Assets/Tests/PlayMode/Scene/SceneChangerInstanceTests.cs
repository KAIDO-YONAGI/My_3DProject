using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SceneChangerInstanceTests
{
    private const string GameplayName = "MultiplayerSampleScene";
    private const string OtherGameplayName = "MultiplayerSampleScene 1";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly HashSet<int> originalHandles = new HashSet<int>();
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private Component changer;
    private Scene lastLoadedScene;

    [SetUp]
    public void SetUp()
    {
        originalHandles.Clear();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            originalHandles.Add(SceneManager.GetSceneAt(index).handle);
        }

        Type timeManagerType = RuntimeType("Scene.TimeManager");
        var timeManager = (Component)timeManagerType.GetProperty(
            "Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .GetValue(null);
        if (timeManager == null)
        {
            var timeObject = new GameObject("SceneTestTimeManager");
            objects.Add(timeObject);
            timeObject.AddComponent(timeManagerType);
        }

        var root = new GameObject("SceneChangerUnderTest");
        objects.Add(root);
        root.SetActive(false);
        changer = root.AddComponent(RuntimeType("Scene.SceneChanger"));
        ((Behaviour)changer).enabled = false;
        SetField("retryEventSO", CreateAsset("SO.VoidEventSO"));
        SetField("objectsToUnableWhileMenuOrReset", new UnityEngine.Object[0]);
        SetField("fadeDuration", 0f);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        var addedScenes = new List<Scene>();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!originalHandles.Contains(scene.handle) && scene.isLoaded)
            {
                addedScenes.Add(scene);
            }
        }

        foreach (Scene scene in addedScenes)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(objects[index]);
            }
        }
        objects.Clear();
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator CurrentSceneReturnsLoadedInstanceWhenSameNameAlreadyExists()
    {
        Scene sameNamedScene = GetOrCreateSameNamedScene();
        ScriptableObject asset = CreateSceneAsset(GameplayName);

        yield return Routine("LoadScenesRoutine", asset);

        Assert.That(lastLoadedScene.IsValid() && lastLoadedScene.isLoaded, Is.True);
        Assert.That(lastLoadedScene.handle, Is.Not.EqualTo(sameNamedScene.handle));
        Scene current = (Scene)changer.GetType().GetMethod("GetCurrentScene").Invoke(changer, null);
        Assert.That(current.handle, Is.EqualTo(lastLoadedScene.handle),
            "当前场景必须是切换器实际加载的实例。");
    }

    [UnityTest]
    public IEnumerator SwitchingRepeatedlyUnloadsOwnedSceneAndPreservesSameNamedScene()
    {
        Scene sameNamedScene = GetOrCreateSameNamedScene();
        ScriptableObject first = CreateSceneAsset(GameplayName);
        ScriptableObject second = CreateSceneAsset(OtherGameplayName);
        yield return Routine("LoadScenesRoutine", first);
        Scene previous = lastLoadedScene;

        foreach (ScriptableObject target in new[] { second, first, second })
        {
            yield return Routine("UnloadCurrentScenes", target);

            Assert.That(previous.IsValid() && previous.isLoaded, Is.False,
                "切换前的实际场景必须完成卸载。");
            Assert.That(sameNamedScene.IsValid() && sameNamedScene.isLoaded, Is.True,
                "切换器不能卸载同名的其他实例。");
            Assert.That(lastLoadedScene.IsValid() && lastLoadedScene.isLoaded, Is.True);
            var group = (IList)changer.GetType().GetMethod("GetCurrentScenes").Invoke(changer, null);
            Assert.That(group.Count, Is.EqualTo(1));
            Assert.That(group[0], Is.SameAs(target));
            previous = lastLoadedScene;
        }
    }

    private IEnumerator Routine(string method, ScriptableObject asset)
    {
        var group = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(asset.GetType()));
        group.Add(asset);
        return (IEnumerator)changer.GetType().GetMethod(method, PrivateInstance)
            .Invoke(changer, new object[] { group, Vector3.zero, false });
    }

    private static Scene GetOrCreateSameNamedScene()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.name == GameplayName && scene.isLoaded)
            {
                return scene;
            }
        }
        return SceneManager.CreateScene(GameplayName);
    }

    private ScriptableObject CreateSceneAsset(string sceneName)
    {
        ScriptableObject asset = CreateAsset("SO.GameSceneSO");
        asset.GetType().GetField("sceneName").SetValue(asset, sceneName);
        return asset;
    }

    private ScriptableObject CreateAsset(string name)
    {
        ScriptableObject asset = ScriptableObject.CreateInstance(RuntimeType(name));
        objects.Add(asset);
        return asset;
    }

    private void SetField(string name, object value)
    {
        changer.GetType().GetField(name, PrivateInstance).SetValue(changer, value);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastLoadedScene = scene;
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType("Core.My_FrameWork.Scripts." + name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name);
        return type;
    }
}
