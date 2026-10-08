using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class NetworkConnectionConfigurationTests
{
    [Test]
    public void AutoStartClientUsesNetworkManagerAddressConfiguration()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Core/Prefabs/NetworkManager.prefab");
        Assert.That(prefab, Is.Not.Null);
        MonoBehaviour autoStart = FindComponent(prefab, "AutoStartClient");
        MonoBehaviour manager = FindComponent(prefab, "NetworkManager");

        Assert.That(autoStart, Is.Not.Null);
        Assert.That(manager, Is.Not.Null);
        Assert.That(new SerializedObject(autoStart).FindProperty("connectAddress"), Is.Null);
        Assert.That(new SerializedObject(manager).FindProperty("networkAddress").stringValue,
            Is.EqualTo("127.0.0.1"));
    }

    private static MonoBehaviour FindComponent(GameObject root, string name)
    {
        foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
        {
            if (component != null && component.GetType().Name == name)
            {
                return component;
            }
        }

        throw new InvalidOperationException("Component not found: " + name);
    }
}
