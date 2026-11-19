using Core.Scripts.Common;
using Core.Scripts.SO;
using UnityEngine;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 本地玩家的数值服务。持有运行时模型，初始值由 PlayerStatsSO 资产提供。
    /// 每个客户端只维护本机玩家的一份数值；界面通过 Stats 订阅变化，修改走本类的写入口。
    /// </summary>
    public class StatsManager : YSingleton<StatsManager>
    {
        [SerializeField] private PlayerStatsSO statsConfig;

        // 运行时模型由本类持有，外部只能通过只读入口访问。
        private PlayerStatsModel model;

        /// <summary>运行时数值的只读入口；单例初始化完成后可用。</summary>
        public IPlayerStatsReadOnly Stats => model;

        /// <summary>单例注册后按资产初始值建立运行时模型。</summary>
        protected override void OnSingletonInitialized()
        {
            Debug.Assert(statsConfig != null,
                $"{nameof(statsConfig)} 未指定：玩家数值没有初始值。", this);
            model = new PlayerStatsModel(statsConfig.CreateInitialData());
        }

        /// <summary>死亡后复活：把当前生命恢复到上限。仍存活时保持原值。</summary>
        public void Respawn()
        {
            if (model.CurrentHealth <= 0)
                model.SetCurrentHealth(model.MaxHealth);
        }

        /// <summary>调整生命上限。</summary>
        public void UpdateMaxHealth(int amount) => model.UpdateMaxHealth(amount);

        /// <summary>调整当前生命。</summary>
        public void UpdateHealth(int amount) => model.UpdateHealth(amount);

        /// <summary>调整攻击伤害。</summary>
        public void UpdateDamage(int amount) => model.UpdateDamage(amount);

        /// <summary>调整移动速度。</summary>
        public void UpdateSpeed(float amount) => model.UpdateSpeed(amount);
    }
}
