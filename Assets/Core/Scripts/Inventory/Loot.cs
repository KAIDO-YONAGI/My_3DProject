using System.Collections;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Inventory
{
    /// <summary>
    /// 世界中的掉落物：本地玩家进入触发器时经 <see cref="LootEventSO"/> 发出拾取请求。
    /// 通用掉落物用 <see cref="sr"/> 显示道具图标；专用掉落物预制体可装配自己的外观并配置道具与数量。
    /// </summary>
    /// <remarks>
    /// 预制体需要带 isTrigger 的 Collider 与 Rigidbody（isKinematic），才能与玩家根节点的
    /// CharacterController 产生触发事件。
    /// </remarks>
    public class Loot : MonoBehaviour
    {
        /// <summary>掉落物生成后不可立即拾取的秒数，避免在玩家脚下生成后被立刻捡回。</summary>
        private const float PickupLockSeconds = 0.5f;

        /// <summary>掉落物承载的道具，可在预制体实例上单独配置。</summary>
        public ItemSO item;

        /// <summary>图标渲染器；通用掉落物在生成时把道具图标写到这里。</summary>
        public SpriteRenderer sr;

        /// <summary>拾取请求事件资产；掉落物被拾取时经该事件交给物品栏。</summary>
        public LootEventSO lootEvent;

        /// <summary>掉落物承载的剩余数量，可在预制体实例上单独配置。</summary>
        public int quantity = 1;

        private bool canBePick = true;
        private Coroutine unlockRoutine;

        /// <summary>本掉落物使用的拾取事件资产；管理器用它校验两端引用一致。</summary>
        public LootEventSO PickupEvent => lootEvent;

        private void Awake()
        {
            Collider pickupCollider = GetComponentInChildren<Collider>();
            Debug.Assert(pickupCollider != null && pickupCollider.isTrigger,
                "掉落物缺少触发器碰撞体，玩家无法拾取。", this);
            Debug.Assert(lootEvent != null,
                $"{nameof(lootEvent)} 未指定：拾取请求不会交给物品栏。", this);
            ApplyVisual();
        }

        private void OnValidate()
        {
            ApplyVisual();
        }

        /// <summary>用道具与数量初始化掉落物，并短暂禁止拾取。</summary>
        public void Initialize(ItemSO item, int quantity)
        {
            canBePick = false;
            this.item = item;
            this.quantity = quantity;
            gameObject.SetActive(true);
            ApplyVisual();
            if (!string.IsNullOrEmpty(item.itemName))
                gameObject.name = item.itemName;

            if (unlockRoutine != null) StopCoroutine(unlockRoutine);
            unlockRoutine = StartCoroutine(UnlockPickup());
        }

        /// <summary>拾取请求发出后更新剩余数量；由物品栏在放不下时调用。</summary>
        public void SetQuantity(int remaining)
        {
            quantity = Mathf.Max(0, remaining);
        }

        /// <summary>拾取完成后移除掉落物。</summary>
        public void Collect()
        {
            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!canBePick || item == null || quantity <= 0) return;
            if (!IsLocalPlayer(other)) return;

            // 拾取期间保持锁定，物品栏放不下时等到离开触发器再允许重试。
            canBePick = false;
            lootEvent.RaiseLootEvent(item, quantity, this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other))
                canBePick = true;
        }

        /// <summary>把道具图标写到图标渲染器上；没有配置图标渲染器的专用掉落物保留自带外观。</summary>
        private void ApplyVisual()
        {
            if (item == null || sr == null) return;
            sr.sprite = item.icon;
            sr.enabled = item.icon != null;
        }

        private IEnumerator UnlockPickup()
        {
            yield return new WaitForSeconds(PickupLockSeconds);
            canBePick = true;
            unlockRoutine = null;
        }

        /// <summary>
        /// 是否本机玩家：联机时只认本地拥有者，单机场景没有网络身份时按 Player 标签判断。
        /// </summary>
        private static bool IsLocalPlayer(Collider other)
        {
            NetworkIdentity identity = other.GetComponentInParent<NetworkIdentity>();
            return identity != null ? identity.isLocalPlayer : other.CompareTag("Player");
        }
    }
}
