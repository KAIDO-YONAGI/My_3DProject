using TMPro;
using UnityEngine;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 血量文本的显示层。持有控件引用并托管纯 C# 的 HealthController：
    /// Start 时创建并首刷，OnDestroy 时释放。
    /// </summary>
    public class HealthView : MonoBehaviour
    {
        [SerializeField] private TMP_Text healthText;

        private HealthController controller;

        private void Awake()
        {
            Debug.Assert(healthText != null,
                $"{nameof(healthText)} 未指定：血量文本不会更新。", this);
        }

        private void Start()
        {
            // Start 在所有 Awake 之后：此时 StatsManager 必然就绪，控制器可以立即订阅。
            controller = new HealthController(this);
            controller.Refresh();
        }

        private void OnDestroy()
        {
            controller?.Dispose();
            controller = null;
        }

        public void SetHp(int current, int max)
        {
            healthText.text = "HP:" + current + "/" + max;
        }
    }
}
