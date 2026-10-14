using Core.Scripts.Networking;
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

        private void OnEnable()
        {
            targetButton.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            targetButton.onClick.RemoveListener(OnClick);
        }

        // 点击时转发编号；管理器缺失说明常驻场景未加载，请求无法落地。
        private void OnClick()
        {
            NetworkCharacterManager manager = NetworkCharacterManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning(
                    $"[{nameof(CharacterSelectButton)}] 缺少 {nameof(NetworkCharacterManager)}，角色 {characterId} 的切换请求已忽略。",
                    this);
                return;
            }

            manager.RequestCharacter(characterId);
        }
    }
}
