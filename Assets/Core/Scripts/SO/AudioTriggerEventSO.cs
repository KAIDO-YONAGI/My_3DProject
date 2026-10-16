using System;
using Core.Scripts.GamePlayFrameWork.Scripts.Audio;
using UnityEngine;

namespace Core.Scripts.SO
{
// 命名规范：VoidEventSO 类名不携带语义，同语义复用时资产名必须含语义前缀，
// 如 SlashActionFinishedEventSO、SceneLoadedVoidEventSO；禁止再创建无语义的资产名。

    [CreateAssetMenu(fileName = "AudioTriggerEventSO", menuName = "Events/AudioTriggerEventSO", order = 0)]
    public class AudioTriggerEventSO : ScriptableObject
    {
        public event Action<AudioTriggerMessage> AudioTriggerEvent;

        public void OnEventRaised(AudioTriggerMessage audioTriggerMessage)
        {
            AudioTriggerEvent?.Invoke(audioTriggerMessage);
        }
    }
}