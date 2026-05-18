using UnityEngine;

public readonly struct CharacterInputFrame
{
    public CharacterInputFrame(Vector2 moveInput, bool sprintHeld, bool jumpPressed)
    {
        MoveInput = moveInput;
        SprintHeld = sprintHeld;
        JumpPressed = jumpPressed;
    }

    public Vector2 MoveInput { get; }
    public bool SprintHeld { get; }
    public bool JumpPressed { get; }
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;
}
