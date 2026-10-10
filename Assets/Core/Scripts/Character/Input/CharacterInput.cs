using UnityEngine;

/// <summary>
/// 单帧输入快照。
/// PlayerCharacterController 从 CharacterInputReader（Input System）采集数据后创建它，CharacterMotor 不再直接依赖输入 API。
/// </summary>
public readonly struct CharacterInput
{
    /// <summary>
    /// 保存这一帧的移动轴、冲刺保持状态和跳跃按下事件。
    /// </summary>
    public CharacterInput(Vector2 moveInput, bool sprintHeld, bool jumpPressed)
    {
        MoveInput = moveInput;
        SprintHeld = sprintHeld;
        JumpPressed = jumpPressed;
    }

    // MoveInput 已在采集处限制到单位圆内，可直接作为方向和动画权重使用。
    public Vector2 MoveInput { get; }
    public bool SprintHeld { get; }

    // JumpPressed 只在按键按下的单帧为 true，防止按住空格连续起跳。
    public bool JumpPressed { get; }

    // 使用极小死区过滤浮点噪声，同时保持键盘按下和松开的即时响应。
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;
}
