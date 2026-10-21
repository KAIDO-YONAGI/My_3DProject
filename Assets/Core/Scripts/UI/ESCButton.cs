using Core.Scripts.Common;
using Core.Scripts.UI.SystemCanvasManagers;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Scripts.UI
{
    public class ESCButton : MonoBehaviour
    {
        [Tooltip("点击后请求关闭指定面板的按钮。")]
        [SerializeField] private Button escButton;

        [Tooltip("该按钮要关闭的面板类型。显式关闭不受 Close On Escape 限制。")]
        [SerializeField] private CanvasToToggle canvasToESC;

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名报错，订阅点直接解引用。
        private void ValidateReferences()
        {
            if (escButton == null)
            {
                Debug.LogError($"[{nameof(ESCButton)}] escButton 未指定：点击不会关闭任何面板。", this);
            }
        }

        private void OnEnable()
        {
            escButton.onClick.AddListener(OnESC);
        }

        private void OnDisable()
        {
            escButton.onClick.RemoveListener(OnESC);
        }

        private void OnESC()
        {
            UIManager.Instance.RequestCanvasClose(canvasToESC);
        }
    }
}
