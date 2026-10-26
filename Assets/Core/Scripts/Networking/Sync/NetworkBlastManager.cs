using Core.Scripts.Scene.VoxelPlatform;
using Core.Scripts.Scene.VoxelPlatform.Destruction;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Sync
{
    public struct AttackRequestPack
    {
        public Vector3 Position { get; }
        public int Damage { get; }
        public double Radius { get; } //可以分成数个部分来分别查四周chunk是否命中

        public AttackRequestPack(Vector3 position, int damage=1, double radius = 0.45)
        {
            this.Position = position;
            this.Damage = damage;
            this.Radius = radius;
        }
    }

    public class NetworkBlastManager : NetworkBehaviour
    {
        public static NetworkBlastManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        [ClientRpc(includeOwner = false)]
        public void RpcRequestAttack(AttackRequestPack request)
        {
            // VoxelPlatformDestruction.
        }

        [Command]
        public void CommandAttack(AttackRequestPack request)
        {
            RpcRequestAttack(request);
        }
    }
}