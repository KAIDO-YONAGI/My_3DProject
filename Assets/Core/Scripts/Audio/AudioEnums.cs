namespace Core.Scripts.Audio
{
    /// <summary>音效类型：与 AudioConfigSO 的 SFXEntry 一一对应</summary>
    public enum AudioType
    {
        None,
        Walk,
        IceBroken,
        IceFreeze,
        Hurt,
        SpikeLaunch,
        LoadScene,
        Dead,
        SpringBounce,
        SpeedUp,
        Land,
        Jump,
        PickUp,
        PressurePlate,
        Run
    }

    /// <summary>背景音乐类型：与 AudioConfigSO 的 BGMEntry 一一对应</summary>
    public enum BGMType
    {
        Menu,
        Gaming,
        // Default 没有功能性索引，建议在此之前添加新的枚举。
        Default
    }
}
