using System.Collections.Generic;
using UnityEngine;

public class ButtonSceneToggler : MonoBehaviour
{
    [Tooltip("按顺序叠加加载的完整目标场景组。不要把常驻场景放进此列表。")]
    [SerializeField] private List<GameSceneSO> sceneToLoad = new List<GameSceneSO>();
    [SerializeField] private CanvasGroup ButtonCanvas;
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private bool isToFade = true;

    public void HandleSceneToggle()//editor内由button组件绑定
    {
        ButtonCanvas.alpha = 0;
        ButtonCanvas.interactable = false;
        ButtonCanvas.blocksRaycasts = false;

        if (sceneToLoad != null && sceneToLoad.Count > 0 && sceneToLoad[0] != null)
        {
            // 场景切换走 SceneChanger 的唯一入口；直接 Raise 事件只会通知订阅方，不会切场景
            SceneChanger.Instance.RequestSceneLoad(sceneToLoad, newPosition, isToFade);
        }
        else
        {
            Debug.LogWarning("[ButtonSceneToggler] sceneToLoad 未配置。重试按钮请改用 RetryButton 组件。", this);
        }
    }
}
