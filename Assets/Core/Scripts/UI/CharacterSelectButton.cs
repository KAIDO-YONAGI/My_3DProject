using Core.Scripts.Networking.CharacterPresentation;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Scripts.UI
{
    /// <summary>
    /// 角色选择按钮：把 UI 点击转发给角色切换的统一入口。
    /// 按钮不区分联机与离线，编号校验、网络提交和离线替换由 NetworkCharacterManager 按当前表现模式决定。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSelectButton : MonoBehaviour
    {
        [Tooltip("被监听的按钮。留空时自动取用同一 GameObject 上的 Button。")] [SerializeField]
        private Button targetButton;

        [Tooltip("该按钮代表的角色编号，对应 NetworkCharacterManager.characters 数组下标。")]
        [SerializeField, Min(0)]
        private int characterId;

        private void Awake()
        {
            // 留空时取同一 GameObject 上的 Button，与字段 Tooltip 的约定保持一致。
            if (targetButton == null)
            {
                targetButton = GetComponent<Button>();
            }

            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名断言，订阅点直接解引用。
        private void ValidateReferences()
        {
            Debug.Assert(targetButton != null,
                $"{nameof(targetButton)} 未指定：点击不会转发角色切换。", this);
        }

        private void OnEnable()
        {
            targetButton.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            targetButton.onClick.RemoveListener(OnClick);
        }

        // 点击时转发编号；管理器属于必需依赖，缺失时由 RequiredInstance 明确报告。
        private void OnClick()
        {
            NetworkCharacterManager.RequiredInstance.RequestCharacter(characterId);
        }
    }
}
