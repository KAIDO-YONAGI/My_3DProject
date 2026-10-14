using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterVisualConfigurationTests
{
    [TestCase("娜娜莉（华丽飞踢）")]
    [TestCase("娜娜莉（学园之星）")]
    public void SharedCharacterPrefabOwnsCompleteEditorConfiguration(string character)
    {
        Type visualType = Type.GetType(
            "Core.Scripts.Character.Presentation.CharacterVisual, Assembly-CSharp");
        Assert.That(visualType, Is.Not.Null, "视觉配置由 CharacterVisual 保存。");
            string path = "Assets/Core/Prefabs/Characters/" + character + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Regular), path);
            Component visual = prefab.GetComponent(visualType);
            Assert.That(visual, Is.Not.Null, path);
            var serialized = new SerializedObject(visual);
            Assert.That(serialized.FindProperty("animator").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<Animator>()), path);
            AssertReferences(serialized, prefab.transform, "cameras");
            AssertReferences(serialized, prefab.transform, "listeners");
            AssertReferences(serialized, prefab.transform, "cameraControllers");
            Assert.That(serialized.FindProperty("cameras").arraySize,
                Is.GreaterThan(0), path);
            Assert.That(serialized.FindProperty("inputCamera").objectReferenceValue,
                Is.Not.Null, path);
            SerializedProperty follower = serialized.FindProperty("ponytailFollower");
            Assert.That(follower.FindPropertyRelative("poseCaptured").boolValue, Is.True, path);
            foreach (string field in new[] { "headBone", "hairRigRoot" })
            {
                var bone = follower.FindPropertyRelative(field).objectReferenceValue as Transform;
                Assert.That(bone, Is.Not.Null, path);
                Assert.That(bone.IsChildOf(prefab.transform), Is.True, path);
            }
            Type controllerType = Type.GetType(
                "Core.Scripts.Character.Control.PlayerCharacterController, Assembly-CSharp");
            Assert.That(prefab.GetComponentsInChildren(controllerType, true).Length,
                Is.EqualTo(1), path);
            Assert.That(prefab.GetComponentsInChildren<CharacterController>(true).Length,
                Is.EqualTo(1), path);
            var movement = prefab.GetComponent(controllerType) as Behaviour;
            Assert.That(movement.enabled, Is.True, path);
            Assert.That(serialized.FindProperty("standaloneController").objectReferenceValue,
                Is.SameAs(movement), path);
            Assert.That(serialized.FindProperty("movementCollider").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<CharacterController>()), path);
            Assert.That(prefab.GetComponent<Animator>().applyRootMotion, Is.False, path);
            foreach (Camera camera in prefab.GetComponentsInChildren<Camera>(true))
                Assert.That(camera.enabled, Is.True, path);
            foreach (AudioListener listener in prefab.GetComponentsInChildren<AudioListener>(true))
                Assert.That(listener.enabled, Is.True, path);
    }

    private static void AssertReferences(SerializedObject serialized, Transform root, string field)
    {
        SerializedProperty references = serialized.FindProperty(field);
        var unique = new HashSet<UnityEngine.Object>();
        for (int index = 0; index < references.arraySize; index++)
        {
            var component = references.GetArrayElementAtIndex(index).objectReferenceValue as Component;
            Assert.That(component, Is.Not.Null, field);
            Assert.That(component.transform.IsChildOf(root), Is.True, field);
            Assert.That(unique.Add(component), Is.True, field);
        }
    }
}
