using Mirror;
using Core.Scripts.Audio;
using UnityEngine;
using AudioType = Core.Scripts.Audio.AudioType;

namespace Core.Scripts.Networking.Sync
{
    /// <summary>将本地音频管理器提交的请求转发给服务器和其他观察客户端。</summary>
    public class NetworkAudioPlayer : NetworkBehaviour
    {
        /// <summary>把音效请求发送给观察该本地玩家网络对象的其他客户端。</summary>
        public void SendAudioRequest(AudioRequest request)
        {
            // 只允许本地拥有者在客户端 Ready 后通过该对象发送 Command。
            if (!isLocalPlayer || !NetworkClient.ready)
            {
                return;
            }

            // 将可空坐标拆成 Mirror 可直接序列化的标记和 Vector3。
            bool hasWorldPosition = request.WorldPosition.HasValue;
            Vector3 worldPosition = request.WorldPosition.GetValueOrDefault();
            CmdPlayAudio(request.AudioType, hasWorldPosition, worldPosition);
        }

        [Command]
        private void CmdPlayAudio(AudioType audioType, bool hasWorldPosition, Vector3 worldPosition)
        {
            // 服务器把请求广播给观察者，同时保留原始 2D 或 3D 播放方式。
            RpcPlayAudio(audioType, hasWorldPosition, worldPosition);
        }

        [ClientRpc(includeOwner = false)]
        private void RpcPlayAudio(AudioType audioType, bool hasWorldPosition, Vector3 worldPosition)
        {
            // 拥有者由 includeOwner=false 排除；远端位置按请求的 2D/3D 类型还原。
            Vector3? playbackPosition = null;
            if (hasWorldPosition)
            {
                playbackPosition = worldPosition;
            }
            AudioPlayerManager.Instance.PlaySfx(audioType, playbackPosition);
        }
    }
}
