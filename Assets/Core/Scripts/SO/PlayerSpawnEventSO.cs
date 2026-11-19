using System;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>玩家出生位置广播：场景加载完成后由 SceneChanger 广播，玩家侧自行应用位置。</summary>
    [CreateAssetMenu(fileName = "PlayerSpawnEvent", menuName = "Events/PlayerSpawnEvent", order = 0)]
    public class PlayerSpawnEventSO : ScriptableObject
    {
        public event Action<Vector3> PlayerSpawn;

        /// <summary>发布一次出生位置。</summary>
        public void OnPlayerSpawn(Vector3 position)
        {
            PlayerSpawn?.Invoke(position);
        }
    }
}