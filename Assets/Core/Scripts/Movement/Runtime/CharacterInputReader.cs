using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 相机与角色控制器共用的输入入口。
/// 它把 Input System 的 PlayerControls 包装成只读访问器，并在这里集中维护“旧输入轴 -> Input System”的量纲换算，
/// 使 ThirdPersonCamera 的 sensitivity、scrollSpeed 等既有数值保持改造前的手感。
/// 输入实例按需创建：专用服务器和远程角色走不到采集路径，因此不会创建 PlayerControls。
/// </summary>
public sealed class CharacterInputReader : System.IDisposable
{
    // 旧 Mouse X/Y 轴的 sensitivity 是 0.1，而 Input System 的 <Mouse>/delta 是原始像素增量，
    // 乘上该系数后 sensitivity 字段的含义才与改造前一致。
    private const float LookScale = 0.1f;

    // Windows 上滚一格 <Mouse>/scroll/y 为 120，旧 Mouse ScrollWheel 轴每格约为 0.1。
    // 该常量按 Windows 标定；换到每格为 1 的平台（如 macOS）需要重新标定。
    private const float ZoomScale = 0.1f / 120f;

    private PlayerControls controls;

    // 第一次读取输入时才创建并启用动作表，避免非本地玩家产生输入资源。
    private PlayerControls Controls => controls ??= CreateEnabledControls();

    /// <summary>
    /// 读取移动输入，并限制到单位圆内防止斜向超速。
    /// </summary>
    public Vector2 ReadMove()
    {
        // 旧代码使用 GetAxisRaw，这里同样不做输入平滑，只做长度截断。
        return Vector2.ClampMagnitude(Controls.Player.Move.ReadValue<Vector2>(), 1f);
    }

    /// <summary>
    /// 是否存在移动输入，供相机判断是否让角色转向镜头的水平朝向。
    /// </summary>
    public bool HasMoveInput()
    {
        Vector2 move = Controls.Player.Move.ReadValue<Vector2>();
        return move.sqrMagnitude > 0.0001f;
    }

    /// <summary>疾跑键是否处于按住状态。</summary>
    public bool SprintHeld => Controls.Player.Sprint.IsPressed();

    /// <summary>本帧是否按下跳跃键。</summary>
    public bool JumpPressed => Controls.Player.Jump.WasPressedThisFrame();

    /// <summary>本帧鼠标位移，已换算成与旧 Mouse X/Y 轴一致的量纲。</summary>
    public Vector2 ReadLook()
    {
        return Controls.Player.Look.ReadValue<Vector2>() * LookScale;
    }

    /// <summary>本帧滚轮增量，已换算成与旧 Mouse ScrollWheel 轴一致的量纲。</summary>
    public float ReadZoom()
    {
        return Controls.Player.Zoom.ReadValue<float>() * ZoomScale;
    }

    /// <summary>自由光标键是否按住；按住期间相机把鼠标交还给系统。</summary>
    public bool FreeCursorHeld => Controls.Player.FreeCursor.IsPressed();

    /// <summary>本帧是否按下释放光标键。</summary>
    public bool ReleaseCursorPressed => Controls.Player.ReleaseCursor.WasPressedThisFrame();

    /// <summary>本帧是否按下重新捕获光标键。</summary>
    public bool LockCursorPressed => Controls.Player.LockCursor.WasPressedThisFrame();

    /// <summary>
    /// 释放输入资源。必须调用：生成的包装类在终结时会断言动作表仍处于启用状态。
    /// </summary>
    public void Dispose()
    {
        if (controls == null)
        {
            return;
        }

        controls.Disable();
        controls.Dispose();
        controls = null;
    }

    private static PlayerControls CreateEnabledControls()
    {
        PlayerControls created = new PlayerControls();
        created.Enable();
        return created;
    }
}
