using UnityEngine;

public class TimeManager : YSingleton<TimeManager>
{
    private bool isGamePaused;
    private int pauseCount = 0;
    public bool IsGamePaused()
    {
        return isGamePaused;
    }
    public void PauseGame()//引用计数：多系统同时暂停时，只有全部 Resume 后才真正恢复时间
    {
        pauseCount++;
        if (pauseCount > 1) return;

        Time.timeScale = 0;
        isGamePaused = true;
    }

    public void ResumeGame()
    {
        if (pauseCount == 0) return;

        pauseCount--;
        if (pauseCount > 0) return;

        Time.timeScale = 1;
        isGamePaused = false;
    }
    public void ForceResumeGame()//强制恢复游戏，重置暂停次数，注意此时要配合画布组的状态变化来调用
    {
        pauseCount = 0;
        Time.timeScale = 1;
        isGamePaused = false;
    }
}
