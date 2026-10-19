using System.Collections.Generic;
using Core.Scripts.Camera.Runtime;
using Core.Scripts.Character.Control;
using UnityEngine;

namespace Core.Scripts.Character.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class CharacterVisual : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private UnityEngine.Camera[] cameras = new UnityEngine.Camera[0];
        [SerializeField] private AudioListener[] listeners = new AudioListener[0];
        [SerializeField] private ThirdPersonCamera[] cameraControllers = new ThirdPersonCamera[0];
        [SerializeField] private PlayerCharacterController standaloneController;
        [SerializeField] private CharacterController movementCollider;

        [Header("发辫跟随")]
        // 可选修正项，在 Animator 完成骨骼更新后执行。
        [SerializeField] private CharacterBoneFollower ponytailFollower = new CharacterBoneFollower();

        public Animator Animator => animator;
        public UnityEngine.Camera InputCamera => inputCamera;
        public UnityEngine.Camera[] Cameras => cameras;
        public AudioListener[] Listeners => listeners;
        public ThirdPersonCamera[] CameraControllers => cameraControllers;
        public PlayerCharacterController StandaloneController => standaloneController;
        public CharacterController MovementCollider => movementCollider;

        private void Awake()
        {
            ponytailFollower?.Initialize();
        }

        private void LateUpdate()
        {
            // Animator 已完成骨骼计算后，再恢复发辫根节点相对头部的偏移。
            // 先于默认执行顺序的 DynamicBone 更新，使粒子读取修正后的根姿势。
            ponytailFollower?.Apply();
        }

        public bool TryValidate(bool requireCamera, out string issue)
        {
            if (animator == null || !BelongsToVisual(animator)
                || animator.runtimeAnimatorController == null)
            {
                issue = "Animator 及其 Controller 必须配置在视觉层级内";
                return false;
            }
            if (!ValidateReferences(cameras) || !ValidateReferences(listeners)
                || !ValidateReferences(cameraControllers))
            {
                issue = "相机组件数组包含空项、重复项或层级外引用";
                return false;
            }
            if (inputCamera != null && System.Array.IndexOf(cameras, inputCamera) < 0)
            {
                issue = "主输入相机必须包含在 Camera 数组中";
                return false;
            }
            if (requireCamera && (inputCamera == null || !inputCamera.CompareTag("MainCamera")
                || listeners.Length == 0 || cameraControllers.Length == 0))
            {
                issue = "本地视觉需要主输入相机、MainCamera 标签、监听器和相机控制器";
                return false;
            }
            if (standaloneController == null || !BelongsToVisual(standaloneController)
                || movementCollider == null || !BelongsToVisual(movementCollider))
            {
                issue = "完整角色 Prefab 需要显式配置角色控制器和 CharacterController";
                return false;
            }
            if (ponytailFollower == null || !ponytailFollower.IsConfigured(transform))
            {
                issue = "发辫跟随需要同一视觉层级的骨骼引用和已保存的初始偏移";
                return false;
            }
            issue = null;
            return true;
        }

        private bool ValidateReferences<T>(T[] references) where T : Component
        {
            if (references == null) return false;
            var unique = new HashSet<T>();
            foreach (T reference in references)
                if (reference == null || !BelongsToVisual(reference) || !unique.Add(reference))
                    return false;
            return true;
        }

        private bool BelongsToVisual(Component component)
        {
            return component.transform == transform || component.transform.IsChildOf(transform);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            animator = GetComponentInChildren<Animator>(true);
            cameras = GetComponentsInChildren<UnityEngine.Camera>(true);
            listeners = GetComponentsInChildren<AudioListener>(true);
            cameraControllers = GetComponentsInChildren<ThirdPersonCamera>(true);
            inputCamera = cameras.Length == 1 ? cameras[0] : null;
            standaloneController = GetComponentInChildren<PlayerCharacterController>(true);
            movementCollider = GetComponentInChildren<CharacterController>(true);
        }

        public bool CaptureBonePose()
        {
            return ponytailFollower != null && ponytailFollower.CaptureEditorPose(animator);
        }
#endif
    }
}
