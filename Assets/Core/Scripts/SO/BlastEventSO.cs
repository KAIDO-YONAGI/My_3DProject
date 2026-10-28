using System;
using Core.Scripts.Common;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>网络攻击请求与本地爆炸表现共用的数据载荷。</summary>
    public struct AttackRequestPack
    {
        public AttackType AttackType;
        public Vector3 PlatformLocalPosition;
        public int Damage;
        public float Radius;
        public int PlatformId;

        /// <summary>创建包含攻击类别、平台局部位置、伤害、半径和平台 ID 的请求。</summary>
        public AttackRequestPack(AttackType attackType, Vector3 platformLocalPosition,
            int damage, float radius, int platformId)
        {
            AttackType = attackType;
            PlatformLocalPosition = platformLocalPosition;
            Damage = damage;
            Radius = radius;
            PlatformId = platformId;
        }
    }

    /// <summary>通知表现订阅者某种攻击已在本地完成结算。</summary>
    [CreateAssetMenu(fileName = "BlastEventSO", menuName = "Events/BlastEventSO", order = 0)]
    public class BlastEventSO : ScriptableObject
    {
        public event Action<AttackRequestPack> BlastEvent;

        /// <summary>发布爆炸表现通知，供粒子、音效等订阅者响应。</summary>
        public void OnEventRaised(AttackRequestPack request)
        {
            BlastEvent?.Invoke(request);
        }
    }
}
