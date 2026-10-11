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

    [Test]
    public void StateMachineAccumulatesTimeInState()
    {
        Type stateMachineType = RuntimeType("CharacterMovementStateMachine");
        Type contextType = RuntimeType("CharacterMovementStateContext");
        object machine = Activator.CreateInstance(stateMachineType);

        // 构造后停在 Idle，尚未累计任何时长。
        Assert.That(TimeInState(machine), Is.EqualTo(0f));

        // 保持同一状态时逐帧累加。
        UpdateWithDeltaTime(machine, contextType, false, 0.4f);
        Assert.That(TimeInState(machine), Is.EqualTo(0.4f).Within(0.0001f));
        UpdateWithDeltaTime(machine, contextType, false, 0.4f);
        Assert.That(TimeInState(machine), Is.EqualTo(0.8f).Within(0.0001f));

        // 发生转换的那一帧归零。
        UpdateWithDeltaTime(machine, contextType, true, 0.4f);
        Assert.That(TimeInState(machine), Is.EqualTo(0f));

        // 之后从零重新累计。
        UpdateWithDeltaTime(machine, contextType, true, 0.4f);
        Assert.That(TimeInState(machine), Is.EqualTo(0.4f).Within(0.0001f));
    }

    // 构造带帧长的完整上下文：贴地，hasMoveInput 决定进入 Run 还是保持 Idle。
    private static void UpdateWithDeltaTime(
        object machine,
        Type contextType,
        bool hasMoveInput,
        float deltaTime)
    {
        object context = Activator.CreateInstance(
            contextType,
            true,
            true,
            hasMoveInput,
            false,
            false,
            -2f,
            false,
            deltaTime,
            0f);

        machine.GetType().GetMethod("Update", PublicInstance).Invoke(machine, new[] { context });
    }

    [Test]
    public void AirborneStatesIgnoreAirborneToleranceWindow()
    {
        Type stateMachineType = RuntimeType("CharacterMovementStateMachine");
        Type contextType = RuntimeType("CharacterMovementStateContext");
        Type stateIdType = RuntimeType("CharacterMovementStateId");
        object machine = Activator.CreateInstance(stateMachineType);

        // 起跳当帧离地计时被清零，此后整个容忍窗口内 withinAirborneTolerance 都为 true。
        Update(machine, contextType, true, true, false, false, true, 6f);
        AssertState(machine, stateIdType, "Rising");

        // 角色确实在空中：容忍窗口只在地面子树内抑制悬空帧，不能把空中状态拉回地面。
        UpdateWithTolerance(machine, contextType, false, false, false, false, false, 6f, true);
        AssertState(machine, stateIdType, "Rising");

        // 窗口内竖直速度转负只切换空中状态。
        UpdateWithTolerance(machine, contextType, false, false, false, false, false, -1f, true);
        AssertState(machine, stateIdType, "Falling");

        // 真实接地仍然立刻回到地面子树。
        UpdateWithTolerance(machine, contextType, false, true, false, false, false, -2f, true);
        AssertState(machine, stateIdType, "Idle");
    }

    // 使用带离地容忍判定的七参数上下文，覆盖空中子树对该窗口的处理。
    private static void UpdateWithTolerance(
        object machine,
        Type contextType,
        bool groundedBeforeMove,
        bool groundedAfterMove,
        bool hasMoveInput,
        bool sprintHeld,
        bool jumpStarted,
        float verticalVelocity,
        bool withinAirborneTolerance)
    {
        object context = Activator.CreateInstance(
            contextType,
            groundedBeforeMove,
            groundedAfterMove,
            hasMoveInput,
            sprintHeld,
            jumpStarted,
            verticalVelocity,
            withinAirborneTolerance);

        machine.GetType().GetMethod("Update", PublicInstance).Invoke(machine, new[] { context });
    }

    private static float TimeInState(object machine)
    {
        return (float)machine.GetType().GetProperty("TimeInState", PublicInstance).GetValue(machine);
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

    private static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType($"Core.Scripts.Character.Movement.States.{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"找不到运行时类型 {typeName}。");
        return type;
    }
}
