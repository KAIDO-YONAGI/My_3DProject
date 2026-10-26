using System;
using UnityEngine;

namespace Core.Scripts.SO
{
    /// <summary>广播场景组加载完成及当前场景配置。</summary>
    [CreateAssetMenu(fileName = "SceneLoadedEventSO", menuName = "Events/SceneLoadedEventSO", order = 1)]
    public class SceneLoadedEventSO : ScriptableObject
    {
        public event Action<GameSceneSO> SceneLoadedEvent;

        public void RaiseSceneLoadedEvent(GameSceneSO currentScene)
        {
            SceneLoadedEvent?.Invoke(currentScene);
        }
    }
}
