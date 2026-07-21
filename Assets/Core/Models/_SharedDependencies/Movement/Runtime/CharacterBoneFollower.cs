using System;
using UnityEngine;

[Serializable]
public sealed class CharacterBoneFollower
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform hairRigRoot;

    private bool initialized;
    private Vector3 localPosition;
    private Quaternion localRotation;

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

    private bool CanFollow(Transform target)
    {
        return target != null
            && headBone != null
            && target != headBone
            && !target.IsChildOf(headBone);
    }
}
