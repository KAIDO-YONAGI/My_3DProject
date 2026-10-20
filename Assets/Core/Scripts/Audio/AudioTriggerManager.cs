using Core.Scripts.Character.Input;
using Core.Scripts.Networking.CharacterPresentation;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Audio
{
    public struct FeedbackMessage
    {
        public readonly AudioType AudioType;
        public readonly GameObject Source;
        public readonly Vector3 WorldPosition;

        public FeedbackMessage(AudioType audioType, GameObject source, Vector3 worldPosition)
        {
            AudioType = audioType;
            Source = source;
            WorldPosition = worldPosition;
        }
    }
    public class AudioTriggerManager : MonoBehaviour
    {

        [SerializeField] private FeedbackEventSO feedbackEvent;
        private readonly CharacterInputReader input = new();
        
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

            Vector3 worldPosition = source.transform.position;
            // 本地先播放，保证操作反馈不需要等待服务器往返。
            AudioPlayerManager.Instance?.PlaySfx(AudioType.Hurt, worldPosition);
            // 再发布带有声源和世界坐标的反馈，由对应的 NetworkAudioPlayer 负责同步给其他客户端。
            feedbackEvent?.OnEventRaised(new FeedbackMessage(AudioType.Hurt, source, worldPosition));
        }

        private GameObject ResolvePlayer()
        {
            if (NetworkClient.isConnected)
            {
                // 联机模式使用当前连接的本地玩家对象，不能使用场景中预先配置的单机角色引用。
                return NetworkClient.localPlayer != null
                    ? NetworkClient.localPlayer.gameObject
                    : null;
            }

            // 单机模式读取管理器当前角色，角色替换后仍能拿到最新实例。
            return NetworkCharacterManager.Instance != null
                ? NetworkCharacterManager.Instance.StandalonePlayer
                : null;
        }
    }
}
