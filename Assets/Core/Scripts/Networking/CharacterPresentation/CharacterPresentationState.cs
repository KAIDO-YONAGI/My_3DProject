using Core.Scripts.Camera;
using Core.Scripts.Character.Presentation;
using UnityEngine;

namespace Core.Scripts.Networking.CharacterPresentation
{
    /// <summary>缓存一个网络玩家的角色实例、来源资源及其相机和音频表现状态。</summary>
    class CharacterPresentationState
    {
        public GameObject Instance;
        public int CharacterId;
        // 最近一次相机配置使用的本地身份，用于判断后续回调是否需要刷新。
        public bool CameraIsLocal;
        public GameObject SourcePrefab;
        public UnityEngine.Camera[] Cameras;
        public AudioListener[] Listeners;
        public ThirdPersonCamera[] CameraControllers;
        public CharacterVisual Visual;
    }
}
