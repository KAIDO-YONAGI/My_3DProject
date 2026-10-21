using System;
using Core.Scripts.Scene;
using Core.Scripts.SO;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Scripts.UI
{
    /// <summary>
    /// 重试按钮：隐藏自身画布、强制解除暂停并发出重试事件。
    /// 由常驻场景上的 SceneChanger 监听并整组重载当前场景组。
    /// </summary>
    public class RetryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private VoidEventSO retryEventSO;

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名报错，点击流程直接解引用。
        private void ValidateReferences()
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(RetryButton)}] button 未指定：点击不会被监听。", this);
            }

            if (retryEventSO == null)
            {
                Debug.LogError($"[{nameof(RetryButton)}] retryEventSO 未指定：重试事件无法广播。", this);
            }
        }

        private void OnEnable()
        {
            button.onClick.AddListener(HandleRetry);
        }
        private void OnDisable()
        {
            button.onClick.RemoveAllListeners();
        }
        public void HandleRetry()//editor内由button组件绑定
        {
            TimeManager.Instance.ForceResumeGame();
            retryEventSO.OnEventRaised();
        }
    }
}
