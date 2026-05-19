using System;
using UnityEngine;

[Serializable]
public sealed class SimpleCharacterMovementSettings
{
    [SerializeField] private float runSpeed = 6.8f;
    [SerializeField] private float sprintSpeed = 10.8f;
    [SerializeField] private float acceleration = 32f;
    [SerializeField] private float deceleration = 38f;
    [SerializeField] private float airControlMultiplier = 0.45f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float gravity = -28f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float groundedPull = -2f;
    [SerializeField] private bool orientToMovement;

    public float RunSpeed => runSpeed;
    public float SprintSpeed => sprintSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float AirControlMultiplier => airControlMultiplier;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => gravity <= 0f ? gravity : -gravity;
    public float GroundedPull => groundedPull;
    public bool OrientToMovement => orientToMovement;

    public float GetTargetSpeed(bool sprintHeld)
    {
        return sprintHeld ? sprintSpeed : runSpeed;
    }

    public float GetReferenceSpeed(bool sprintHeld)
    {
        float targetSpeed = GetTargetSpeed(sprintHeld);
        return targetSpeed > 0.01f ? targetSpeed : (runSpeed > 0.01f ? runSpeed : 1f);
    }

    public float GetJumpSpeed()
    {
        return Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * Mathf.Abs(Gravity) * 2f);
    }

    public bool ApplyRecommendedMinimums()
    {
        bool changed = false;
        changed |= EnsureMinimum(ref runSpeed, 6.8f);
        changed |= EnsureMinimum(ref sprintSpeed, 10.8f);
        changed |= EnsureMinimum(ref acceleration, 32f);
        changed |= EnsureMinimum(ref deceleration, 38f);
        changed |= EnsureMinimum(ref airControlMultiplier, 0.45f);
        changed |= EnsureMinimum(ref jumpHeight, 1.2f);

        if (rotationSpeed <= 0f)
        {
            rotationSpeed = 360f;
            changed = true;
        }

        if (gravity > -28f)
        {
            gravity = -28f;
            changed = true;
        }

        if (groundedPull > -2f)
        {
            groundedPull = -2f;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureMinimum(ref float target, float minimum)
    {
        if (target >= minimum)
        {
            return false;
        }

        target = minimum;
        return true;
    }
}
