using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Sync
{
    /// <summary>封装攻击爆炸的位置、伤害值和水平半径，供网络命令传递。</summary>
    public struct AttackRequestPack
    {
        public Vector3 Position;
        public int Damage;
        public float Radius; //可以分成数个部分来分别查四周chunk是否命中

        public AttackRequestPack(Vector3 position, int damage=1, float radius = 0.45f)
        {
            this.Position = position;
            this.Damage = damage;
            this.Radius = radius;
        }
    }

    /// <summary>接收客户端攻击请求，并向其他客户端广播爆炸结算。</summary>
    public class NetworkBlastManager : NetworkBehaviour
    {
        [SerializeField] private BlastEventSO blastEvent;

        private void Awake()
        {
            if (blastEvent == null)
            {
                Debug.LogError($"[{nameof(NetworkBlastManager)}] blastEvent 未指定：爆炸不会同步给其他客户端。", this);
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            // Mirror 只对本地拥有者调用此回调，远程玩家实例不会订阅全局爆炸事件。
            blastEvent.BlastEvent += OnBlast;
        }

        private void OnDisable()
        {
            blastEvent.BlastEvent -= OnBlast;
        }

        private void OnBlast(BlastMessage message)
        {
            // 只接受由当前玩家对象发起的事件，避免其他本地来源代发网络攻击。
            if (message.Source != gameObject || !NetworkClient.ready)
                return;

            CommandAttack(message.Request);
        }

        [ClientRpc(includeOwner = false)]
        public void RpcRequestAttack(AttackRequestPack request)
        {
            VoxelPlatformDestruction.ExplodeAt(request.Position, request.Radius);
        }

        [Command]
        public void CommandAttack(AttackRequestPack request)
        {
            RpcRequestAttack(request);
        }
    }
}
