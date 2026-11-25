using System;
using System.Collections;
using System.Collections.Generic;
using Core.Scripts.Character.Stats;
using Core.Scripts.Common;
using Core.Scripts.SO;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using SceneChanger = Core.Scripts.Scene.SceneChanger;

namespace Core.Scripts.Inventory
{
    /// <summary>
    /// 快捷栏的运行时状态管理器：槽位内容、堆叠、拾取、丢弃与使用都从这里进入。
    /// 界面通过 <see cref="InventoryChanged"/> 刷新显示，拾取请求经 <see cref="LootEventSO"/> 事件资产进入。
    /// </summary>
    /// <remarks>
    /// 槽位在常驻场景初始化时从 <see cref="InventoryConfigSO"/> 复制成运行态，之后只改本类的列表。
    /// 掉落物从 <see cref="lootPrefab"/> 实例化到当前玩法场景，随场景卸载一起销毁。
    /// </remarks>
    public class InventoryManager : YSingleton<InventoryManager>
    {
        [Header("数据配置")]
        [Tooltip("快捷栏的初始槽位；常驻场景初始化时复制成运行态。")]
        [SerializeField] private InventoryConfigSO inventoryConfig;

        [Header("掉落物")]
        [Tooltip("掉落物预制体；丢弃物品与放不下的物品都用它生成。")]
        [SerializeField] private Loot lootPrefab;

        [Tooltip("拾取请求事件；掉落物被拾取后经该事件进入快捷栏。")]
        [SerializeField] private LootEventSO lootEvent;

        [Tooltip("掉落物生成位置相对本地玩家根节点的高度偏移。")]
        [SerializeField] private float dropHeight = 0.5f;

        private readonly List<InventorySlotState> hotbar = new();

        // 非联网运行时的掉落原点缓存；联机时始终取本地玩家根节点。
        private Transform fallbackPlayerRoot;

        /// <summary>任意槽位内容变化后触发，界面按整表刷新。</summary>
        public event Action InventoryChanged;

        /// <summary>快捷栏槽位数量。</summary>
        public int HotbarCount => hotbar.Count;

        /// <summary>单例注册后把配置里的槽位复制成运行态。</summary>
        protected override void OnSingletonInitialized()
        {
            Debug.Assert(inventoryConfig != null,
                $"{nameof(inventoryConfig)} 未指定：快捷栏没有初始槽位。", this);
            Debug.Assert(lootPrefab != null,
                $"{nameof(lootPrefab)} 未指定：丢弃的物品无法生成到世界中。", this);
            Debug.Assert(lootEvent != null,
                $"{nameof(lootEvent)} 未指定：拾取请求不会进入快捷栏。", this);
            Debug.Assert(lootPrefab.PickupEvent == lootEvent,
                "掉落物预制体与物品栏引用的拾取事件不一致：两端要指向同一个事件资产。", this);

            hotbar.AddRange(inventoryConfig.HotbarSlots);
        }

        private void OnEnable()
        {
            lootEvent.LootEvent += OnLootPicked;
        }

        private void OnDisable()
        {
            lootEvent.LootEvent -= OnLootPicked;
        }

        /// <summary>读取指定槽位；下标越界时返回空槽。</summary>
        public InventorySlotState GetSlot(int index) =>
            index >= 0 && index < hotbar.Count ? hotbar[index] : default;

        /// <summary>
        /// 把物品放进快捷栏，优先补同类堆叠再占用空格。
        /// </summary>
        /// <returns>仍未放下的数量；调用方据此决定掉落物的去留。</returns>
        public int AddItem(ItemSO item, int quantity)
        {
            if (item == null || quantity <= 0) return quantity;

            int remaining = AddToSlots(hotbar, item, quantity);
            if (remaining != quantity)
                InventoryChanged?.Invoke();
            return remaining;
        }

        /// <summary>使用指定槽位的一个物品：先结算道具效果，再从槽位移除。</summary>
        public void UseSlot(int index)
        {
            InventorySlotState slot = GetSlot(index);
            if (slot.item == null || slot.quantity <= 0) return;

            ApplyItemEffects(slot.item);
            RemoveFromSlot(index, 1);
        }

        /// <summary>丢弃指定槽位的一个物品到本地玩家脚下。</summary>
        public void DropSlot(int index)
        {
            InventorySlotState slot = GetSlot(index);
            if (slot.item == null || slot.quantity <= 0) return;

            SpawnLoot(slot.item, 1);
            RemoveFromSlot(index, 1);
        }

