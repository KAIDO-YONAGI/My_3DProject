using System;
using Core.Scripts.Inventory;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>掉落物被拾取时的请求事件：携带道具、数量与掉落物实例。</summary>
    [CreateAssetMenu(fileName = "LootEvent", menuName = "Events/LootEvent", order = 0)]
    public class LootEventSO : ScriptableObject
    {
        /// <summary>拾取请求；订阅方负责把物品放进物品栏并处理掉落物去留。</summary>
        public event Action<ItemSO, int, Loot> LootEvent;

        /// <summary>广播一次拾取请求。</summary>
        public void RaiseLootEvent(ItemSO item, int quantity, Loot loot)
        {
            LootEvent?.Invoke(item, quantity, loot);
        }
    }
}
