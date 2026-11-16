using System;
using UnityEngine;

namespace Core.Scripts.Character.Movement
{
    /// <summary>
    /// 集中保存角色运动参数。
    /// PlayerCharacterController 负责序列化配置，CharacterMotor 只读取这里暴露的安全值完成实际位移。
    /// </summary>
    [Serializable]
    public class CharacterMovementSettings
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

        // 非跳跃离地的容忍时长（秒）：下坡与台阶造成的短暂悬空在此窗口内仍按贴地处理，避免误播跳跃动画。
        [SerializeField, Min(0f)] private float airborneTolerance = 0.3f;

        /// <summary>
        /// 有移动输入时水平速度的上升速率，单位米每二次方秒。只影响物理位移，不决定动画切换。
        /// </summary>
        public float Acceleration => acceleration;

        /// <summary>
        /// 无移动输入时水平速度的衰减速率，单位米每二次方秒。只影响物理位移，不决定动画切换。
        /// </summary>
        public float Deceleration => deceleration;

        /// <summary>
        /// 空中改变水平速度的力度比例，取值 0 到 1。只作用于加减速，重力照常计算。
        /// </summary>
        public float AirControlMultiplier => airControlMultiplier;

        /// <summary>
        /// 启用朝向后每秒允许转动的角度，单位度每秒。
        /// </summary>
        public float RotationSpeed => rotationSpeed;

        /// <summary>
        /// 重力加速度，单位米每二次方秒。填入正数时自动取反以保证向下；起跳速度由该值与跳跃高度反推。
        /// </summary>
        public float Gravity => gravity <= 0f ? gravity : -gravity;

        /// <summary>
        /// 贴地时施加的向下速度，单位米每秒。填入正数时自动取反；用于压住贴地状态，避免负速度逐帧累积。
        /// </summary>
        public float GroundedPull => groundedPull <= 0f ? groundedPull : -groundedPull;

        /// <summary>
        /// 是否让角色以 <see cref="RotationSpeed"/> 的角速度转向移动方向。关闭时朝向由外部逻辑决定。
        /// </summary>
        public bool OrientToMovement => orientToMovement;

        /// <summary>
        /// 非跳跃离地的容许时长，单位秒。下坡与台阶造成的短暂悬空在窗口内仍按贴地处理，避免误播跳跃动画。
        /// </summary>
        public float AirborneTolerance => airborneTolerance;

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
}
