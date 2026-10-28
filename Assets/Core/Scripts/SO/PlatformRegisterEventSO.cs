using System;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Core.Scripts.Scene.VoxelPlatform
{
    /// <summary>向场景常驻的破坏管理器发布平台、网络 ID 与所属场景。</summary>
    [CreateAssetMenu(fileName = "PlatformRegisterEvent", menuName = "Events/Platform Register")]
    public sealed class PlatformRegisterEventSO : ScriptableObject
    {
        /// <summary>平台根节点请求加入运行时注册表时触发。</summary>
        public event Action<VoxelPlatformRenderer, int, UnityScene> PlatformRegistrationRequested;

        /// <summary>发布包含平台组件、稳定 ID 和实际加载场景的注册请求。</summary>
        public void RaisePlatformRegistration(VoxelPlatformRenderer platform, int platformId, UnityScene scene)
        {
            PlatformRegistrationRequested?.Invoke(platform, platformId, scene);
        }
    }
}
