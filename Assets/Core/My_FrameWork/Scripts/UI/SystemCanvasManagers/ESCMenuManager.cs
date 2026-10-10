using Core.My_FrameWork.Scripts.Core;
using Core.My_FrameWork.Scripts.Scene;
using Core.My_FrameWork.Scripts.SO;
using UnityEngine;

namespace Core.My_FrameWork.Scripts.UI.SystemCanvasManagers
{
    public class ESCMenuManager : MonoBehaviour, ICanvasManager
    {
        [SerializeField] private CanvasGroup ESCGroup;
        [SerializeField] private ToggleCanvasEventSO toggleESCEvent;
        [SerializeField] private SceneLoadedEventSO sceneLoadedEvent;

        public ToggleCanvasEventSO ToggleCanvasEvent => toggleESCEvent;
        public SceneLoadedEventSO SceneLoadedEvent => sceneLoadedEvent;

        private GameSceneSO _currentScene;

        private void OnEnable()
        {
            toggleESCEvent.toggleCanvasEvent += OnESC;
            sceneLoadedEvent.SceneLoadedEvent += OnSceneLoaded;
        }

        private void OnDisable()
        {
            toggleESCEvent.toggleCanvasEvent -= OnESC;
            sceneLoadedEvent.SceneLoadedEvent -= OnSceneLoaded;
        }

        private void OnSceneLoaded(GameSceneSO sceneLoaded)
        {
            _currentScene = sceneLoaded;
            OnESC(false);
        }

        private void OnESC(bool state)
        {
            if (_currentScene == null || _currentScene.sceneType == SceneType.Menu) return;
            if (state)
            {
                TimeManager.Instance.PauseGame();
            }
            else
            {
                TimeManager.Instance.ResumeGame();
            }

            ((ICanvasManager)this).SetCanvaState(ESCGroup, CanvasToToggle.ESC, state);
        }
    }
}