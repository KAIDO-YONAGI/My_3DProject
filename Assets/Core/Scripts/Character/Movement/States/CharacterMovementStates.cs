internal class IdleCharacterMovementState : CharacterMovementStateBase
{
    public override CharacterMovementStateId Id => CharacterMovementStateId.Idle;

    public override CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        if (context.JumpStarted)
        {
            return CharacterMovementStateId.Rising;
        }

        return ResolveGroundedState(context);
    }
}

internal class RunCharacterMovementState : CharacterMovementStateBase
{
    public override CharacterMovementStateId Id => CharacterMovementStateId.Run;

    public override CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        if (context.JumpStarted)
        {
            return CharacterMovementStateId.Rising;
        }

        return ResolveGroundedState(context);
    }
}

internal class SprintCharacterMovementState : CharacterMovementStateBase
{
    public override CharacterMovementStateId Id => CharacterMovementStateId.Sprint;

    public override CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        if (context.JumpStarted)
        {
            return CharacterMovementStateId.Rising;
        }

        return ResolveGroundedState(context);
    }
}

internal class RisingCharacterMovementState : CharacterMovementStateBase
{
    public override CharacterMovementStateId Id => CharacterMovementStateId.Rising;

    public override CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        if (context.GroundedAfterMove)
        {
            return ResolveGroundedState(context);
        }

        return context.VerticalVelocity <= 0f
            ? CharacterMovementStateId.Falling
            : CharacterMovementStateId.Rising;
    }
}

internal class FallingCharacterMovementState : CharacterMovementStateBase
{
    public override CharacterMovementStateId Id => CharacterMovementStateId.Falling;

    public override CharacterMovementStateId Update(CharacterMovementStateContext context)
    {
        if (context.GroundedAfterMove)
        {
            return ResolveGroundedState(context);
        }

        return context.VerticalVelocity > 0f
            ? CharacterMovementStateId.Rising
            : CharacterMovementStateId.Falling;
    }
}
