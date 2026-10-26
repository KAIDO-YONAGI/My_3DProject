using System;
using Core.Scripts.Common;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>一次已经结算的爆炸表现通知。</summary>
    public readonly struct BlastMessage
    {
        public AttackType AttackType { get; }
        public Vector3 Position { get; }
        public float Radius { get; }

        /// <summary>创建包含攻击类型、世界位置和水平半径的爆炸通知。</summary>
        public BlastMessage(AttackType attackType, Vector3 position, float radius)
        {
            AttackType = attackType;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>通知表现订阅者某种攻击已在本地完成结算。</summary>
    [CreateAssetMenu(fileName = "BlastEventSO", menuName = "Events/BlastEventSO", order = 0)]
    public sealed class BlastEventSO : ScriptableObject
    {
        public event Action<BlastMessage> BlastEvent;

        /// <summary>发布爆炸表现通知，供粒子、音效等订阅者响应。</summary>
        public void OnEventRaised(BlastMessage message)
        {
            BlastEvent?.Invoke(message);
        }
    }
}
