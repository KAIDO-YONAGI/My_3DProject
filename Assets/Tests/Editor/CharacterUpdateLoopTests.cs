using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterUpdateLoopTests
{
    private const string CharacterNamespace = "Core.Scripts.Character.";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private readonly List<Component> controllers = new List<Component>();
    private readonly List<object> animationDrivers = new List<object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Component controller in controllers)
        {
            object driver = Field(controller, "animationDriver");
            if (driver != null)
            {
                ClearOverride(driver);
            }
        }

        foreach (object driver in animationDrivers)
        {
            ClearOverride(driver);
        }

        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(objects[index]);
            }
        }

        animationDrivers.Clear();
        controllers.Clear();
        objects.Clear();
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
        Invoke(controller, "RebindAnimator", animator, null);
        object driver = Field(controller, "animationDriver");
        ClearOverride(driver);

        Invoke(controller, "RebindAnimator", null, null);
        Invoke(controller, "ApplyRemoteAnimation");

        Assert.That(Field(controller, "animator"), Is.Null);
        Assert.That(Field(controller, "animationDriver"), Is.Null);
    }

    [Test]
    public void ExplicitModelBindingPreservesMotor()
    {
        Component controller = CreateController();
        object motor = Field(controller, "motor");
        Animator animator = CreateAnimator(controller.transform);

        Invoke(controller, "RebindAnimator", animator, null);
        object driver = Field(controller, "animationDriver");
        animationDrivers.Add(driver);

        Assert.That(Field(controller, "motor"), Is.SameAs(motor));
        Assert.That(Field(controller, "animator"), Is.SameAs(animator));
        Assert.That(driver, Is.Not.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BoundLocomotionClipsKeepTheirBlendUntilModelRebinding(bool sprintHeld)
    {
        object set = CreateAnimationSet(true);
        Animator animator = CreateAnimator();
        object driver = CreateDriver(
            animator,
            sprintHeld ? CreateAnimationSet(false) : set,
            sprintHeld ? set : CreateAnimationSet(false));

        ((IList)Field(set, "samples")).Clear();
        Apply(driver, true, sprintHeld, 1f);

        Assert.That(animator.GetFloat(sprintHeld ? "SprintWeight" : "RunWeight"), Is.EqualTo(1f));
        Assert.That(animator.GetFloat("IdleWeight"), Is.Zero);
    }

    [TestCase(false, true, false, 0f, 1f)]
    [TestCase(true, false, true, 1f, 0f)]
    [TestCase(false, false, true, 0f, 0f)]
    public void MissingClipsKeepExistingBlendFallback(
        bool hasRun,
        bool hasSprint,
        bool sprintHeld,
        float expectedRun,
        float expectedSprint)
    {
        Animator animator = CreateAnimator();
        object driver = CreateDriver(
            animator, CreateAnimationSet(hasRun), CreateAnimationSet(hasSprint));

        Apply(driver, true, sprintHeld, 1f);

        Assert.That(animator.GetFloat("RunWeight"), Is.EqualTo(expectedRun));
        Assert.That(animator.GetFloat("SprintWeight"), Is.EqualTo(expectedSprint));
        Assert.That(animator.GetFloat("IdleWeight"), Is.EqualTo(1f - expectedRun - expectedSprint));
    }

    [Test]
    public void LandingResetsWeightsForOneFrame()
    {
        Animator animator = CreateAnimator();
        object driver = CreateDriver(animator, CreateAnimationSet(true), CreateAnimationSet(false));

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
        controllers.Add(controller);
        Invoke(controller, "EnsureRuntimeState");
        return controller;
    }

    private Animator CreateAnimator(Transform parent = null)
    {
        GameObject model = CreateObject("CharacterUpdateLoopModel");
        model.transform.SetParent(parent, false);
        return model.AddComponent<Animator>();
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        objects.Add(gameObject);
        return gameObject;
    }

    private AnimationClip CreateClip()
    {
        var clip = new AnimationClip();
        objects.Add(clip);
        return clip;
    }

    private object CreateAnimationSet(bool hasClip)
    {
        object set = Activator.CreateInstance(RuntimeType("Animation.DirectionalAnimationSet"));
        if (hasClip)
        {
            object sample = Activator.CreateInstance(
                RuntimeType("Animation.DirectionalAnimationSample"), "Forward", Vector2.up, CreateClip());
            ((IList)Field(set, "samples")).Add(sample);
        }

        return set;
    }

    private object CreateDriver(Animator animator, object runSet, object sprintSet)
    {
        object driver = Activator.CreateInstance(
            RuntimeType("Animation.CharacterAnimator"), animator, CreateClip(), runSet, sprintSet, null);
        animationDrivers.Add(driver);
        Assert.That(Invoke(driver, "Initialize"), Is.EqualTo(true));
        return driver;
    }

    private static void Apply(object driver, bool grounded, bool sprintHeld, float weight)
    {
        object motion = Activator.CreateInstance(
            RuntimeType("Animation.CharacterMotion"), grounded, sprintHeld, Vector2.up, weight);
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

    private static void ClearOverride(object driver)
    {
        FieldInfo field = driver.GetType().GetField("overrideController", InstanceFields);
        var controller = field.GetValue(driver) as AnimatorOverrideController;
        if (controller != null)
        {
            var animator = Field(driver, "animator") as Animator;
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
            }

            UnityEngine.Object.DestroyImmediate(controller);
            field.SetValue(driver, null);
        }
    }
}