        /// <summary>拾取事件处理：物品先入快捷栏，放不下的数量留在掉落物上。</summary>
        private void OnLootPicked(ItemSO item, int quantity, Loot loot)
        {
            if (item == null || loot == null || quantity <= 0) return;

            int remaining = AddItem(item, quantity);
            if (remaining <= 0)
                loot.Collect();
            else
                loot.SetQuantity(remaining);
        }

        /// <summary>把物品放进槽位列表，返回仍未放下的数量。</summary>
        private static int AddToSlots(List<InventorySlotState> slots, ItemSO item, int quantity)
        {
            int stackLimit = Mathf.Max(1, item.stackableSize);
            for (int index = 0; index < slots.Count && quantity > 0; index++)
            {
                InventorySlotState slot = slots[index];
                if (slot.item != null && slot.item != item) continue;

                int capacity = stackLimit - (slot.item == item ? slot.quantity : 0);
                int placed = Mathf.Min(capacity, quantity);
                if (placed <= 0) continue;

                slot.item = item;
                slot.quantity += placed;
                slots[index] = slot;
                quantity -= placed;
            }

            return quantity;
        }

        /// <summary>从指定槽位移除数量；返回实际移除的数量。</summary>
        private int RemoveFromSlot(int index, int quantity)
        {
            if (index < 0 || index >= hotbar.Count || quantity <= 0) return 0;

            InventorySlotState slot = hotbar[index];
            if (slot.item == null) return 0;

            int removed = Mathf.Min(slot.quantity, quantity);
            slot.quantity -= removed;
            if (slot.quantity <= 0)
                slot = new InventorySlotState(null, 0);
            hotbar[index] = slot;

            InventoryChanged?.Invoke();
            return removed;
        }

        /// <summary>在本地玩家脚下生成掉落物。</summary>
        private void SpawnLoot(ItemSO item, int quantity)
        {
            Transform origin = ResolveDropOrigin();
            Loot loot = Instantiate(lootPrefab,
                origin.position + Vector3.up * dropHeight, Quaternion.identity);

            // 掉落物随当前玩法场景卸载；场景框架未就绪时退回活动场景。
            UnityEngine.SceneManagement.Scene scene = SceneChanger.Instance != null
                ? SceneChanger.Instance.GetCurrentScene()
                : SceneManager.GetActiveScene();
            SceneManager.MoveGameObjectToScene(loot.gameObject, scene);
            loot.Initialize(item, quantity);
        }

        /// <summary>
        /// 掉落原点：联机取本地玩家根节点；非联网时按 Player 标签查找一次并缓存，
        /// 找不到玩家时退回管理器自身位置。
        /// </summary>
        private Transform ResolveDropOrigin()
        {
            if (NetworkClient.localPlayer != null)
                return NetworkClient.localPlayer.transform;

            if (fallbackPlayerRoot == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) fallbackPlayerRoot = player.transform;
            }

            return fallbackPlayerRoot != null ? fallbackPlayerRoot : transform;
        }

        /// <summary>把道具的使用效果结算到本地玩家数值上；持续时间为正时启动还原计时。</summary>
        private void ApplyItemEffects(ItemSO item)
        {
            StatsManager stats = StatsManager.RequiredInstance;
            if (item.maxHealth > 0) stats.UpdateMaxHealth(item.maxHealth);
            if (item.currentHealth > 0) stats.UpdateHealth(item.currentHealth);
            if (item.speed > 0) stats.UpdateSpeed(item.speed);
            if (item.damage > 0) stats.UpdateDamage(item.damage);
            if (item.duration > 0) StartCoroutine(RevertAfter(item, item.duration));
        }

        /// <summary>持续时间结束后还原道具带来的生命上限、移动速度与攻击伤害变化。</summary>
        private IEnumerator RevertAfter(ItemSO item, float duration)
        {
            yield return new WaitForSeconds(duration);

            // 允许暂缺：退出播放模式时数值服务可能已先销毁。
            if (StatsManager.Instance == null) yield break;

            StatsManager stats = StatsManager.RequiredInstance;
            if (item.maxHealth > 0)
            {
                stats.UpdateMaxHealth(-item.maxHealth);
                int overflow = stats.Stats.CurrentHealth - stats.Stats.MaxHealth;
                if (overflow > 0) stats.UpdateHealth(-overflow);
            }

            if (item.speed > 0) stats.UpdateSpeed(-item.speed);
            if (item.damage > 0) stats.UpdateDamage(-item.damage);
        }
    }
}
