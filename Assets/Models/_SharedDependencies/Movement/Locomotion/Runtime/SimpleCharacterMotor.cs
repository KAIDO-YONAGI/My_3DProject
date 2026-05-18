using UnityEngine;

public sealed class SimpleCharacterMotor
{
    private readonly Transform owner;
    private readonly CharacterController characterController;
    private readonly SimpleCharacterMovementSettings settings;

    private Vector3 planarVelocity;
    private float verticalVelocity;

    public SimpleCharacterMotor(Transform owner, CharacterController characterController, SimpleCharacterMovementSettings settings)
    {
        this.owner = owner;
        this.characterController = characterController;
        this.settings = settings;
        verticalVelocity = settings.GroundedPull;
    }

    public SimpleCharacterMotorFrame Tick(CharacterInputFrame input, Transform inputSpace, float deltaTime)
    {
        bool hasMoveInput = input.HasMoveInput;
        Vector3 desiredMoveDirection = ResolveMoveDirection(input.MoveInput, inputSpace);
        if (settings.OrientToMovement && hasMoveInput && desiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            RotateTowards(desiredMoveDirection, deltaTime);
        }

        bool groundedBeforeMove = characterController.isGrounded;
        if (groundedBeforeMove && verticalVelocity < 0f)
        {
            verticalVelocity = settings.GroundedPull;
        }

        bool jumpStarted = groundedBeforeMove && input.JumpPressed;
        if (jumpStarted)
        {
            verticalVelocity = settings.GetJumpSpeed();
        }

        float targetSpeed = hasMoveInput ? settings.GetTargetSpeed(input.SprintHeld) : 0f;
        Vector3 targetPlanarVelocity = hasMoveInput ? desiredMoveDirection * targetSpeed : Vector3.zero;
        float velocityStep = hasMoveInput ? settings.Acceleration : settings.Deceleration;
        if (!groundedBeforeMove)
        {
            velocityStep *= settings.AirControlMultiplier;
        }

        planarVelocity = Vector3.MoveTowards(planarVelocity, targetPlanarVelocity, velocityStep * deltaTime);
        verticalVelocity += settings.Gravity * deltaTime;

        Vector3 frameMotion = (planarVelocity + Vector3.up * verticalVelocity) * deltaTime;
        CollisionFlags collisionFlags = characterController.Move(frameMotion);

        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        bool groundedAfterMove = (collisionFlags & CollisionFlags.Below) != 0;
        bool landedThisFrame = !groundedBeforeMove && groundedAfterMove && verticalVelocity <= 0f;
        if (groundedAfterMove && verticalVelocity < 0f)
        {
            verticalVelocity = settings.GroundedPull;
        }

        Vector3 localVelocity = owner.InverseTransformDirection(planarVelocity);
        Vector2 localPlanarVelocity = new Vector2(localVelocity.x, localVelocity.z);
        Vector2 localDirection = localPlanarVelocity.sqrMagnitude > 0.0001f ? localPlanarVelocity.normalized : Vector2.zero;

        float moveMagnitude01 = Mathf.Clamp01(planarVelocity.magnitude / settings.GetReferenceSpeed(input.SprintHeld));
        return new SimpleCharacterMotorFrame(
            groundedAfterMove,
            jumpStarted,
            landedThisFrame,
            input.SprintHeld && hasMoveInput,
            localDirection,
            moveMagnitude01,
            verticalVelocity);
    }

    private Vector3 ResolveMoveDirection(Vector2 moveInput, Transform inputSpace)
    {
        Transform basis = inputSpace;
        if (basis == null && Camera.main != null && !Camera.main.transform.IsChildOf(owner))
        {
            basis = Camera.main.transform;
        }

        Vector3 forward;
        Vector3 right;
        if (basis != null)
        {
            forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
        }
        else
        {
            forward = owner.forward;
            right = owner.right;
        }

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void RotateTowards(Vector3 desiredMoveDirection, float deltaTime)
    {
        Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, Vector3.up);
        owner.rotation = Quaternion.RotateTowards(owner.rotation, targetRotation, settings.RotationSpeed * deltaTime);
    }
}
