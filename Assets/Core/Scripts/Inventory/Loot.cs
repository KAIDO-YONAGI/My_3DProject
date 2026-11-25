using System.Collections;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Inventory
{
    /// <summary>
    /// 世界中的掉落物：本地玩家进入触发器时把物品交给 <see cref="InventoryManager"/>。
    /// 背包放不下的剩余数量留在掉落物上，可再次拾取。
    /// </summary>
    /// <remarks>
    /// 预制体需要带 isTrigger 的 Collider 与 Rigidbody（isKinematic），才能与玩家根节点的
    /// CharacterController 产生触发事件。图标渲染器可留空，留空时保留预制体自带外观。
    /// </remarks>
    public class Loot : MonoBehaviour
    {
        /// <summary>掉落物生成后不可立即拾取的秒数，避免在玩家脚下生成后被立刻捡回。</summary>
        private const float PickupLockSeconds = 0.5f;

        [Tooltip("图标渲染器；留空表示使用预制体自带外观。")]
        [SerializeField] private SpriteRenderer iconRenderer;

        /// <summary>掉落物承载的道具。</summary>
        public ItemSO item;

        /// <summary>掉落物承载的剩余数量。</summary>
        public int quantity;

        private bool canBePick = true;
        private Coroutine unlockRoutine;

        private void Awake()
        {
            Collider pickupCollider = GetComponentInChildren<Collider>();
            Debug.Assert(pickupCollider != null && pickupCollider.isTrigger,
                "掉落物缺少触发器碰撞体，玩家无法拾取。", this);
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

        private void OnTriggerEnter(Collider other)
        {
            if (!canBePick || item == null || quantity <= 0) return;
            if (!IsLocalPlayer(other)) return;

            // 拾取期间保持锁定，背包放不下时等到离开触发器再允许重试。
            canBePick = false;
            int remaining = InventoryManager.RequiredInstance.AddItem(item, quantity);
            if (remaining <= 0)
            {
                Destroy(gameObject);
                return;
            }

            quantity = remaining;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other))
                canBePick = true;
        }

        /// <summary>把道具图标写到预制体外观上；没有配置图标渲染器时保留预制体原样。</summary>
        private void ApplyVisual()
        {
            if (item == null || iconRenderer == null) return;
            iconRenderer.sprite = item.icon;
            iconRenderer.enabled = item.icon != null;
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
