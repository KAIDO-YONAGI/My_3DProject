namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 血量显示的控制器，纯 C# 类，由 HealthView 创建并托管。
    /// 订阅 StatsManager 的 HealthChanged，把当前血量推给视图；数值规则在模型，视图只负责显示。
    /// </summary>
    public class HealthController
    {
        private readonly HealthView view;

        /// <summary>订阅生命值变化；由视图在 Start 时调用。</summary>
        /// <param name="view">血量显示视图，控制器把最新数值推给它。</param>
        public HealthController(HealthView view)
        {
            this.view = view;
            StatsManager.RequiredInstance.Stats.HealthChanged += Refresh;
        }

        /// <summary>退订生命值变化；单例已销毁时跳过。</summary>
        public void Dispose()
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.Stats.HealthChanged -= Refresh;
        }

        /// <summary>把当前生命值推给视图。</summary>
        public void Refresh()
        {
            IPlayerStatsReadOnly stats = StatsManager.RequiredInstance.Stats;
            view.SetHp(stats.CurrentHealth, stats.MaxHealth);
        }
    }
}
