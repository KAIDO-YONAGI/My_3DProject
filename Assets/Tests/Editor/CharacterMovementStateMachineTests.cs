using System;
using System.Reflection;
using NUnit.Framework;

public sealed class CharacterMovementStateMachineTests
{
    private static readonly BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    [Test]
    public void StateMachineTransitionsThroughGroundedAndAirborneStates()
    {
        Type stateMachineType = RuntimeType("CharacterMovementStateMachine");
        Type contextType = RuntimeType("CharacterMovementStateContext");
        Type stateIdType = RuntimeType("CharacterMovementStateId");

        object machine = Activator.CreateInstance(stateMachineType);

        AssertState(machine, stateIdType, "Idle");
        Update(machine, contextType, true, true, true, false, false, -2f);
        AssertState(machine, stateIdType, "Run");

        Update(machine, contextType, true, true, true, true, false, -2f);
        AssertState(machine, stateIdType, "Sprint");

        Update(machine, contextType, true, true, false, false, true, 6f);
        AssertState(machine, stateIdType, "Rising");

        Update(machine, contextType, false, false, false, false, false, 0f);
        AssertState(machine, stateIdType, "Falling");

        Update(machine, contextType, false, true, true, false, false, -2f);
        AssertState(machine, stateIdType, "Run");
    }

    [Test]
    public void StateMachineHandlesLedgeDropAndCeilingCollision()
    {
        Type stateMachineType = RuntimeType("CharacterMovementStateMachine");
        Type contextType = RuntimeType("CharacterMovementStateContext");
        Type stateIdType = RuntimeType("CharacterMovementStateId");
        object machine = Activator.CreateInstance(stateMachineType);

        Update(machine, contextType, true, true, true, false, false, -2f);
        AssertState(machine, stateIdType, "Run");

        Update(machine, contextType, true, false, true, false, false, -0.2f);
        AssertState(machine, stateIdType, "Falling");

        Update(machine, contextType, false, false, false, false, false, 0f);
        AssertState(machine, stateIdType, "Falling");

        Update(machine, contextType, false, false, false, false, false, -1f);
        AssertState(machine, stateIdType, "Falling");

        Update(machine, contextType, false, true, false, false, false, -2f);
        AssertState(machine, stateIdType, "Idle");
    }

    [Test]
    public void StateMachineReportsTransitionEdgeForPresentation()
    {
        Type stateMachineType = RuntimeType("CharacterMovementStateMachine");
        Type contextType = RuntimeType("CharacterMovementStateContext");
        Type stateIdType = RuntimeType("CharacterMovementStateId");
        object machine = Activator.CreateInstance(stateMachineType);

        // 构造后尚未发生转换，边沿信息与当前状态一致。
        Assert.That(Property<bool>(machine, "ChangedThisFrame"), Is.False);
        Assert.That(StateName(machine, stateIdType, "PreviousStateId"), Is.EqualTo("Idle"));

        // 没有移动输入时保持 Idle，不产生转换。
        Update(machine, contextType, true, true, false, false, false, -2f);
        Assert.That(Property<bool>(machine, "ChangedThisFrame"), Is.False);
        Assert.That(StateName(machine, stateIdType, "PreviousStateId"), Is.EqualTo("Idle"));

        // 发生转换的当帧给出上一个状态，供表现层做落地、起跳这类边沿检测。
        Update(machine, contextType, true, true, true, false, false, -2f);
        Assert.That(Property<bool>(machine, "ChangedThisFrame"), Is.True);
        Assert.That(StateName(machine, stateIdType, "PreviousStateId"), Is.EqualTo("Idle"));
        Assert.That(StateName(machine, stateIdType, "CurrentStateId"), Is.EqualTo("Run"));

        // 再次转换时边沿信息更新为紧邻的上一个状态。
        Update(machine, contextType, true, true, false, false, true, 6f);
        Assert.That(Property<bool>(machine, "ChangedThisFrame"), Is.True);
        Assert.That(StateName(machine, stateIdType, "PreviousStateId"), Is.EqualTo("Run"));
        Assert.That(StateName(machine, stateIdType, "CurrentStateId"), Is.EqualTo("Rising"));

        // 未发生转换的帧只清掉边沿标志，最近一次转换前的状态继续保留。
        Update(machine, contextType, false, false, false, false, false, 8f);
        Assert.That(Property<bool>(machine, "ChangedThisFrame"), Is.False);
        Assert.That(StateName(machine, stateIdType, "PreviousStateId"), Is.EqualTo("Run"));
        Assert.That(StateName(machine, stateIdType, "CurrentStateId"), Is.EqualTo("Rising"));
    }

    [Test]
    public void SyncedStateNumbersStayFrozen()
    {
        Type stateIdType = RuntimeType("CharacterMovementStateId");

        // 分层状态机新增的根、地面、空中三个父状态不进入枚举。
        // 这五个数值同时是网络线格式，服务端按 Falling 为上限校验，改动会破坏联机兼容。
        Assert.That(Enum.GetValues(stateIdType).Length, Is.EqualTo(5));
        Assert.That(Convert.ToByte(Enum.Parse(stateIdType, "Idle")), Is.EqualTo(0));
        Assert.That(Convert.ToByte(Enum.Parse(stateIdType, "Run")), Is.EqualTo(1));
        Assert.That(Convert.ToByte(Enum.Parse(stateIdType, "Sprint")), Is.EqualTo(2));
        Assert.That(Convert.ToByte(Enum.Parse(stateIdType, "Rising")), Is.EqualTo(3));
        Assert.That(Convert.ToByte(Enum.Parse(stateIdType, "Falling")), Is.EqualTo(4));
    }

    private static void Update(
        object machine,
        Type contextType,
        bool groundedBeforeMove,
        bool groundedAfterMove,
        bool hasMoveInput,
        bool sprintHeld,
        bool jumpStarted,
        float verticalVelocity)
    {
        object context = Activator.CreateInstance(
            contextType,
            groundedBeforeMove,
            groundedAfterMove,
            hasMoveInput,
            sprintHeld,
            jumpStarted,
            verticalVelocity);

        machine.GetType().GetMethod("Update", PublicInstance).Invoke(machine, new[] { context });
    }

    private static void AssertState(object machine, Type stateIdType, string expectedName)
    {
        Assert.That(StateName(machine, stateIdType, "CurrentStateId"), Is.EqualTo(expectedName));
    }

    private static string StateName(object machine, Type stateIdType, string propertyName)
    {
        object value = machine.GetType().GetProperty(propertyName, PublicInstance).GetValue(machine);
        return Enum.GetName(stateIdType, value);
    }

    private static T Property<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name, PublicInstance).GetValue(target);
    }

    private static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType($"Core.Scripts.Character.Movement.States.{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"找不到运行时类型 {typeName}。");
        return type;
    }
}
