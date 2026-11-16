using System.Collections.Generic;
using Core.Scripts.SO;
using UnityEngine;

namespace Core.Scripts.Scene
{
    /// <summary>
    /// 关卡内传送触发器：玩家进入二维触发区域后，通过 SceneChanger 唯一入口切换完整场景组。
    /// </summary>
    public class SceneToggler : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [SerializeField] private Vector3 newPosition;
        [Tooltip("按顺序叠加加载的完整目标场景组。不要把常驻场景放进此列表。")]
        [SerializeField] private List<GameSceneSO> sceneToLoad = new();
        [SerializeField] private bool isToFade = true;

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名断言，触发回调直接解引用。
        private void ValidateReferences()
        {
            Debug.Assert(sceneToLoad != null && sceneToLoad.Count > 0,
                $"{nameof(sceneToLoad)} 未配置：进入触发器不会切换场景。", this);

            for (int index = 0; index < sceneToLoad.Count; index++)
            {
                Debug.Assert(sceneToLoad[index] != null,
                    $"{nameof(sceneToLoad)}[{index}] 为空：该场景组条目会被跳过。", this);
            }
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (!collider.CompareTag(PlayerTag))
                return;

            // 场景切换走 SceneChanger 的唯一入口；直接 Raise 事件只会通知订阅方，不会切场景
            SceneChanger.RequiredInstance.RequestSceneLoad(sceneToLoad, newPosition, isToFade);
        }
    }
}
