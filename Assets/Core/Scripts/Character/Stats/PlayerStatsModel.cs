using System;
using UnityEngine;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 玩家数值的运行时状态与规则。
    /// 初始值由 StatsManager 从 PlayerStatsSO 资产拷入，运行时只由本类的写方法修改。
    /// </summary>
    public class PlayerStatsModel : IPlayerStatsReadOnly
    {
        /// <summary>生命值或上限变化时触发。HealthController 订阅后推给 HealthView。</summary>
        public event Action HealthChanged;
        /// <summary>速度、伤害等属性变化时触发。</summary>
        public event Action StatsChanged;

        // 运行时数值本体；构造时整体替换为拷贝，之后只由本类的写方法修改。
        private readonly PlayerStatsData data;

        /// <summary>用初始数值建立模型；构造时拷贝，之后与传入对象互不影响。</summary>
        /// <param name="initial">初始数值模板。</param>
        public PlayerStatsModel(PlayerStatsData initial)
        {
            data = initial.Clone();
        }

        /// <summary>生命值上限。</summary>
        public int MaxHealth => data.maxHealth;
        /// <summary>当前生命值。</summary>
        public int CurrentHealth => data.currentHealth;
        /// <summary>攻击伤害数值。</summary>
        public int Damage => data.damage;
        /// <summary>移动速度，单位米每秒。</summary>
        public float Speed => data.speed;

        /// <summary>调整生命上限，结果最低为 1；当前生命不随上限变化自动回满。</summary>
        /// <param name="amount">上限增量，可为负。</param>
        public void UpdateMaxHealth(int amount)
        {
            data.maxHealth += amount;
            if (data.maxHealth < 1) data.maxHealth = 1;
            HealthChanged?.Invoke();
        }

        /// <summary>调整当前生命，结果钳制在 0 与生命上限之间。</summary>
        /// <param name="amount">生命增量，负数表示受伤。</param>
        public void UpdateHealth(int amount)
        {
            data.currentHealth = Mathf.Clamp(data.currentHealth + amount, 0, data.maxHealth);
            HealthChanged?.Invoke();
        }

        /// <summary>把当前生命设为指定值，结果钳制在 0 与生命上限之间。</summary>
        /// <param name="value">目标生命值。</param>
        public void SetCurrentHealth(int value)
        {
            data.currentHealth = Mathf.Clamp(value, 0, data.maxHealth);
            HealthChanged?.Invoke();
        }

        /// <summary>调整攻击伤害。</summary>
        /// <param name="amount">伤害增量，可为负。</param>
        public void UpdateDamage(int amount)
        {
            data.damage += amount;
            StatsChanged?.Invoke();
        }

        /// <summary>调整移动速度。</summary>
        /// <param name="amount">速度增量，可为负。</param>
        public void UpdateSpeed(float amount)
        {
            data.speed += amount;
            StatsChanged?.Invoke();
        }
    }
}
