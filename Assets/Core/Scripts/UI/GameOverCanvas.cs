using Core.Scripts.Scene;
using Core.Scripts.SO;
using UnityEngine;

namespace Core.Scripts.UI
{
    /// <summary>
    /// GameOver 画布组：死亡时显示占位界面并暂停游戏，场景加载完成后自动收起并恢复时间。
    /// 当前不接入 UIManager，画布显隐由本组件直接控制；重试按钮复用 RetryButton 广播重试事件。
    /// </summary>
    public class GameOverCanvas : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private SceneLoadedEventSO sceneLoadedEvent;

        private void Awake()
        {
            Debug.Assert(canvasGroup != null,
                $"{nameof(canvasGroup)} 未指定：死亡界面无法显隐。", this);
            Debug.Assert(sceneLoadedEvent != null,
                $"{nameof(sceneLoadedEvent)} 未指定：场景加载后不会自动收起。", this);
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            sceneLoadedEvent.SceneLoadedEvent += OnSceneLoaded;
        }

        private void OnDisable()
        {
            sceneLoadedEvent.SceneLoadedEvent -= OnSceneLoaded;
        }

        /// <summary>显示死亡界面：打开画布、释放光标并暂停游戏。</summary>
        public void Show()
        {
            ApplyVisible(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            TimeManager.RequiredInstance.PauseGame();
        }

        /// <summary>收起死亡界面并恢复游戏时间；场景重载完成后由 SceneLoadedEvent 调用。</summary>
        public void Hide()
        {
            ApplyVisible(false);
            TimeManager.RequiredInstance.ResumeGame();
        }

        private void OnSceneLoaded(GameSceneSO _)
        {
            Hide();
        }

        private void ApplyVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}