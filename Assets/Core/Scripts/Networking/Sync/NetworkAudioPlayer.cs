using Mirror;
using Core.Scripts.Audio;
using Core.Scripts.SO;
using UnityEngine;
using AudioType = Core.Scripts.Audio.AudioType;

namespace Core.Scripts.Networking.Sync
{
    public class NetworkAudioPlayer : NetworkBehaviour
    {
        [SerializeField] private FeedbackEventSO feedbackEvent;
        private bool isSubscribed;

        private void OnEnable()
        {
            SubscribeToFeedback();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // 首次生成网络玩家时，本地身份在 OnEnable 之后才确定，因此在这里完成订阅。
            SubscribeToFeedback();
        }

        public override void OnStopLocalPlayer()
        {
            UnsubscribeFromFeedback();
            base.OnStopLocalPlayer();
        }

        private void OnDisable()
        {
            UnsubscribeFromFeedback();
        }

        private void SubscribeToFeedback()
        {
            if (isSubscribed || !isLocalPlayer || feedbackEvent == null)
                return;
            // 只有本地玩家订阅反馈，远程玩家对象不会收到本地反馈通知。
            // 这里订阅的价值在于唤醒将来会有的多个反馈系统（单本地玩家）
            feedbackEvent.FeedbackEvent += OnFeedback;
            isSubscribed = true;
        }

        private void UnsubscribeFromFeedback()
        {
            if (!isSubscribed) return;
            // 组件停用、销毁或本地身份结束时解除订阅，避免失效对象继续响应反馈。
            feedbackEvent.FeedbackEvent -= OnFeedback;
            isSubscribed = false;
        }

        private void OnFeedback(FeedbackMessage feedbackMessage)
        {
            // 反馈来源必须是当前本地玩家对象，避免其他来源误发网络反馈。
            if (feedbackMessage.Source != gameObject)
            {
                return;
            }

            // 网络对象尚未进入 ready 状态时，发送 Command 没有有效的网络连接上下文。
            if (!NetworkClient.ready)
            {
                return;
            }

            // 本地玩家已经在 AudioTriggerManager 中播放一次，这里只把反馈提交给服务器。
            CommandPlayAudio(feedbackMessage.AudioType, feedbackMessage.WorldPosition);
        }

        [Command]
        private void CommandPlayAudio(AudioType audioType, Vector3 worldPosition)
        {
            // Command 在服务器执行，由服务器统一转发给正在观察该网络对象的客户端。
            RpcPlayAudio(audioType, worldPosition);
        }

        [ClientRpc(includeOwner = false)] //owner不播放
        private void RpcPlayAudio(AudioType audioType, Vector3 worldPosition)
        {
            // includeOwner=false 排除发起 Command 的拥有者，避免本地即时播放和 RPC 播放重复。
            AudioPlayerManager.Instance?.PlaySfx(audioType, worldPosition);
        }
    }
}