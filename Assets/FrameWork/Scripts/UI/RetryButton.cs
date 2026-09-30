using UnityEngine;

/// <summary>
/// 重试按钮：隐藏自身画布、强制解除暂停并发出重试事件。
/// 由常驻场景上的 SceneChanger 监听并整组重载当前场景组。
/// </summary>
public class RetryButton : MonoBehaviour
{
    [SerializeField] private CanvasGroup ButtonCanvas;
    [SerializeField] private VoidEventSO retryEventSO;

    public void HandleRetry()//editor内由button组件绑定
    {
        if (ButtonCanvas != null)
        {
            ButtonCanvas.alpha = 0;
            ButtonCanvas.interactable = false;
            ButtonCanvas.blocksRaycasts = false;
        }

        if (TimeManager.Instance != null)
            TimeManager.Instance.ForceResumeGame();

        if (retryEventSO != null)
            retryEventSO.OnEventRaised();
        else
            Debug.LogError("[RetryButton] Retry Event SO is not assigned.", this);
    }
}
