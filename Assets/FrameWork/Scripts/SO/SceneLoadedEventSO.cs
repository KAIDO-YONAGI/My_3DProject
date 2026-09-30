using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneLoadedEventSO", menuName = "Events/SceneLoadedEventSO", order = 1)]
public class SceneLoadedEventSO : ScriptableObject
{
    public event Action<GameSceneSO> SceneLoadedEvent;

    public void RaiseSceneLoadedEvent(GameSceneSO currentScene)
    {
        SceneLoadedEvent?.Invoke(currentScene);
    }
}
