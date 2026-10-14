using System;
using UnityEngine;

namespace Core.Scripts.Character.Presentation
{
    /// <summary>
    /// 修正未正确挂在头骨层级下的发辫根节点。
    /// CharacterVisual 在 Awake 初始化、LateUpdate 应用，使修正发生在 Animator 更新骨骼之后。
    /// </summary>
    [Serializable]
    public class CharacterBoneFollower
    {
        // 关闭后不查找骨骼，也不执行运行时修正。
        [SerializeField] private bool enabled = true;

        // headBone 可由 Inspector 指定；编辑器保存姿势时从人形 Animator 自动获取 Head 骨骼。
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform hairRigRoot;

        // 初始化成功后保存发辫根节点相对头骨的位置和旋转。
        private bool initialized;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private bool poseCaptured;

        /// <summary>
        /// 检查编辑器保存的骨骼引用和姿势。
        /// </summary>
        public bool IsConfigured(Transform visualRoot)
        {
            return !enabled || (poseCaptured && headBone != null && hairRigRoot != null
                && headBone.IsChildOf(visualRoot) && hairRigRoot.IsChildOf(visualRoot));
        }

        /// <summary>
        /// 使用编辑器保存的偏移；引用无效时保持未初始化状态。
        /// </summary>
        public void Initialize()
        {
            initialized = enabled && poseCaptured && CanFollow(hairRigRoot);
        }

#if UNITY_EDITOR
        public bool CaptureEditorPose(Animator animator)
        {
            if (!enabled)
            {
                return true;
            }

            if (headBone == null && animator != null && animator.isHuman)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (headBone == null || !CanFollow(hairRigRoot))
            {
                return false;
            }

            // 保存相对头骨的初始偏移，动画更新后只恢复这一层关系。
            localPosition = headBone.InverseTransformPoint(hairRigRoot.position);
            localRotation = Quaternion.Inverse(headBone.rotation) * hairRigRoot.rotation;
            poseCaptured = true;
            return true;
        }
#endif

        /// <summary>
        /// 在 LateUpdate 中把保存的局部偏移重新换算到世界空间。
        /// </summary>
        public void Apply()
        {
            if (!initialized || !CanFollow(hairRigRoot))
            {
                return;
            }

            hairRigRoot.SetPositionAndRotation(
                headBone.TransformPoint(localPosition),
                headBone.rotation * localRotation);
        }

        // 已经位于头骨子层级的对象会自然跟随，不需要也不能再次施加变换。
        private bool CanFollow(Transform target)
        {
            return target != null
                   && headBone != null
                   && target != headBone
                   && !target.IsChildOf(headBone);
        }
    }
}
