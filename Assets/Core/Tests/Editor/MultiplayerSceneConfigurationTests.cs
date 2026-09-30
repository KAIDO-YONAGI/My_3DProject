using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MultiplayerSceneConfigurationTests
{
    private const string PersistentScenePath = "Assets/Core/Scenes/PersistentScene.unity";
    private const string GameplayScenePath = "Assets/Core/Scenes/MultiplayerSampleScene.unity";

    [Test]
    public void LocalCharacterPrefabsReferenceCompletePrefabRoots()
    {
        Scene persistentScene = EnsureSceneLoaded(PersistentScenePath, out bool openedScene);

        try
        {
            MonoBehaviour manager = FindComponent(persistentScene, "NetworkCharacterManager");
            Assert.That(manager, Is.Not.Null);

            SerializedProperty prefabs = new SerializedObject(manager)
                .FindProperty("localCharacterPrefabs");
            Assert.That(prefabs, Is.Not.Null);
            Assert.That(prefabs.arraySize, Is.GreaterThan(0));

            for (int index = 0; index < prefabs.arraySize; index++)
            {
                GameObject prefab = prefabs.GetArrayElementAtIndex(index).objectReferenceValue
                    as GameObject;

                Assert.That(prefab, Is.Not.Null, $"本地角色编号 {index} 没有配置 Prefab。");
                Assert.That(
                    prefab.transform.parent,
                    Is.Null,
                    $"本地角色编号 {index} 引用了 Prefab 内部节点 {prefab.name}。");
                Assert.That(
                    prefab.GetComponentInChildren<Camera>(true),
                    Is.Not.Null,
                    $"本地角色编号 {index} 的 Prefab 缺少 Camera。");
                Assert.That(
                    FindComponent(prefab, "ThirdPersonCamera"),
                    Is.Not.Null,
                    $"本地角色编号 {index} 的 Prefab 缺少 ThirdPersonCamera。");
            }
        }
        finally
        {
            CloseIfOpened(persistentScene, openedScene);
        }
    }

    [Test]
    public void NetworkSpawnPointsStartSlightlyAboveTerrainCollider()
    {
        Scene persistentScene = EnsureSceneLoaded(PersistentScenePath, out bool openedPersistent);
        Scene gameplayScene = EnsureSceneLoaded(GameplayScenePath, out bool openedGameplay);

        try
        {
            Terrain terrain = FindComponent<Terrain>(gameplayScene);
            Assert.That(terrain, Is.Not.Null);

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            Assert.That(terrainCollider, Is.Not.Null);

            List<Transform> spawnPoints = FindTransformsWithComponent(
                persistentScene,
                "NetworkStartPosition");
            Assert.That(spawnPoints, Is.Not.Empty);

            foreach (Transform spawnPoint in spawnPoints)
            {
                Vector3 rayOrigin = new Vector3(
                    spawnPoint.position.x,
                    terrainCollider.bounds.max.y + 1000f,
                    spawnPoint.position.z);
                bool hitTerrain = terrainCollider.Raycast(
                    new Ray(rayOrigin, Vector3.down),
                    out RaycastHit hit,
                    3000f);

                Assert.That(
                    hitTerrain,
                    Is.True,
                    $"{spawnPoint.name} 下方没有 TerrainCollider。");

                float heightGap = spawnPoint.position.y - hit.point.y;
                Assert.That(
                    heightGap,
                    Is.InRange(0.05f, 0.5f),
                    $"{spawnPoint.name} 与地表的高度差为 {heightGap:F3}。");
            }
        }
        finally
        {
            CloseIfOpened(gameplayScene, openedGameplay);
            CloseIfOpened(persistentScene, openedPersistent);
        }
    }

    [Test]
    public void NetworkCameraModeDisablesStandaloneCameras()
    {
        Scene persistentScene = EnsureSceneLoaded(PersistentScenePath, out bool openedScene);

        try
        {
            MonoBehaviour manager = FindComponent(persistentScene, "NetworkCharacterManager");
            Assert.That(manager, Is.Not.Null);

            MethodInfo setStandaloneCameraEnabled = manager.GetType().GetMethod(
                "SetStandaloneCameraEnabled",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(setStandaloneCameraEnabled, Is.Not.Null);

            List<Camera> standaloneCameras = FindStandaloneCameras(persistentScene);
            Assert.That(standaloneCameras, Is.Not.Empty);

            try
            {
                setStandaloneCameraEnabled.Invoke(null, new object[] { false });
                foreach (Camera camera in standaloneCameras)
                {
                    Assert.That(
                        camera.enabled,
                        Is.False,
                        $"{camera.name} 在联机模式下仍然启用。");
                }
            }
            finally
            {
                setStandaloneCameraEnabled.Invoke(null, new object[] { true });
            }
        }
        finally
        {
            CloseIfOpened(persistentScene, openedScene);
        }
    }

    private static Scene EnsureSceneLoaded(string path, out bool openedScene)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (scene.IsValid() && scene.isLoaded)
        {
            openedScene = false;
            return scene;
        }

        openedScene = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static void CloseIfOpened(Scene scene, bool openedScene)
    {
        if (openedScene && scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static MonoBehaviour FindComponent(Scene scene, string typeName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour component = FindComponent(root, typeName);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static MonoBehaviour FindComponent(GameObject root, string typeName)
    {
        foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null && component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static List<Transform> FindTransformsWithComponent(
        Scene scene,
        string typeName)
    {
        var results = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour component in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null && component.GetType().Name == typeName)
                {
                    results.Add(component.transform);
                }
            }
        }

        return results;
    }

    private static List<Camera> FindStandaloneCameras(Scene scene)
    {
        var results = new List<Camera>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                bool belongsToNetworkPlayer = false;
                foreach (MonoBehaviour component in
                         camera.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (component != null
                        && component.GetType().Name == "NetworkIdentity")
                    {
                        belongsToNetworkPlayer = true;
                        break;
                    }
                }

                if (!belongsToNetworkPlayer)
                {
                    results.Add(camera);
                }
            }
        }

        return results;
    }


    [Test]
    public void PresentationModeTogglesStandalonePlayerAndCamerasTogether()
    {
        Scene persistentScene = EnsureSceneLoaded(PersistentScenePath, out bool openedScene);

        try
        {
            MonoBehaviour manager = FindComponent(persistentScene, "NetworkCharacterManager");
            Assert.That(manager, Is.Not.Null);

            MethodInfo applyPresentationMode = manager.GetType().GetMethod(
                "ApplyPresentationMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(applyPresentationMode, Is.Not.Null);

            List<GameObject> standalonePlayers = FindStandalonePlayers(persistentScene);
            Assert.That(standalonePlayers, Is.Not.Empty);

            List<Camera> standaloneCameras = FindStandaloneCameras(persistentScene);
            Assert.That(standaloneCameras, Is.Not.Empty);

            try
            {
                applyPresentationMode.Invoke(null, new object[] { false });
                foreach (GameObject player in standalonePlayers)
                {
                    Assert.That(player.activeSelf, Is.True, $"{player.name} 在单机模式下不应被隐藏。");
                }

                applyPresentationMode.Invoke(null, new object[] { true });
                foreach (GameObject player in standalonePlayers)
                {
                    Assert.That(player.activeSelf, Is.False, $"{player.name} 在联机模式下仍然可见。");
                }

                foreach (Camera camera in standaloneCameras)
                {
                    Assert.That(camera.enabled, Is.False, $"{camera.name} 在联机模式下仍然启用。");
                }
            }
            finally
            {
                applyPresentationMode.Invoke(null, new object[] { false });
            }
        }
        finally
        {
            CloseIfOpened(persistentScene, openedScene);
        }
    }

    private static List<GameObject> FindStandalonePlayers(Scene scene)
    {
        var results = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour component in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component.GetType().Name != "PlayerCharacterController")
                {
                    continue;
                }

                bool belongsToNetworkPlayer = false;
                foreach (MonoBehaviour parent in
                         component.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (parent != null && parent.GetType().Name == "NetworkIdentity")
                    {
                        belongsToNetworkPlayer = true;
                        break;
                    }
                }

                if (!belongsToNetworkPlayer)
                {
                    results.Add(component.gameObject);
                }
            }
        }

        return results;
    }
}
