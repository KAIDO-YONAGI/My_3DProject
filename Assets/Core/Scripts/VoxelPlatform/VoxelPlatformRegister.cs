using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;
using Core.Scripts.SO;

namespace Core.Scripts.VoxelPlatform
{
    /// <summary>集中配置同场景平台引用和网络 ID，并在场景加载后发布注册事件。</summary>
    public sealed class VoxelPlatformRegister : MonoBehaviour
    {
        /// <summary>保存一个平台根组件及其跨客户端一致的网络 ID。</summary>
        [System.Serializable]
        public struct PlatformReference
        {
            public VoxelPlatformRenderer renderer;
            [Min(1)] public int id;
        }

        [SerializeField] private PlatformReference[] platforms;
        [SerializeField] private PlatformRegisterEventSO registerEvent;

        private bool registrationRaised;

        private void Awake()
        {
            ValidateReferences();
        }

        // 必填引用只校验一次：缺失时按字段名与下标断言，注册流程直接解引用。
        private void ValidateReferences()
        {
            Debug.Assert(platforms != null && platforms.Length > 0,
                $"{nameof(platforms)} 未配置：场景中的平台不会注册。", this);
            Debug.Assert(registerEvent != null,
                $"{nameof(registerEvent)} 未指定：平台注册事件无法发布。", this);

            for (int index = 0; index < platforms.Length; index++)
            {
                Debug.Assert(platforms[index].renderer != null,
                    $"{nameof(platforms)}[{index}].renderer 为空：该平台不会注册。", this);
                Debug.Assert(platforms[index].id > 0,
                    $"{nameof(platforms)}[{index}].id 不是正数：该平台不会注册。", this);
            }
        }

        /// <summary>监听 Unity 场景加载完成事件，以便只发布所属场景的平台。</summary>
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>场景对象完成初始化后补发注册，覆盖组件晚于场景加载启用的情况。</summary>
        private void Start()
        {
            TryRaiseRegistrations(gameObject.scene);
        }

        /// <summary>停止监听场景加载，避免禁用或销毁后继续响应。</summary>
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>仅在该组件所属场景加载完成时触发平台注册。</summary>
        private void OnSceneLoaded(UnityScene loadedScene, LoadSceneMode loadMode)
        {
            if (loadedScene == gameObject.scene)
                TryRaiseRegistrations(loadedScene);
        }

        /// <summary>逐项发布父节点配置的平台和 ID，并附带平台所属的实际场景。</summary>
        private void TryRaiseRegistrations(UnityScene scene)
        {
            if (registrationRaised || !scene.IsValid() || !scene.isLoaded)
                return;

            bool raisedAnyRegistration = false;
            for (int index = 0; index < platforms.Length; index++)
            {
                PlatformReference reference = platforms[index];
                if (reference.renderer.gameObject.scene != scene)
                {
#if UNITY_EDITOR
                    Debug.LogError("平台注册项必须引用当前场景中的平台根组件。", this);
#endif
                    continue;
                }

                registerEvent.RaisePlatformRegistration(reference.renderer, reference.id,
                    reference.renderer.gameObject.scene);
                raisedAnyRegistration = true;
            }

            registrationRaised = raisedAnyRegistration;
        }
    }
}