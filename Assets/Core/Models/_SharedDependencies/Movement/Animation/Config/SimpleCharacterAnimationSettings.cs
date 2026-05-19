using System;
using UnityEngine;

[Serializable]
public sealed class SimpleCharacterAnimationSettings
{
    [SerializeField, Range(0.05f, 0.95f)] private float jumpStartExitNormalizedTime = 0.55f;
    [SerializeField] private float stateFadeDuration = 0.1f;
    [SerializeField] private float landLockTime = 0.18f;
    [SerializeField] private float airVerticalBlendRange = 1.5f;
    [SerializeField, Range(0.6f, 1f)] private float fullJumpGroundedExitNormalizedTime = 0.92f;

    public float JumpStartExitNormalizedTime => jumpStartExitNormalizedTime;
    public float StateFadeDuration => Mathf.Max(0.01f, stateFadeDuration);
    public float LandLockTime => Mathf.Max(0f, landLockTime);
    public float AirVerticalBlendRange => Mathf.Max(0.1f, airVerticalBlendRange);
    public float FullJumpGroundedExitNormalizedTime => Mathf.Clamp01(fullJumpGroundedExitNormalizedTime);
}
