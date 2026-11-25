using Core.Scripts.SO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Scripts.Inventory
{
    /// <summary>
    /// 单个物品槽位的显示与点击入口，快捷栏和背包共用本组件。
    /// 槽位内容由 <see cref="InventoryCanvasView"/> 通过 <see cref="RestoreState"/> 写入，
    /// 左键使用，右键丢弃。
    /// </summary>
    public class InventorySlot : MonoBehaviour, IPointerClickHandler
    {
        [Header("控件")]
        [Tooltip("物品图标。")]
        [SerializeField] private Image itemImage;

        [Tooltip("数量文本。")]
        [SerializeField] private TMP_Text quantityText;

        private ItemSO item;
        private int quantity;
        private int slotIndex = -1;
        private bool isBackpackSlot;

        private void Awake()
        {
            Debug.Assert(itemImage != null,
                $"{nameof(itemImage)} 未指定：物品图标无法显示。", this);
            Debug.Assert(quantityText != null,
                $"{nameof(quantityText)} 未指定：物品数量无法显示。", this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (slotIndex < 0 || quantity <= 0) return;

            InventoryManager manager = InventoryManager.RequiredInstance;
            if (eventData.button == PointerEventData.InputButton.Left)
                manager.UseSlot(isBackpackSlot, slotIndex);
            else if (eventData.button == PointerEventData.InputButton.Right)
                manager.DropSlot(isBackpackSlot, slotIndex);
        }

        /// <summary>配置槽位归属与下标；由物品栏视图在初始化时调用。</summary>
        public void ConfigureSlot(bool backpack, int index)
        {
            isBackpackSlot = backpack;
            slotIndex = index;
        }

        /// <summary>用管理器中的槽位状态覆盖显示；数量为 0 时清空槽位。</summary>
        public void RestoreState(ItemSO slotItem, int amount)
        {
            item = amount > 0 ? slotItem : null;
            quantity = item != null ? amount : 0;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (quantity <= 0)
                item = null;

            bool hasItem = item != null;
            itemImage.gameObject.SetActive(hasItem);
            quantityText.text = hasItem ? quantity.ToString() : string.Empty;
            if (hasItem)
                itemImage.sprite = item.icon;
        }
    }
}
