using System;
using UnityEngine;

[Serializable]
public sealed class CharacterMovementSettings
{
    [SerializeField, Min(0f)] private float runSpeed = 6.8f;
    [SerializeField, Min(0f)] private float sprintSpeed = 10.8f;
    [SerializeField, Min(0f)] private float acceleration = 32f;
    [SerializeField, Min(0f)] private float deceleration = 38f;
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.45f;
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;
    [SerializeField] private float gravity = -28f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float groundedPull = -2f;
    [SerializeField] private bool orientToMovement;

    public float Acceleration => acceleration;
    public float Deceleration => deceleration;
    public float AirControlMultiplier => airControlMultiplier;
    public float RotationSpeed => rotationSpeed;
    public float Gravity => gravity <= 0f ? gravity : -gravity;
    public float GroundedPull => groundedPull <= 0f ? groundedPull : -groundedPull;
    public bool OrientToMovement => orientToMovement;

    public float GetTargetSpeed(bool sprint)
    {
        return sprint ? sprintSpeed : runSpeed;
    }

    public float GetReferenceSpeed(bool sprint)
    {
        return Mathf.Max(0.01f, GetTargetSpeed(sprint));
    }

    public float GetJumpSpeed()
    {
        return Mathf.Sqrt(2f * Mathf.Abs(Gravity) * jumpHeight);
    }
}
