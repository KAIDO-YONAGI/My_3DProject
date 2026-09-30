using UnityEngine;

/// <summary>
/// GameOver 画布管理器，结构和 ESCMenuManager 一致。
/// 通过 ToggleCanvasEventSO(GameOver) 接收开关指令，
/// 由 UIManager 的画布调度系统统一触发，不依赖外部直接引用。
/// 声明：不可 ESC 关闭 + 打开时阻塞全局面板输入（玩家必须走重试流程）。
/// </summary>
public class GameOverCanvasManager : MonoBehaviour, ICanvasManager
{
    [SerializeField] private CanvasGroup gameOverGroup;
    [SerializeField] private ToggleCanvasEventSO toggleGameOverEvent;
    [SerializeField] private SceneLoadedEventSO sceneLoadedEvent;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleGameOverEvent;
    public SceneLoadedEventSO SceneLoadedEvent => sceneLoadedEvent;

    //声明：不可 ESC 关闭 + 打开时阻塞全局面板输入（玩家必须走重试流程）
    public bool CloseOnEscape => false;
    public bool BlocksGlobalInput => true;

    private void OnEnable()
    {
        toggleGameOverEvent.toggleCanvasEvent += OnGameOver;
        sceneLoadedEvent.SceneLoadedEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        toggleGameOverEvent.toggleCanvasEvent -= OnGameOver;
        sceneLoadedEvent.SceneLoadedEvent -= OnSceneLoaded;
    }

    private void OnSceneLoaded(GameSceneSO _)
    {
        OnGameOver(false);
    }

    private void OnGameOver(bool state)
    {
        if (state)
        {
            TimeManager.Instance.PauseGame();
        }
        else
        {
            TimeManager.Instance.ResumeGame();
        }

        ((ICanvasManager)this).SetCanvaState(gameOverGroup, MyEnums.CanvasToToggle.GameOver, state);
    }
}
