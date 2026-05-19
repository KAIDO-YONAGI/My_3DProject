using UnityEngine;

public readonly struct SimpleCharacterMotorFrame
{
    public SimpleCharacterMotorFrame(
        bool isGrounded,
        bool jumpStarted,
        bool landedThisFrame,
        bool wantsSprint,
        Vector2 localDirection,
        float moveMagnitude01,
        float verticalSpeed)
    {
        IsGrounded = isGrounded;
        JumpStarted = jumpStarted;
        LandedThisFrame = landedThisFrame;
        WantsSprint = wantsSprint;
        LocalDirection = localDirection;
        MoveMagnitude01 = moveMagnitude01;
        VerticalSpeed = verticalSpeed;
    }

    public bool IsGrounded { get; }
    public bool JumpStarted { get; }
    public bool LandedThisFrame { get; }
    public bool WantsSprint { get; }
    public Vector2 LocalDirection { get; }
    public float MoveMagnitude01 { get; }
    public float VerticalSpeed { get; }
}
