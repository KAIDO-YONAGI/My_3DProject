using UnityEngine;

namespace Core.Scripts.UI.SystemCanvasManagers
{
    /// <summary>根据运行环境停止编辑器播放或退出构建后的游戏。</summary>
    public class ExitManager : MonoBehaviour
    {
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 在编辑器里停止运行
#else
    Application.Quit(); // 在构建的游戏里退出
#endif
        }
    }
}
