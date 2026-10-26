using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.Common;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Sync
{
    /// <summary>封装攻击爆炸的位置、伤害值和水平半径，供网络命令传递。</summary>
    public struct AttackRequestPack
    {
        public AttackType AttackType;
        public Vector3 Position;
        public int Damage;
        public float Radius; //可以分成数个部分来分别查四周chunk是否命中

        /// <summary>创建网络传输使用的攻击类型、位置、伤害和半径数据。</summary>
        public AttackRequestPack(AttackType attackType, Vector3 position, int damage = 1, float radius = 0.45f)
        {
            AttackType = attackType;
            this.Position = position;
            this.Damage = damage;
            this.Radius = radius;
        }
    }

    /// <summary>接收本地玩家的攻击请求，并向其他观察客户端广播爆炸结算。</summary>
    public class NetworkBlastManager : NetworkBehaviour
    {
        [SerializeField] private BlastEventSO blastEvent;

        private void Awake()
        {
            if (blastEvent == null)
            {
                Debug.LogError($"[{nameof(NetworkBlastManager)}] blastEvent 未指定：远端爆炸表现事件不会发布。", this);
            }
        }

        /// <summary>远端客户端结算攻击，并发布攻击表现事件供特效和音效订阅。</summary>
        [ClientRpc(includeOwner = false)]
        private void RpcRequestAttack(AttackRequestPack request)
        {
            VoxelPlatformDestruction.ExplodeAt(request.Position, request.Radius);
            blastEvent.OnEventRaised(new BlastMessage(request.AttackType, request.Position, request.Radius));
        }

        /// <summary>把本地攻击请求提交到服务器，再转发给该玩家的观察客户端。</summary>
        [Command]
        public void CommandAttack(AttackRequestPack request)
        {
            RpcRequestAttack(request);
        }
    }
}
