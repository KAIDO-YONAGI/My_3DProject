using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraRotationMathTests
{
    private MethodInfo boundedStep;
    private MethodInfo exponentialFactor;
    private MethodInfo alignTarget;

    [SetUp]
    public void SetUp()
    {
        Type type = RuntimeType("CameraRotationMath");
        boundedStep = type.GetMethod("BoundedExponentialStep");
        exponentialFactor = type.GetMethod("ExponentialFactor");
        alignTarget = type.GetMethod("AlignTarget");
    }

    [TestCase(0.5f)]
    [TestCase(0.01f)]
    [TestCase(0.0001f)]
    public void SmallAnglesHaveNoArtificialDeadZone(float remaining)
    {
        float step = Step(remaining, 15f, 360f, 1f / 60f);
        Assert.That(step, Is.GreaterThan(0f).And.LessThan(remaining));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void FramePartitionsMatchContinuousLimitedExponentialModel(int framesPerSecond)
    {
        float remaining = 180f;
        float deltaTime = 1f / framesPerSecond;
        for (int frame = 0; frame < framesPerSecond; frame++)
        {
            float step = Step(remaining, 15f, 360f, deltaTime);
            Assert.That(step, Is.InRange(0f, Mathf.Min(remaining, 360f * deltaTime) + 0.0001f));
            remaining -= step;
        }

        double limitedDuration = (180d - 24d) / 360d;
        double expected = 24d * Math.Exp(-15d * (1d - limitedDuration));
        Assert.That(remaining, Is.EqualTo(expected).Within(0.0001d));
    }

    [Test]
    public void CrossingSpeedLimitInsideFrameMatchesSubdividedSteps()
    {
        float whole = 30f - Step(30f, 15f, 360f, 0.1f);
        float split = 30f;
        for (int index = 0; index < 10; index++)
        {
            split -= Step(split, 15f, 360f, 0.01f);
        }
        Assert.That(split, Is.EqualTo(whole).Within(0.0001f));
    }

    [TestCase(0f, 15f, 360f, 0.1f)]
    [TestCase(90f, 0f, 360f, 0.1f)]
    [TestCase(90f, -1f, 360f, 0.1f)]
    [TestCase(90f, 15f, 0f, 0.1f)]
    [TestCase(90f, 15f, -1f, 0.1f)]
    [TestCase(90f, 15f, 360f, 0f)]
    [TestCase(90f, 15f, 360f, -1f)]
    public void InvalidOrPausedParametersDoNotTurn(float remaining, float rate, float speed, float deltaTime)
    {
        Assert.That(Step(remaining, rate, speed, deltaTime), Is.Zero);
    }

    [Test]
    public void CameraSmoothingDoesNotSnapAtOneHundredMilliseconds()
    {
        float factor = Factor(10f, 0.1f);
        Assert.That(factor, Is.EqualTo(1d - Math.Exp(-1d)).Within(0.000001d));
        Assert.That(factor, Is.LessThan(1f));
        float partitioned = 1f - Mathf.Pow(1f - Factor(10f, 0.01f), 10);
        Assert.That(partitioned, Is.EqualTo(factor).Within(0.000001f));
    }

    [Test]
    public void TargetAlignmentUsesShortestPathAcrossYawWrap()
    {
        Quaternion current = Quaternion.Euler(0f, 359f, 0f);
        Quaternion next = (Quaternion)alignTarget.Invoke(null, new object[] { current, 1f, 15f, 360f, 1f / 60f });
        Assert.That(Quaternion.Angle(current, next), Is.InRange(0.1f, 0.5f));
        Assert.That(Quaternion.Angle(next, Quaternion.Euler(0f, 1f, 0f)), Is.LessThan(2f));
    }

    [Test]
    public void PausedOrbitCanResumeWithoutInvalidSmoothingVelocity()
    {
        Type type = RuntimeType("CameraOrbitState");
        object orbit = Activator.CreateInstance(type, true);
        type.GetMethod("Initialize").Invoke(orbit,
            new object[] { Vector3.back * 4f, Quaternion.identity, Vector3.zero });
        MethodInfo advance = type.GetMethod("Advance");
        Vector3 paused = (Vector3)advance.Invoke(orbit, new object[] { 4f, 0f, 10f, 0f });
        Assert.That(paused, Is.EqualTo(Vector3.back * 4f));
        Vector3 resumed = (Vector3)advance.Invoke(orbit, new object[] { 4f, 0f, 10f, 1f / 60f });
        Assert.That(float.IsNaN(resumed.sqrMagnitude) || float.IsInfinity(resumed.sqrMagnitude), Is.False);
    }

    private float Step(float remaining, float rate, float speed, float deltaTime)
    {
        return (float)boundedStep.Invoke(null, new object[] { remaining, rate, speed, deltaTime });
    }

    private float Factor(float rate, float deltaTime)
    {
        return (float)exponentialFactor.Invoke(null, new object[] { rate, deltaTime });
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
}
