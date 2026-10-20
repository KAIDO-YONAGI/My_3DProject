using UnityEngine;

namespace Core.Scripts.UI.SystemCanvasManagers
{
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
