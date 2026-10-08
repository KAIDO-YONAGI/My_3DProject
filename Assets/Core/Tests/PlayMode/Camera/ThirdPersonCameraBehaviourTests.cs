using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// 从真实相机层级、输入和物理结果验证行为，不把相机脱离角色层级后测试。
/// </summary>
public sealed class ThirdPersonCameraBehaviourTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private readonly List<GameObject> obstacles = new List<GameObject>();
    private Keyboard keyboard;
    private Mouse mouse;
    private GameObject targetObject;
    private GameObject cameraObject;
    private Component cameraComponent;

    [SetUp]
    public void SetUp()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        targetObject = new GameObject("CameraBehaviourTarget");
        targetObject.transform.position = new Vector3(10000f, 10000f, 10000f);
        cameraObject = new GameObject("CameraBehaviourUnderTest");
        cameraObject.SetActive(false);
        cameraObject.transform.SetParent(targetObject.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 2f, -4f);
        cameraObject.transform.rotation = Quaternion.identity;
        cameraComponent = cameraObject.AddComponent(RuntimeType("ThirdPersonCamera"));
        SetField("target", targetObject.transform);
        SetField("targetOffset", Vector3.up * 2f);
        SetField("minHeightY", 0f);
        SetField("lockCursor", false);
        SetField("collisionRadius", 0f);
        SetField("collisionLayers", (LayerMask)(1 << 30));
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(cameraObject);
        UnityEngine.Object.DestroyImmediate(targetObject);
        foreach (GameObject obstacle in obstacles)
        {
            UnityEngine.Object.DestroyImmediate(obstacle);
        }
        obstacles.Clear();
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(mouse);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [UnityTest]
    public IEnumerator ParentTurningDoesNotDragCameraWorldRotation()
    {
        cameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        Quaternion before = cameraObject.transform.rotation;

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return null;
        yield return null;

        Assert.That(Quaternion.Angle(targetObject.transform.rotation, Quaternion.identity),
            Is.GreaterThan(0.01f), "移动输入必须真的触发角色转向，避免空跑。");
        Assert.That(Quaternion.Angle(cameraObject.transform.rotation, before),
            Is.LessThan(0.01f), "角色父节点回正不能改变相机的世界朝向。");
    }

    [UnityTest]
    public IEnumerator WallAppearingClipsFinalSmoothedPositionImmediately()
    {
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        SetField("collisionRadius", 0.2f);
        CreateWall(new Vector3(0f, 2f, -1f), new Vector3(10f, 10f, 0.1f));
        Physics.SyncTransforms();
        yield return null;
        yield return null;

        Assert.That(cameraObject.transform.position.z - targetObject.transform.position.z,
            Is.GreaterThan(-0.76f), "不能用位置平滑把镜头留在墙后，也不能用 minDistance 推回墙后。");
        Assert.That((float)GetField("distance"), Is.EqualTo(4f), "避让不能改写用户的期望缩放距离。");
    }

    [UnityTest]
    public IEnumerator RaisedCandidateUsesItsActualCollisionPath()
    {
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        SetField("targetOffset", Vector3.zero);
        SetField("minHeightY", 2f);
        SetField("collisionRadius", 0.2f);
        SetField("smoothSpeed", 100f);
        CreateWall(new Vector3(0f, 1f, -2f), new Vector3(10f, 0.5f, 0.1f));
        Physics.SyncTransforms();
        Invoke("LateUpdate");

        Assert.That(cameraObject.transform.position.z - targetObject.transform.position.z,
            Is.GreaterThan(-1.9f), "抬高后的最终路径有障碍，不能只检测抬高前的水平射线。");
    }

    [UnityTest]
    public IEnumerator DisablingAndEnablingRestoresConfiguredCursorLock()
    {
        SetField("lockCursor", true);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        ((Behaviour)cameraComponent).enabled = false;
        Assert.That(Cursor.visible, Is.True);
        ((Behaviour)cameraComponent).enabled = true;
        yield return null;
        yield return null;
        Assert.That(Cursor.visible, Is.False, "重新启用应恢复 lockCursor 的配置意图。");
    }

    [UnityTest]
    public IEnumerator LeftClickRecaptureDiscardsThatFramesMouseDelta()
    {
        SetField("lockCursor", true);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        Quaternion before = cameraObject.transform.rotation;

        InputSystem.QueueStateEvent(mouse,
            new MouseState { delta = new Vector2(100f, 0f) }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        Assert.That(Quaternion.Angle(cameraObject.transform.rotation, before),
            Is.LessThan(0.01f), "重新捕获当帧的增量可能来自光标回中，必须丢弃。");
    }

    [UnityTest]
    public IEnumerator DestroyedTargetPausesWithoutExceptionAndCanBeRebound()
    {
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        cameraObject.transform.SetParent(null, true);
        UnityEngine.Object.DestroyImmediate(targetObject);
        targetObject = null;
        Assert.DoesNotThrow(() => Invoke("LateUpdate"), "目标失效时不能继续访问其 Transform。");

        targetObject = new GameObject("ReplacementCameraTarget");
        targetObject.transform.position = new Vector3(10010f, 10000f, 10000f);
        cameraComponent.GetType().GetMethod("SetTarget").Invoke(cameraComponent,
            new object[] { targetObject.transform });
        yield return null;
        yield return null;
        Assert.That(((Behaviour)cameraComponent).enabled, Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator UnstartedDisabledCameraDoesNotReleaseActiveCamerasCursor()
    {
        SetField("lockCursor", true);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        GameObject remote = new GameObject("UnstartedRemoteCamera");
        try
        {
            Behaviour controller = (Behaviour)remote.AddComponent(RuntimeType("ThirdPersonCamera"));
            controller.enabled = false;
            Assert.That(Cursor.visible, Is.False, "未持有光标的组件停用不能释放本地相机的光标。");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(remote);
        }
    }

    [TestCase(20)]
    [TestCase(140)]
    public void DenseSelfCollidersCannotHideExternalWall(int count)
    {
        Vector3 focus = targetObject.transform.position + Vector3.up * 2f;
        for (int index = 0; index < count; index++)
        {
            GameObject body = new GameObject("IgnoredTargetCollider");
            body.transform.SetParent(targetObject.transform, false);
            body.transform.localPosition = new Vector3(0f, 2f, -0.5f - 3f * index / count);
            body.layer = 30;
            body.AddComponent<BoxCollider>().size = Vector3.one * 0.03f;
        }
        CreateWall(new Vector3(0f, 2f, -1.4f), new Vector3(10f, 10f, 0.1f));
        Physics.SyncTransforms();

        RaycastHit[] hits = Physics.SphereCastAll(focus, 0.2f, Vector3.back, 4f,
            1 << 30, QueryTriggerInteraction.Ignore);
        Assert.That(hits.Length, Is.GreaterThan(count), "必须真的填满命中缓冲区，不能空跑扩容用例。");
        Vector3 resolved = ResolveCollision(focus, Vector3.back * 4f, 0.2f, 1 << 30);
        Assert.That(resolved.z, Is.InRange(-1.15f, -1.12f),
            "忽略自身碰撞体后仍应找到墙；球心行程不能再扣一次半径。");
    }

    [Test]
    public void CollisionRadiusAndLayerMaskCanDisableAvoidance()
    {
        CreateWall(new Vector3(0f, 2f, -1f), Vector3.one);
        Physics.SyncTransforms();
        Vector3 focus = targetObject.transform.position + Vector3.up * 2f;
        Assert.That(ResolveCollision(focus, Vector3.back * 4f, 0f, 1 << 30),
            Is.EqualTo(Vector3.back * 4f));
        Assert.That(ResolveCollision(focus, Vector3.back * 4f, 0.2f, 1 << 29),
            Is.EqualTo(Vector3.back * 4f));
    }

    [UnityTest]
    public IEnumerator RemovingObstacleRestoresDistanceSmoothly()
    {
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        SetField("collisionRadius", 0.2f);
        CreateWall(new Vector3(0f, 2f, -1f), new Vector3(10f, 10f, 0.1f));
        Physics.SyncTransforms();
        Invoke("LateUpdate");
        float clippedZ = cameraObject.transform.position.z - targetObject.transform.position.z;
        UnityEngine.Object.DestroyImmediate(obstacles[0]);
        Physics.SyncTransforms();

        yield return new WaitForSeconds(0.1f);
        float restoredZ = cameraObject.transform.position.z - targetObject.transform.position.z;
        Assert.That(restoredZ, Is.LessThan(clippedZ - 0.01f).And.GreaterThan(-3.99f),
            "障碍消失后应继续向期望距离退回，但不能一帧弹回。");
        Assert.That((float)GetField("distance"), Is.EqualTo(4f));
    }

    [Test]
    public void OldCursorOwnerCannotReleaseNewOwnersLock()
    {
        Type type = RuntimeType("CameraCursorController");
        object first = Activator.CreateInstance(type, true);
        object second = Activator.CreateInstance(type, true);
        try
        {
            type.GetMethod("Begin").Invoke(first, new object[] { true });
            type.GetMethod("Begin").Invoke(second, new object[] { true });
            type.GetMethod("Release").Invoke(first, null);
            Assert.That(Cursor.visible, Is.False);
            Assert.That((bool)type.GetProperty("IsLocked").GetValue(first), Is.False);
            Assert.That((bool)type.GetProperty("IsLocked").GetValue(second), Is.True);
        }
        finally
        {
            type.GetMethod("Release").Invoke(first, null);
            type.GetMethod("Release").Invoke(second, null);
        }
        Assert.That(Cursor.visible, Is.True);
    }

    [UnityTest]
    public IEnumerator DistanceLimitAppliesWithoutScrollInput()
    {
        SetField("distance", 12f);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        Assert.That((float)GetField("distance"), Is.EqualTo(10f));
    }

    [UnityTest]
    public IEnumerator LeftClickRecapturesAfterExternalCursorRelease()
    {
        SetField("lockCursor", true);
        cameraObject.SetActive(true);
        yield return null;
        yield return null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Quaternion before = cameraObject.transform.rotation;
        InputSystem.QueueStateEvent(mouse,
            new MouseState { delta = new Vector2(100f, 0f) }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        Assert.That(Cursor.visible, Is.False, "外部释放不能让内部状态阻止显式重新捕获。");
        Assert.That(Quaternion.Angle(cameraObject.transform.rotation, before), Is.LessThan(0.01f));
    }

    [Test]
    public void InitiallyOverlappingSphereCanMoveAwayFromWall()
    {
        CreateWall(new Vector3(0f, 2f, 0.6f), new Vector3(10f, 10f, 1f));
        Physics.SyncTransforms();
        Vector3 focus = targetObject.transform.position + Vector3.up * 2f;
        Assert.That(ResolveCollision(focus, Vector3.back * 4f, 0.2f, 1 << 30),
            Is.EqualTo(Vector3.back * 4f),
            "焦点在墙外、球体略有重叠时，朝远离墙体的安全路径不能被零距离命中永久压住。");
    }

    [Test]
    public void InitiallyOverlappingSphereCannotPassThroughWall()
    {
        CreateWall(new Vector3(0f, 2f, 0.6f), new Vector3(10f, 10f, 1f));
        Physics.SyncTransforms();
        Vector3 focus = targetObject.transform.position + Vector3.up * 2f;
        Assert.That(ResolveCollision(focus, Vector3.forward * 4f, 0.2f, 1 << 30).magnitude,
            Is.LessThan(0.01f), "不能为了允许脱离起点重叠而跳过真正挡路的墙。");
    }

    [Test]
    public void AnotherWallCannotClipSafeExitBackIntoInitialOverlap()
    {
        CreateWall(new Vector3(0f, 2f, 0.6f), new Vector3(10f, 10f, 1f));
        CreateWall(new Vector3(0f, 2f, -0.36f), new Vector3(10f, 10f, 0.1f));
        Physics.SyncTransforms();
        Vector3 focus = targetObject.transform.position + Vector3.up * 2f;
        Assert.That(ResolveCollision(focus, Vector3.back * 4f, 0.2f, 1 << 30).magnitude,
            Is.LessThan(0.01f), "另一面墙缩短候选位置后，必须重新证明终点已脱离起点重叠。");
    }

    private Vector3 ResolveCollision(Vector3 focus, Vector3 offset, float radius, int mask)
    {
        Type type = RuntimeType("CameraCollisionResolver");
        object resolver = Activator.CreateInstance(type, true);
        return (Vector3)type.GetMethod("Resolve").Invoke(resolver,
            new object[] { focus, offset, targetObject.transform, radius, (LayerMask)mask });
    }

    private void CreateWall(Vector3 localPosition, Vector3 size)
    {
        GameObject wall = new GameObject("CameraTestWall");
        obstacles.Add(wall);
        wall.layer = 30;
        wall.transform.position = targetObject.transform.position + localPosition;
        wall.AddComponent<BoxCollider>().size = size;
    }

    private void SetField(string name, object value)
    {
        cameraComponent.GetType().GetField(name, Members).SetValue(cameraComponent, value);
    }

    private object GetField(string name)
    {
        return cameraComponent.GetType().GetField(name, Members).GetValue(cameraComponent);
    }

    private void Invoke(string name)
    {
        cameraComponent.GetType().GetMethod(name, Members).Invoke(cameraComponent, null);
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
