public sealed class SimpleCharacterStateMachine
{
    public SimpleCharacterAnimationState State { get; private set; } = SimpleCharacterAnimationState.Grounded;
    public float StateElapsedTime { get; private set; }

    public SimpleCharacterAnimationState Tick(
        SimpleCharacterMotorFrame frame,
        float jumpStartExitTime,
        float landHoldTime,
        float deltaTime)
    {
        StateElapsedTime += deltaTime;

        switch (State)
        {
            case SimpleCharacterAnimationState.Grounded:
                if (frame.JumpStarted)
                {
                    ChangeState(SimpleCharacterAnimationState.JumpStart);
                }
                else if (!frame.IsGrounded && frame.VerticalSpeed > 0f)
                {
                    ChangeState(SimpleCharacterAnimationState.JumpLoop);
                }
                break;

            case SimpleCharacterAnimationState.JumpStart:
                if (frame.IsGrounded && frame.LandedThisFrame)
                {
                    ChangeState(SimpleCharacterAnimationState.Land);
                }
                else if (StateElapsedTime >= jumpStartExitTime)
                {
                    ChangeState(SimpleCharacterAnimationState.JumpLoop);
                }
                break;

            case SimpleCharacterAnimationState.JumpLoop:
                if (frame.LandedThisFrame || (frame.IsGrounded && frame.VerticalSpeed <= 0f))
                {
                    ChangeState(SimpleCharacterAnimationState.Land);
                }
                break;

            case SimpleCharacterAnimationState.Land:
                if (frame.JumpStarted)
                {
                    ChangeState(SimpleCharacterAnimationState.JumpStart);
                }
                else if (!frame.IsGrounded && frame.VerticalSpeed > 0f)
                {
                    ChangeState(SimpleCharacterAnimationState.JumpLoop);
                }
                else if (StateElapsedTime >= landHoldTime)
                {
                    ChangeState(SimpleCharacterAnimationState.Grounded);
                }
                break;
        }

        return State;
    }

    private void ChangeState(SimpleCharacterAnimationState nextState)
    {
        if (State == nextState)
        {
            return;
        }

        State = nextState;
        StateElapsedTime = 0f;
    }
}
