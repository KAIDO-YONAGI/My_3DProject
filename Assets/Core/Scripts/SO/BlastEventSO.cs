using System;
using Core.Scripts.Networking.Sync;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>一次本地攻击提交的爆炸来源与结算参数。</summary>
    public readonly struct BlastMessage
    {
        public GameObject Source { get; }
        public AttackRequestPack Request { get; }

        public BlastMessage(GameObject source, AttackRequestPack request)
        {
            Source = source;
            Request = request;
        }
    }

    /// <summary>在攻击入口与网络爆炸同步组件之间发布爆炸请求。</summary>
    [CreateAssetMenu(fileName = "BlastEventSO", menuName = "Events/BlastEventSO", order = 0)]
    public sealed class BlastEventSO : ScriptableObject
    {
        public event Action<BlastMessage> BlastEvent;

        public void OnEventRaised(BlastMessage message)
        {
            BlastEvent?.Invoke(message);
        }
    }
}
