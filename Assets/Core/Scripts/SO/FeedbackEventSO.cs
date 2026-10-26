using System;
using Core.Scripts.Audio;
using UnityEngine;

namespace Core.Scripts.SO
{
// 命名规范：VoidEventSO 类名不携带语义，同语义复用时资产名必须含语义前缀，
// 如 SlashActionFinishedEventSO、SceneLoadedVoidEventSO；禁止再创建无语义的资产名。

    /// <summary>向音频管理器发布统一音效播放请求。</summary>
    [CreateAssetMenu(fileName = "FeedbackEventSO", menuName = "Events/FeedbackEventSO", order = 0)]
    public class FeedbackEventSO : ScriptableObject
    {
        public event Action<AudioRequest> AudioRequested;

        /// <summary>通知订阅的音频管理器处理一次播放请求。</summary>
        public void RequestAudio(AudioRequest request)
        {
            AudioRequested?.Invoke(request);
        }
    }
}
