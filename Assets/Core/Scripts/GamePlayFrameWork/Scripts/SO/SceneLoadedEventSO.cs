using System;
using UnityEngine;

namespace Core.My_FrameWork.Scripts.SO
{
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
