using UnityEngine;

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
        if (_currentScene == null || _currentScene.sceneType == MyEnums.SceneType.Menu) return;
        if (state)
        {
            TimeManager.Instance.PauseGame();
        }
        else
        {
            TimeManager.Instance.ResumeGame();
        }

        ((ICanvasManager)this).SetCanvaState(ESCGroup, MyEnums.CanvasToToggle.ESC, state);
    }
}