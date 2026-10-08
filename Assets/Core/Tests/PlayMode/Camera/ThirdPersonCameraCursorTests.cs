using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// 验证 ThirdPersonCamera 的光标规则：Alt 按住期间释放鼠标并暂停注视，松开后按进入前的意图恢复，
/// 同时保持 Escape 释放与左键重新捕获的原有行为。
/// 编辑器会自行接管 Cursor.lockState，因此“是否锁定”以组件内部状态为准，再用 Cursor.visible 验证真实生效。
/// </summary>
public sealed class ThirdPersonCameraCursorTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    private Keyboard keyboard;
    private Mouse mouse;
    private GameObject targetObject;
    private GameObject cameraObject;
    private Component cameraComponent;

    [SetUp]
    public void SetUp()
    {
        // 自建设备并向其排队状态事件，避免测试结果受真实键鼠状态影响。
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();

        targetObject = new GameObject("CameraTarget");
        cameraObject = new GameObject("CameraUnderTest");
        cameraComponent = cameraObject.AddComponent(RuntimeType("ThirdPersonCamera"));
        SetField(cameraComponent, "target", targetObject.transform);
        SetField(cameraComponent, "lockCursor", true);
    }

    [TearDown]
    public void TearDown()
    {
        // 先销毁被测对象释放输入资源，再移除测试设备并恢复光标。
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(targetObject);

        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard);
        }

        if (mouse != null)
        {
            InputSystem.RemoveDevice(mouse);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [UnityTest]
    public IEnumerator StartLocksCursorAndHoldingAltReleasesIt()
    {
        yield return null;
        yield return null;

        Assert.That(CursorLocked(), Is.True, "lockCursor 为真时相机应锁定光标。");
        Assert.That(Cursor.visible, Is.False);

        SetAltHeld(true);
        yield return null;
        yield return null;

        Assert.That(CursorLocked(), Is.False, "按住 Alt 应释放光标。");
        Assert.That(Cursor.visible, Is.True, "按住 Alt 时光标应可见。");
    }

    [UnityTest]
    public IEnumerator ReleasingAltRestoresCursorLock()
    {
        yield return null;
        yield return null;

        SetAltHeld(true);
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.False);

        SetAltHeld(false);
        yield return null;
        yield return null;

        if (!Application.isFocused)
        {
            Assert.Ignore("窗口未获得焦点时相机按设计不抢回鼠标，跳过重新锁定断言。");
        }

        Assert.That(CursorLocked(), Is.True, "松开 Alt 后应恢复锁定。");
        Assert.That(Cursor.visible, Is.False);
    }

    [UnityTest]
    public IEnumerator AltReleaseKeepsCursorFreeWhenItWasAlreadyReleased()
    {
        yield return null;
        yield return null;

        PressKey(Key.Escape);
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.False, "Escape 应释放光标。");

        ReleaseAllKeys();
        yield return null;

        SetAltHeld(true);
        yield return null;
        yield return null;

        SetAltHeld(false);
        yield return null;
        yield return null;

        Assert.That(CursorLocked(), Is.False, "Alt 之前已释放光标时，松开 Alt 不应抢回鼠标。");
    }

    [UnityTest]
    public IEnumerator MouseLookAndZoomReactOnlyWhileCursorIsCaptured()
    {
        yield return null;
        yield return null;

        float yawBefore = Yaw();
        float distanceBefore = Distance();

        QueueMouse(new Vector2(100f, 0f), 120f);
        yield return null;
        yield return null;

        float yawAfterLook = Yaw();
        float distanceAfterZoom = Distance();

        // 量纲核对：<Mouse>/delta 100 * LookScale 0.1 * sensitivity 2 = 20 度；
        // 滚轮一格 120 * ZoomScale(0.1/120) * scrollSpeed 2 = 0.2，与旧 Mouse X/Y、Mouse ScrollWheel 轴的 0.1 灵敏度等价。
        Assert.That(yawAfterLook - yawBefore, Is.EqualTo(20f).Within(0.001f), "鼠标位移换算应等价于旧 Mouse X 轴灵敏度 0.1。");
        Assert.That(distanceBefore - distanceAfterZoom, Is.EqualTo(0.2f).Within(0.001f), "滚轮一格换算应等价于旧 Mouse ScrollWheel 轴灵敏度 0.1。");

        SetAltHeld(true);
        yield return null;
        yield return null;

        QueueMouse(new Vector2(100f, 0f), 120f);
        yield return null;
        yield return null;

        Assert.That(Yaw(), Is.EqualTo(yawAfterLook).Within(0.0001f), "按住 Alt 期间镜头不应响应鼠标位移。");
        Assert.That(Distance(), Is.EqualTo(distanceAfterZoom).Within(0.0001f), "按住 Alt 期间镜头不应响应滚轮。");
    }

    [UnityTest]
    public IEnumerator EscapeReleasesCursorAndLeftClickRecapturesIt()
    {
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.True);

        PressKey(Key.Escape);
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.False, "Escape 应释放光标。");

        ReleaseAllKeys();
        yield return null;

        QueueMouse(default, 0f, true);
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.True, "lockCursor 为真时左键应重新捕获光标。");
    }

    [UnityTest]
    public IEnumerator LeftClickDoesNotRecaptureCursorWhileAltIsHeld()
    {
        yield return null;
        yield return null;

        SetAltHeld(true);
        yield return null;
        yield return null;
        Assert.That(CursorLocked(), Is.False);

        QueueMouse(default, 0f, true);
        yield return null;
        yield return null;

        Assert.That(CursorLocked(), Is.False, "按住 Alt 期间左键用于操作 UI，不应重新锁定光标。");
    }

    // Alt 的按下与松开都通过自建设备的状态事件模拟，不依赖操作系统输入。
    private void SetAltHeld(bool held)
    {
        InputSystem.QueueStateEvent(keyboard, held ? new KeyboardState(Key.LeftAlt) : new KeyboardState());
    }

    private void PressKey(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
    }

    private void ReleaseAllKeys()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }

    // 鼠标位移与滚轮属于 delta 控件，每次输入更新开始时会自动归零，因此单次排队只影响一帧。
    private void QueueMouse(Vector2 delta, float scroll, bool? leftButton = null)
    {
        MouseState state = new MouseState
        {
            delta = delta,
            scroll = new Vector2(0f, scroll)
        };

        if (leftButton.HasValue)
        {
            state = state.WithButton(MouseButton.Left, leftButton.Value);
        }

        InputSystem.QueueStateEvent(mouse, state);
    }

    private bool CursorLocked()
    {
        object cursor = GetField(cameraComponent, "cursor");
        return (bool)cursor.GetType().GetProperty("IsLocked").GetValue(cursor);
    }

    private float Yaw()
    {
        object orbit = GetField(cameraComponent, "orbit");
        return (float)orbit.GetType().GetProperty("Yaw").GetValue(orbit);
    }

    private float Distance()
    {
        return (float)GetField(cameraComponent, "distance");
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
        FieldInfo field = target.GetType().GetField(name, Fields);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }
}
