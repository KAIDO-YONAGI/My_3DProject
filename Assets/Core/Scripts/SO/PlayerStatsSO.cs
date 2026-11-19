using Core.Scripts.Character.Stats;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>
    /// 玩家数值的初始值资产。运行时通过 CreateInitialData 导出一份拷贝放进
    /// PlayerStatsModel，Play 期间资产内容保持不变。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsSO", menuName = "Data/PlayerStatsSO", order = 0)]
    public class PlayerStatsSO : ScriptableObject
    {
        [SerializeField] private PlayerStatsData stats = new();

        /// <summary>资产中保存的初始值。运行时使用 CreateInitialData 导出的拷贝。</summary>
        public PlayerStatsData CreateInitialData() => stats.Clone();
    }
}
