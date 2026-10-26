using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Sync
{
    public struct AttackRequestPack
    {
        public Vector3 Position { get; }
        public int Damage { get; }
        public double Range { get; }//可以分成数个部分来分别查四周chunk是否命中

        public AttackRequestPack(Vector3 position, int damage, double radius=0.45)
        {
            this.Position = position;
            this.Damage = damage;
            this.Range = radius;
        }
    }

    public class AttackSync : NetworkBehaviour
    {
        [ClientRpc(includeOwner = false)]
        public void RpcRequestAttack(AttackRequestPack  request)
        {
            // bool broken =platform.BreakBlock(localPosition);
            
        }

        [Command]
        public void CommandAttack(AttackRequestPack request)
        {
            RpcRequestAttack(request);
        }
    }
}