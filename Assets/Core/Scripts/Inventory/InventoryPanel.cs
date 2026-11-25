using Core.Scripts.SO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Scripts.Inventory
{
    /// <summary>
    /// 物品栏面板的显隐控制：开关 CanvasGroup，响应开关事件与 Tab 键。
    /// </summary>
    /// <remarks>
    /// 打开时释放光标以便点击槽位，与死亡界面的光标处理一致；关闭后不主动重新锁定，
    /// 由镜头输入重新捕获光标。
    /// </remarks>
    public class InventoryPanel : MonoBehaviour
    {
        /// <summary>键盘切换面板使用的按键。</summary>
        private const Key ToggleKey = Key.Tab;

        [Tooltip("控制面板显隐、交互和射线阻挡的画布组。")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("开关请求事件；留空表示只能由代码或 Tab 键开关。")]
        [SerializeField] private ToggleCanvasEventSO toggleEvent;

        [Tooltip("是否响应 Tab 键切换面板。")]
        [SerializeField] private bool toggleWithKeyboard = true;

        /// <summary>面板当前是否打开。</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Debug.Assert(canvasGroup != null,
                $"{nameof(canvasGroup)} 未指定：物品栏面板无法显隐。", this);
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            if (toggleEvent != null)
                toggleEvent.toggleCanvasEvent += ApplyVisible;
        }

        private void OnDisable()
        {
            if (toggleEvent != null)
                toggleEvent.toggleCanvasEvent -= ApplyVisible;
        }

        private void Update()
        {
            if (!toggleWithKeyboard || Keyboard.current == null) return;
            if (Keyboard.current[ToggleKey].wasPressedThisFrame)
                Toggle();
        }

        /// <summary>打开面板。</summary>
        public void Open() => ApplyVisible(true);

        /// <summary>关闭面板。</summary>
        public void Close() => ApplyVisible(false);

        /// <summary>切换面板打开与关闭。</summary>
        public void Toggle() => ApplyVisible(!IsOpen);

        private void ApplyVisible(bool visible)
        {
            IsOpen = visible;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            // 打开时释放光标以便点击槽位；关闭后不主动重新锁定，由镜头输入重新捕获光标。
            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
