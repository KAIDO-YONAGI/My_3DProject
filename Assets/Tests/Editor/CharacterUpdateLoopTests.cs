using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterUpdateLoopTests
{
    private const string CharacterNamespace = "Core.Scripts.Character.";
    private const string TemplatePath = "Assets/Core/Models/Resources/CharacterLocomotion.controller";
    private const string OverrideFolder = "Assets/Core/Animations/Characters/";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

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
    public void InitializationPreservesInspectorController()
    {
        Animator animator = CreateAnimator();
        var configuredController = new AnimatorOverrideController(
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TemplatePath));
        objects.Add(configuredController);
        animator.runtimeAnimatorController = configuredController;

        CreateDriver(animator);

        Assert.That(animator.runtimeAnimatorController, Is.SameAs(configuredController));
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.GetBool("Grounded"), Is.True);
        Assert.That(animator.GetFloat("IdleWeight"), Is.EqualTo(1f));
    }

    [Test]
    public void RemoteAnimationWaitsForExplicitModelBinding()
    {
        Component controller = CreateController();
        CreateAnimator(controller.transform);

        Invoke(controller, "ApplyRemoteAnimation");

        Assert.That(Field(controller, "animator"), Is.Null);
        Assert.That(Field(controller, "animationDriver"), Is.Null);
    }

    [Test]
    public void ClearedModelBindingStaysClearedWhileOldModelExists()
    {
        Component controller = CreateController();
        Animator animator = CreateAnimator(controller.transform);
        Invoke(controller, "RebindAnimator", animator);
        RuntimeAnimatorController configuredController = animator.runtimeAnimatorController;

        Invoke(controller, "RebindAnimator", new object[] { null });
        Invoke(controller, "ApplyRemoteAnimation");

        Assert.That(Field(controller, "animator"), Is.Null);
        Assert.That(Field(controller, "animationDriver"), Is.Null);
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(configuredController));
    }

    [Test]
    public void ExplicitModelBindingPreservesMotor()
    {
        Component controller = CreateController();
        object motor = Field(controller, "motor");
        Animator animator = CreateAnimator(controller.transform);

        Invoke(controller, "RebindAnimator", animator);
        object driver = Field(controller, "animationDriver");

        Assert.That(Field(controller, "motor"), Is.SameAs(motor));
        Assert.That(Field(controller, "animator"), Is.SameAs(animator));
        Assert.That(driver, Is.Not.Null);
    }

    [TestCase(false, 0f, 1f, 0f, 0f)]
    [TestCase(false, 0.4f, 0.6f, 0.4f, 0f)]
    [TestCase(true, 0.4f, 0.6f, 0f, 0.4f)]
    [TestCase(false, 2f, 0f, 1f, 0f)]
    [TestCase(true, -1f, 1f, 0f, 0f)]
    public void MotionDrivesConfiguredBlendParameters(
        bool sprintHeld, float weight, float expectedIdle, float expectedRun, float expectedSprint)
    {
        Animator animator = CreateAnimator();
        object driver = CreateDriver(animator);

        Apply(driver, true, sprintHeld, weight, new Vector2(-0.6f, 0.8f));

        Assert.That(animator.GetFloat("IdleWeight"), Is.EqualTo(expectedIdle).Within(0.0001f));
        Assert.That(animator.GetFloat("RunWeight"), Is.EqualTo(expectedRun).Within(0.0001f));
        Assert.That(animator.GetFloat("SprintWeight"), Is.EqualTo(expectedSprint).Within(0.0001f));
        Assert.That(animator.GetFloat("MoveX"), Is.EqualTo(-0.6f));
        Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(0.8f));
    }

    [Test]
    public void SharedOverrideKeepsAnimatorParametersIndependent()
    {
        var shared = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            OverrideFolder + "娜娜莉（华丽飞踢）.overrideController");
        Assert.That(shared, Is.Not.Null);
        Animator first = CreateAnimator();
        Animator second = CreateAnimator();
        first.runtimeAnimatorController = shared;
        second.runtimeAnimatorController = shared;
        object firstDriver = CreateDriver(first);
        Apply(firstDriver, true, true, 1f);
        object secondDriver = CreateDriver(second);
        Apply(secondDriver, true, false, 0.5f);

        Assert.That(first.runtimeAnimatorController, Is.SameAs(shared));
        Assert.That(second.runtimeAnimatorController, Is.SameAs(shared));
        Assert.That(first.GetFloat("SprintWeight"), Is.EqualTo(1f));
        Assert.That(second.GetFloat("SprintWeight"), Is.Zero);
        Assert.That(second.GetFloat("RunWeight"), Is.EqualTo(0.5f));
    }

    [Test]
    public void RebindingAndDestroyingPlayerPreservesSharedController()
    {
        Component controller = CreateController();
        var shared = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            OverrideFolder + "娜娜莉（华丽飞踢）.overrideController");
        Assert.That(shared, Is.Not.Null);
        Animator first = CreateAnimator(controller.transform);
        Animator second = CreateAnimator(controller.transform);
        first.runtimeAnimatorController = shared;
        second.runtimeAnimatorController = shared;

        Invoke(controller, "RebindAnimator", first);
        Invoke(controller, "RebindAnimator", second);
        Assert.That(first.runtimeAnimatorController, Is.SameAs(shared));
        Assert.That(second.runtimeAnimatorController, Is.SameAs(shared));

        UnityEngine.Object.DestroyImmediate(controller.gameObject);
        Assert.That(shared != null, Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            OverrideFolder + "娜娜莉（华丽飞踢）.overrideController"), Is.SameAs(shared));
    }

    [Test]
    public void MissingControllerLeavesAnimationUninitialized()
    {
        Animator animator = CreateAnimator();
        animator.runtimeAnimatorController = null;
        object driver = Activator.CreateInstance(
            RuntimeType("Animation.CharacterAnimator"), new object[] { animator });

        Assert.That(Invoke(driver, "Initialize"), Is.EqualTo(false));
        Assert.DoesNotThrow(() => Apply(driver, true, false, 1f));
        Assert.That(animator.runtimeAnimatorController, Is.Null);
    }

    [TestCase("娜娜莉（华丽飞踢）")]
    [TestCase("娜娜莉（学园之星）")]
    public void CharacterPrefabsShareCompleteEditorOverrides(string character)
    {
        var shared = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            OverrideFolder + character + ".overrideController");
        Assert.That(shared, Is.Not.Null);
        Assert.That(shared.runtimeAnimatorController,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TemplatePath)));
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        shared.GetOverrides(pairs);
        Assert.That(pairs.Count, Is.EqualTo(22));
        foreach (var pair in pairs)
        {
            Assert.That(pair.Value, Is.Not.Null, pair.Key.name);
            Assert.That(pair.Value, Is.Not.SameAs(pair.Key), pair.Key.name);
        }

        string path = "Assets/Core/Prefabs/Characters/" + character + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(prefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController,
            Is.SameAs(shared), path);
    }

    [Test]
    public void LandingResetsWeightsForOneFrame()
    {
        Animator animator = CreateAnimator();
        object driver = CreateDriver(animator);

        Apply(driver, false, false, 1f);
        Apply(driver, true, false, 1f);
        Assert.That(animator.GetBool("Grounded"), Is.True);
        Assert.That(animator.GetFloat("IdleWeight"), Is.EqualTo(1f));
        Assert.That(animator.GetFloat("RunWeight"), Is.Zero);

        Apply(driver, true, false, 1f);
        Assert.That(animator.GetFloat("RunWeight"), Is.EqualTo(1f));
        Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(1f));
    }

    private Component CreateController()
    {
        GameObject root = CreateObject("CharacterUpdateLoopTest");
        root.AddComponent<CharacterController>();
        Component controller = root.AddComponent(RuntimeType("Control.PlayerCharacterController"));
        Invoke(controller, "EnsureRuntimeState");
        return controller;
    }

    private Animator CreateAnimator(Transform parent = null)
    {
        GameObject model = CreateObject("CharacterUpdateLoopModel");
        model.transform.SetParent(parent, false);
        Animator animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TemplatePath);
        return animator;
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        objects.Add(gameObject);
        return gameObject;
    }

    private object CreateDriver(Animator animator)
    {
        object driver = Activator.CreateInstance(
            RuntimeType("Animation.CharacterAnimator"), new object[] { animator });
        Assert.That(Invoke(driver, "Initialize"), Is.EqualTo(true));
        return driver;
    }

    private static void Apply(
        object driver, bool grounded, bool sprintHeld, float weight, Vector2? direction = null)
    {
        object motion = Activator.CreateInstance(
            RuntimeType("Animation.CharacterMotion"), grounded, sprintHeld, direction ?? Vector2.up, weight);
        Invoke(driver, "Apply", motion);
    }

    private static object Field(object target, string name)
    {
        return target.GetType().GetField(name, InstanceFields).GetValue(target);
    }

    private static object Invoke(object target, string name, params object[] arguments)
    {
        return target.GetType().GetMethod(name, InstanceFields | BindingFlags.Public)
            .Invoke(target, arguments);
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType($"{CharacterNamespace}{name}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"找不到运行时类型 {name}。");
        return type;
    }

}
