using UnityEngine;

namespace Core.Scripts.Scene
{
    /// <summary>
    /// 浮空告示牌：绕竖轴朝向本地相机，并做上下浮动。
    /// 远程玩家的相机会被关闭，Camera.main 只会取到本地启用的相机。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class FloatingSignboard : MonoBehaviour
    {
        [Header("朝向")]
        [Tooltip("水平对齐速度，0 表示立即对齐。")]
        [SerializeField, Min(0f)] private float turnSpeed = 8f;

        [Header("浮动")]
        [Tooltip("上下浮动幅度，单位为米。")]
        [SerializeField, Min(0f)] private float floatAmplitude = 0.15f;
        [Tooltip("浮动周期，单位为秒。")]
        [SerializeField, Min(0.01f)] private float floatPeriod = 3f;
        [Tooltip("相位偏移，多个告示牌填不同值可以错开起伏。")]
        [SerializeField] private float phaseOffset;

        private Vector3 baseLocalPosition;

        private void OnEnable()
        {
            baseLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            ApplyFacing();
            ApplyFloat();
        }

        private void ApplyFacing()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 toCamera = camera.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            transform.rotation = turnSpeed <= 0f
                ? target
                : Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void ApplyFloat()
        {
            float offset = Mathf.Sin((Time.time / floatPeriod + phaseOffset) * Mathf.PI * 2f)
                           * floatAmplitude;
            transform.localPosition = baseLocalPosition + Vector3.up * offset;
        }
    }
}