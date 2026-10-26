namespace Core.Scripts.Common
{
    /// <summary>标识场景配置资产所代表的场景用途。</summary>
    public enum SceneType
    {
        Location,
        Menu,
        Retry,
        // Default 没有功能性索引，建议在此之前添加新的枚举。
        Default
    }

    /// <summary>标识 UI 管理器可切换和排序的画布类型。</summary>
    public enum CanvasToToggle //显示优先级系统和画布唤起系统共用
    {
        ESC,
        GameOver,
        Dialog,
        Ending,
        Menu,
        Dying,
        Guide,
        // Default 没有索引，建议在此之前添加新的枚举。
        // 此外，不应使用 Default 的任何索引，因为它在语义上代表默认界面，目前也是 ESC 的重要判断依据。
        Default
    }
}
