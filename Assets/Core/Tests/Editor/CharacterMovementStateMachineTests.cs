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
        object currentState = machine.GetType().GetProperty("CurrentStateId", PublicInstance).GetValue(machine);
        Assert.That(Enum.GetName(stateIdType, currentState), Is.EqualTo(expectedName));
    }

    private static Type RuntimeType(string typeName)
    {
        Type type = Type.GetType($"Core.Scripts.Character.Movement.States.{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"找不到运行时类型 {typeName}。");
        return type;
    }
}
