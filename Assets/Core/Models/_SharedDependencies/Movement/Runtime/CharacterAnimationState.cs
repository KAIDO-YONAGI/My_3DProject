/// <summary>
/// 网络层使用的动画阶段，不直接驱动本地 Animator。
/// ClientProtocol.PlayerContext 保存这个稳定枚举，避免网络数据依赖 Animator 中可变的状态名称。
/// </summary>
public enum CharacterAnimationState
{
    // 角色位于地面，可表现待机、跑步或冲刺。
    Grounded,

    // 角色刚离地，供网络快照区分起跳瞬间。
    JumpStart,

    // 角色已经进入持续上升或下落阶段。
    JumpLoop,
}
