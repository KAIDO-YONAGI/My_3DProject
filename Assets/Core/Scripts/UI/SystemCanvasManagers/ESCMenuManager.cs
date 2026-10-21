using Core.Scripts.Common;
using Core.Scripts.Scene;
using Core.Scripts.SO;
using UnityEngine;

namespace Core.Scripts.UI.SystemCanvasManagers
{
    public class ESCMenuManager : MonoBehaviour, ICanvasManager
    {
        [SerializeField] private CanvasGroup ESCGroup;
        [SerializeField] private ToggleCanvasEventSO toggleESCEvent;
        [SerializeField] private SceneLoadedEventSO sceneLoadedEvent;

        public ToggleCanvasEventSO ToggleCanvasEvent => toggleESCEvent;
        public SceneLoadedEventSO SceneLoadedEvent => sceneLoadedEvent;

        private GameSceneSO _currentScene;

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名报错，订阅与开关流程直接解引用。
        private void ValidateReferences()
        {
            if (ESCGroup == null)
            {
                Debug.LogError($"[{nameof(ESCMenuManager)}] ESCGroup 未指定：画布显隐无法生效。", this);
            }

            if (toggleESCEvent == null)
            {
                Debug.LogError($"[{nameof(ESCMenuManager)}] toggleESCEvent 未指定：收不到开关指令。", this);
            }

            if (sceneLoadedEvent == null)
            {
                Debug.LogError($"[{nameof(ESCMenuManager)}] sceneLoadedEvent 未指定：切场景时不会自动关闭。", this);
            }
        }

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