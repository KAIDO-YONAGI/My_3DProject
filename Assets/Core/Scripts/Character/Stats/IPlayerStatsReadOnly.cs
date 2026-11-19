using System;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 玩家数值的只读视图。界面与外部系统通过它读取状态并订阅变化，
    /// 运行时数值的修改只经过 StatsManager 的写入口。
    /// </summary>
    public interface IPlayerStatsReadOnly
    {
        /// <summary>生命值或生命上限变化时触发。</summary>
        event Action HealthChanged;
        /// <summary>速度、伤害等属性变化时触发，界面据此重绘属性。</summary>
        event Action StatsChanged;

        /// <summary>生命值上限。</summary>
        int MaxHealth { get; }
        /// <summary>当前生命值。</summary>
        int CurrentHealth { get; }
        /// <summary>攻击伤害数值。</summary>
        int Damage { get; }
        /// <summary>移动速度，单位米每秒。</summary>
        float Speed { get; }
    }
}
