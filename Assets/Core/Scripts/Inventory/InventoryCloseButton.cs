using UnityEngine;
using UnityEngine.UI;

namespace Core.Scripts.Inventory
{
    /// <summary>物品栏关闭按钮：点击后收起所属物品栏面板。</summary>
    public class InventoryCloseButton : MonoBehaviour
    {
        [Tooltip("触发关闭的按钮。")]
        [SerializeField] private Button closeButton;

        private InventoryPanel panel;

        private void Awake()
        {
            Debug.Assert(closeButton != null,
                $"{nameof(closeButton)} 未指定：点击不会关闭物品栏面板。", this);

            panel = GetComponentInParent<InventoryPanel>();
            Debug.Assert(panel != null,
                "关闭按钮不在物品栏面板层级内：点击不会关闭任何面板。", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(Close);
        }

        private void Close()
        {
            panel.Close();
        }
    }
}
