using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>
    /// 道具定义资产：名称、图标、堆叠上限与使用效果。
    /// 运行期资产只读，背包槽位保存对资产本身的引用。
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "Data/ItemSO")]
    public class ItemSO : ScriptableObject
    {
        [Header("基本信息")]
        [Tooltip("道具名称；掉落物生成时用作对象名。")]
        public string itemName;

        [Tooltip("道具说明文本。")]
        [TextArea]
        public string itemDescription;

        [Tooltip("界面图标；掉落物图标渲染器也使用本图。")]
        public Sprite icon;

        [Tooltip("单槽堆叠上限；为 1 表示不可堆叠。")]
        public int stackableSize = 1;

        [Header("使用效果")]
        [Tooltip("使用后增加的当前生命；0 表示无此效果。")]
        public int currentHealth;

        [Tooltip("使用后增加的生命上限；0 表示无此效果。")]
        public int maxHealth;

        [Tooltip("使用后增加的移动速度；0 表示无此效果。")]
        public float speed;

        [Tooltip("使用后增加的攻击伤害；0 表示无此效果。")]
        public int damage;

        [Tooltip("大于 0 时，本次使用带来的生命上限、移动速度与攻击伤害变化在持续时间结束后还原；小于等于 0 表示永久生效。")]
        public float duration;
    }
}
