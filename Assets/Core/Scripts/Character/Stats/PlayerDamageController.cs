using Core.Scripts.Scene;
using Core.Scripts.SO;
using Core.Scripts.UI;
using UnityEngine;

namespace Core.Scripts.Character.Stats
{
    /// <summary>
    /// 本地玩家的受击与死亡编排。订阅伤害广播扣血，生命归零时进入死亡状态；
    /// 重试请求先回满血，再由 SceneChanger 重载当前场景组并恢复游戏时间。
    /// 伤害源只上报数值，不知道玩家数值系统的实现。
    /// </summary>
    public class PlayerDamageController : MonoBehaviour
    {
        [SerializeField] private PlayerDamagedEventSO playerDamagedEvent;
        [SerializeField] private VoidEventSO retryEventSO;

        [Tooltip("死亡时显示的 GameOver 画布组。留空时死亡只暂停游戏，不显示界面。")] [SerializeField]
        private GameOverCanvas gameOverCanvas;

        // 死亡只处理一次：归零后继续落下的伤害不再结算，重试复活时清除。
        private bool dead;

        private void Awake()
        {
            Debug.Assert(playerDamagedEvent != null,
                $"{nameof(playerDamagedEvent)} 未指定：受击广播不会扣血。", this);
            Debug.Assert(retryEventSO != null,
                $"{nameof(retryEventSO)} 未指定：重试后不会复活回血。", this);
        }

        private void OnEnable()
        {
            playerDamagedEvent.PlayerDamaged += OnDamaged;
            retryEventSO.VoidEvent += OnRetryRequest;
        }

        private void OnDisable()
        {
            playerDamagedEvent.PlayerDamaged -= OnDamaged;
            retryEventSO.VoidEvent -= OnRetryRequest;
        }

        /// <summary>扣除生命，生命归零时进入死亡状态。</summary>
        /// <param name="damage">受到的伤害，按正数传入。</param>
        private void OnDamaged(int damage)
        {
            if (dead) return;

            StatsManager stats = StatsManager.RequiredInstance;
            stats.UpdateHealth(-damage);
            if (stats.Stats.CurrentHealth > 0) return;

            dead = true;
            EnterDeathState();
        }

        /// <summary>
        /// 死亡表现：配置了 GameOver 画布组时由画布组暂停游戏，未配置时直接暂停游戏。
        /// 重试请求触发场景重载，加载完成后 SceneChanger 恢复游戏时间。
        /// </summary>
        private void EnterDeathState()
        {
            if (gameOverCanvas != null)
            {
                gameOverCanvas.Show();
                return;
            }

            TimeManager.RequiredInstance.PauseGame();
        }

        /// <summary>重试：回满血，随后由 SceneChanger 继续重载当前场景组。</summary>
        private void OnRetryRequest()
        {
            StatsManager.RequiredInstance.Respawn();
            dead = false;
        }
    }
}
