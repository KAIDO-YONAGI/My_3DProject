using Core.Scripts.Camera.Runtime;
using Core.Scripts.Character.Presentation;
using UnityEngine;

namespace Core.Scripts.Networking.CharacterPresentation
{
    internal sealed class CharacterPresentationState
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
