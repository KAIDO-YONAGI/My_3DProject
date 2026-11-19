using System;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>
    /// 本地玩家受击广播：伤害源只上报伤害值，
    /// 扣血、死亡与复活编排统一收口在 PlayerDamageController。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerDamagedEvent", menuName = "Events/PlayerDamagedEvent", order = 0)]
    public class PlayerDamagedEventSO : ScriptableObject
    {
        public event Action<int> PlayerDamaged;

        /// <summary>发布一次受击广播。</summary>
        /// <param name="damage">受到的伤害，按正数传入。</param>
        public void OnPlayerDamaged(int damage)
        {
            PlayerDamaged?.Invoke(damage);
        }
    }
}
