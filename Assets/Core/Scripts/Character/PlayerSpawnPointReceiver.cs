using Core.Scripts.SO;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Character
{
    /// <summary>
    /// 玩家侧的出生位置接收器：订阅 SceneChanger 广播的出生位置并把自己传送到该点。
    /// 网络玩家只处理本机拥有者，远端玩家继续由网络同步驱动；视觉实例与模块子物体不处理。
    /// </summary>
    public class PlayerSpawnPointReceiver : MonoBehaviour
    {
        [SerializeField] private PlayerSpawnEventSO playerSpawnEvent;

        private CharacterController movementCollider;

        private void Awake()
        {
            Debug.Assert(playerSpawnEvent != null,
                $"{nameof(playerSpawnEvent)} 未指定：加载完成后不会响应出生位置。", this);
            movementCollider = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            playerSpawnEvent.PlayerSpawn += OnPlayerSpawn;
        }

        private void OnDisable()
        {
            playerSpawnEvent.PlayerSpawn -= OnPlayerSpawn;
        }

        // 传送前停用碰撞体，避免 CharacterController 保留旧位置；结束后恢复原来的启用状态。
        private void OnPlayerSpawn(Vector3 position)
        {
            NetworkIdentity identity = GetComponentInParent<NetworkIdentity>(true);
            if (identity != null)
            {
                // 视觉实例与模块子物体跟随玩家根，不响应出生位置。
                if (identity.gameObject != gameObject) return;
                // 远端玩家的位置由网络同步驱动。
                if (!identity.isLocalPlayer) return;
            }

            bool colliderWasEnabled = movementCollider != null && movementCollider.enabled;
            if (movementCollider != null) movementCollider.enabled = false;
            transform.position = position;
            if (movementCollider != null) movementCollider.enabled = colliderWasEnabled;
        }
    }
}