using Core.Scripts.Character.Input;
using Core.Scripts.Networking.CharacterPresentation;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Audio
{
    /// <summary>读取本地测试输入并通过音频事件请求播放受击音效。</summary>
    public class AudioTriggerManager : MonoBehaviour
    {

        [SerializeField] private FeedbackEventSO feedbackEvent;
        private readonly CharacterInputReader input = new();

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名断言，运行时调用点直接解引用。
        private void ValidateReferences()
        {
            Debug.Assert(feedbackEvent != null,
                $"{nameof(feedbackEvent)} 未指定：按键反馈不会播放也不会同步。", this);
        }

        private void OnDisable()
        {
            // 输入对象持有 Unity Input System 资源，组件停用时释放，避免重复启用后产生多份输入状态。
            input.Dispose();
        }

        private void Update()
        {
            if (!input.TestPressed)
            {
                return;
            }

            GameObject source = ResolvePlayer();
            if (source == null)
            {
                return;
            }

            // 由 AudioPlayerManager 统一负责本地播放和网络转发。
            feedbackEvent.RequestAudio(new AudioRequest(AudioType.Hurt, source.transform.position));
        }

        private GameObject ResolvePlayer()
        {
            if (NetworkClient.isConnected)
            {
                // 联机模式使用当前连接的本地玩家对象，不能使用场景中预先配置的单机角色引用。
                // 玩家尚未生成时为空属于合法连接状态，调用方跳过本次触发即可。
                NetworkIdentity localPlayer = NetworkClient.localPlayer;
                return localPlayer != null ? localPlayer.gameObject : null;
            }

            // 单机模式的角色由常驻场景的管理器维护，管理器缺失按必需依赖直接失败。
            return NetworkCharacterManager.RequiredInstance.StandalonePlayer;
        }
    }
}
