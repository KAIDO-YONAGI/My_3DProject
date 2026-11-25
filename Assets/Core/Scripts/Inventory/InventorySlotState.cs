using Core.Scripts.SO;

namespace Core.Scripts.Inventory
{
    /// <summary>一个槽位的状态：物品与数量。</summary>
    [System.Serializable]
    public struct InventorySlotState
    {
        public ItemSO item;
        public int quantity;

        public InventorySlotState(ItemSO item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }
    }
}
