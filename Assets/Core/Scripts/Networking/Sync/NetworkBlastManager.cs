using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Sync
{
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
            AttackResult result = VoxelPlatformDestruction.Instance.ExplodeAt(
                request.PlatformLocalPosition, request.Radius, request.PlatformId);
            if (!result.ExplosionCenter.HasValue) return;
            blastEvent.OnEventRaised(request);
        }

        /// <summary>把本地攻击请求提交到服务器，再转发给该玩家的观察客户端。</summary>
        [Command]
        public void CommandAttack(AttackRequestPack request)
        {
            RpcRequestAttack(request);
        }
    }
}
