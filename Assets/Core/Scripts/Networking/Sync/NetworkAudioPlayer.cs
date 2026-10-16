using Mirror;
using Core.Scripts.GamePlayFrameWork.Scripts.Audio;
using Core.Scripts.SO;
using UnityEngine;
using AudioType = Core.Scripts.GamePlayFrameWork.Scripts.Audio.AudioType;

namespace Core.Scripts.Networking.Sync
{
    public class NetworkAudioPlayer : NetworkBehaviour
    {
        [SerializeField] private AudioTriggerEventSO audioTriggerEvent;

        private void OnEnable()
        {
            if (audioTriggerEvent != null)
            {
                // 每个网络玩家实例都订阅同一个事件资产，收到事件后再通过下面的守卫判断自己是否应该处理。
                audioTriggerEvent.AudioTriggerEvent += OnAudioTrigger;
            }
        }

        private void OnDisable()
        {
            if (audioTriggerEvent != null)
            {
                // 组件禁用或销毁时解除订阅，避免失效对象继续响应音频事件。
                audioTriggerEvent.AudioTriggerEvent -= OnAudioTrigger;
            }
        }

        private void OnAudioTrigger(AudioTriggerMessage audioTriggerMessage)
        {
            // 事件会被所有 NetworkAudioPlayer 收到，只有产生事件的那个网络玩家对象可以继续处理。
            if (audioTriggerMessage.Source != gameObject)
            {
                return;
            }

            // 只有本地拥有者能向服务器发送该对象的 Command，远程客户端不能代发。
            if (!isLocalPlayer)
            {
                return;
            }

            // 网络对象尚未进入 ready 状态时，发送 Command 没有有效的网络连接上下文。
            if (!NetworkClient.ready)
            {
                return;
            }

            // 本地玩家已经在 AudioTriggerManager 中播放一次，这里只把事件提交给服务器。
            CommandPlayAudio(audioTriggerMessage.AudioType, audioTriggerMessage.WorldPosition);
        }

        [Command]
        private void CommandPlayAudio(AudioType audioType, Vector3 worldPosition)
        {
            // Command 在服务器执行，由服务器统一转发给正在观察该网络对象的客户端。
            RpcPlayAudio(audioType, worldPosition);
        }

        [ClientRpc(includeOwner = false)]//owner不播放
        private void RpcPlayAudio(AudioType audioType, Vector3 worldPosition)
        {
            // includeOwner=false 排除发起 Command 的拥有者，避免本地即时播放和 RPC 播放重复。
            AudioPlayerManager.Instance?.PlaySfx(audioType, worldPosition);
        }
    }
}
