using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡内传送触发器：玩家进入二维触发区域后，通过 SceneChanger 唯一入口切换完整场景组。
/// </summary>
public class SceneToggler : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [SerializeField] private Vector3 newPosition;
    [Tooltip("按顺序叠加加载的完整目标场景组。不要把常驻场景放进此列表。")]
    [SerializeField] private List<GameSceneSO> sceneToLoad = new List<GameSceneSO>();
    [SerializeField] private bool isToFade = true;

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (!collider.CompareTag(PlayerTag))
            return;

        // 场景切换走 SceneChanger 的唯一入口；直接 Raise 事件只会通知订阅方，不会切场景
        SceneChanger.Instance.RequestSceneLoad(sceneToLoad, newPosition, isToFade);
    }
}
