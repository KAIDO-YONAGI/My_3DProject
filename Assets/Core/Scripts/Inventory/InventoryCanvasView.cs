using UnityEngine;

namespace Core.Scripts.Inventory
{
    /// <summary>快捷栏显示层：把 <see cref="InventoryManager"/> 的槽位状态刷新到控件上。</summary>
    public class InventoryCanvasView : MonoBehaviour
    {
        [Header("槽位")]
        [Tooltip("显示用槽位，顺序即管理器中的槽位顺序。")]
        [SerializeField] private InventorySlot[] slots;

        private bool subscribed;

        private void Awake()
        {
            Debug.Assert(slots != null,
                $"{nameof(slots)} 未指定：快捷栏没有可显示的槽位。", this);

            for (int index = 0; index < slots.Length; index++)
            {
                Debug.Assert(slots[index] != null,
                    $"{nameof(slots)}[{index}] 为空：该槽位不会刷新显示。", this);
            }
        }

        private void Start()
        {
            InventoryManager manager = InventoryManager.RequiredInstance;
            for (int index = 0; index < slots.Length; index++)
                slots[index].ConfigureSlot(index);

            Debug.Assert(manager.HotbarCount == slots.Length,
                $"快捷栏槽位数量不一致：管理器 {manager.HotbarCount} 个，控件 {slots.Length} 个。", this);

            subscribed = true;
            manager.InventoryChanged += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (subscribed) Refresh();
        }

        private void OnDestroy()
        {
            // 允许暂缺：单例可能先于视图销毁，销毁阶段不再退订。
            if (subscribed && InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= Refresh;
        }

        /// <summary>按管理器当前槽位状态刷新全部控件；管理器槽位少于控件时多余槽位清空。</summary>
        private void Refresh()
        {
            InventoryManager manager = InventoryManager.Instance;
            // 允许暂缺：移除或销毁阶段不再刷新显示。
            if (manager == null) return;

            for (int index = 0; index < slots.Length; index++)
            {
                InventorySlotState slot = manager.GetSlot(index);
                slots[index].RestoreState(slot.item, slot.quantity);
            }
        }
    }
}
