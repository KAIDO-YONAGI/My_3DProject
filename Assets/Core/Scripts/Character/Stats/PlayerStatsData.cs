using System;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 玩家数值的传输格式。资产初始值、运行时模型与快照之间都以拷贝传递，
    /// 每个持有者拿到独立对象，修改互不影响。
    /// </summary>
    [Serializable]
    public class PlayerStatsData
    {
        /// <summary>生命值上限。</summary>
        public int maxHealth;
        /// <summary>当前生命值。</summary>
        public int currentHealth;
        /// <summary>攻击伤害数值。</summary>
        public int damage;
        /// <summary>移动速度，单位米每秒。</summary>
        public float speed;

        /// <summary>字段级拷贝；字段全是值类型，浅拷贝即可。</summary>
        public PlayerStatsData Clone() => (PlayerStatsData)MemberwiseClone();
    }
}
