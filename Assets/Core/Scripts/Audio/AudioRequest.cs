using UnityEngine;

namespace Core.Scripts.Audio
{
    /// <summary>携带音效类型和可选世界坐标的统一播放请求。</summary>
    public readonly struct AudioRequest
    {
        public AudioType AudioType { get; }
        public Vector3? WorldPosition { get; }

        /// <summary>创建一次音效请求；省略位置时按二维音效播放。</summary>
        public AudioRequest(AudioType audioType, Vector3? worldPosition = null)
        {
            AudioType = audioType;
            WorldPosition = worldPosition;
        }
    }
}
