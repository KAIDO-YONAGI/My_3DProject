using System;
using UnityEngine;

/// <summary>
/// 集中保存角色运动参数。
/// PlayerCharacterController 负责序列化配置，CharacterMotor 只读取这里暴露的安全值完成实际位移。
/// </summary>
[Serializable]
public sealed class CharacterMovementSettings
{
    // 地面移动速度；冲刺键只负责在两档速度之间选择。
    [SerializeField, Min(0f)] private float runSpeed = 6.8f;
    [SerializeField, Min(0f)] private float sprintSpeed = 10.8f;

    // 加速和减速只影响物理位移，动画切换另由输入意图直接控制。
    [SerializeField, Min(0f)] private float acceleration = 32f;
    [SerializeField, Min(0f)] private float deceleration = 38f;

    // 空中仍允许调整方向，但控制力度按此比例衰减。
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.45f;

    // 开启朝向移动后，角色以每秒角度限制转向速度。
    [SerializeField, Min(0f)] private float rotationSpeed = 360f;

    // 重力与贴地拉力在属性中统一修正为负值，避免 Inspector 误填正数。
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

    /// <summary>
    /// 根据当前是否冲刺返回本帧目标水平速度。
    /// </summary>
    public float GetTargetSpeed(bool sprint)
    {
        return sprint ? sprintSpeed : runSpeed;
    }

    /// <summary>
    /// 根据目标跳跃高度和重力反推起跳瞬间需要的竖直速度。
    /// </summary>
    public float GetJumpSpeed()
    {
        return Mathf.Sqrt(2f * Mathf.Abs(Gravity) * jumpHeight);
    }
}
