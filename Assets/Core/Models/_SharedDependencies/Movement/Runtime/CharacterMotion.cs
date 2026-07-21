using UnityEngine;

public readonly struct CharacterMotion
{
    public CharacterMotion(
        bool isGrounded,
        bool jumpStarted,
        bool wantsSprint,
        Vector2 localDirection,
        float moveMagnitude01)
    {
        IsGrounded = isGrounded;
        JumpStarted = jumpStarted;
        WantsSprint = wantsSprint;
        LocalDirection = localDirection;
        MoveMagnitude01 = moveMagnitude01;
    }

    public bool IsGrounded { get; }
    public bool JumpStarted { get; }
    public bool WantsSprint { get; }
    public Vector2 LocalDirection { get; }
    public float MoveMagnitude01 { get; }
}
