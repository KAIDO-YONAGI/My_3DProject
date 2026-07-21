using System;
using UnityEngine;

/// <summary>
/// 修正未正确挂在头骨层级下的发辫根节点。
/// PlayerCharacterController 在 Awake 初始化、LateUpdate 应用，使修正发生在 Animator 更新骨骼之后。
/// </summary>
[Serializable]
public sealed class CharacterBoneFollower
{
    // 关闭后不查找骨骼，也不执行运行时修正。
    [SerializeField] private bool enabled = true;

    // headBone 可由 Inspector 指定；留空时从人形 Animator 自动获取 Head 骨骼。
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform hairRigRoot;

    // 初始化成功后保存发辫根节点相对头骨的位置和旋转。
    private bool initialized;
    private Vector3 localPosition;
    private Quaternion localRotation;

    /// <summary>
    /// 解析头骨引用并记录模型初始偏移；引用无效时保持未初始化状态。
    /// </summary>
    public void Initialize(Animator animator)
    {
        initialized = false;
        if (!enabled)
        {
            return;
        }

        if (headBone == null && animator != null && animator.isHuman)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        if (headBone == null || !CanFollow(hairRigRoot))
        {
            return;
        }

        // 保存相对头骨的初始偏移，动画更新后只恢复这一层关系。
        localPosition = headBone.InverseTransformPoint(hairRigRoot.position);
        localRotation = Quaternion.Inverse(headBone.rotation) * hairRigRoot.rotation;
        initialized = true;
    }

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
