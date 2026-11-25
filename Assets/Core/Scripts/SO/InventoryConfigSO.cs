using System.Collections.Generic;
using Core.Scripts.Inventory;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>
    /// 快捷栏的初始槽位配置。InventoryManager 在常驻场景初始化时读取一次并复制成运行态，
    /// 资产内容在运行期保持不变。
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfigSO", menuName = "Data/InventoryConfigSO")]
    public class InventoryConfigSO : ScriptableObject
    {
        [Tooltip("快捷栏每个槽位的初始物品与数量，长度即快捷栏槽位数。")]
        [SerializeField] private InventorySlotState[] hotbarSlots = new InventorySlotState[0];

        /// <summary>快捷栏初始槽位。</summary>
        public IReadOnlyList<InventorySlotState> HotbarSlots => hotbarSlots;
    }
}
