using System;
using UnityEngine;

[Serializable]
public sealed class CharacterBoneFollower
{
    private static readonly string[] HeadBoneCandidates = { "Bip001-Head", "Bone_head", "head_adjust", "head", "Head" };
    private static readonly string[] LeftPonytailTargetCandidates = { "bone_hairLB00", "Bone_hairBL00", "Bn_l_hairC_001", "Bn_l_hairB_001" };
    private static readonly string[] RightPonytailTargetCandidates = { "bone_hairRB00", "Bone_hairBR00", "Bn_r_hairC_001", "Bn_r_hairB_001" };
    private static readonly string[] LeftPonytailSourceCandidates = { "Bn_l_hairC_001", "Bn_l_hairB_001", "Bn_l_hairA_001" };
    private static readonly string[] RightPonytailSourceCandidates = { "Bn_r_hairC_001", "Bn_r_hairB_001", "Bn_r_hairA_001" };

    [SerializeField] private bool enabled = true;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform hairRigRoot;
    [SerializeField] private Transform leftPonytailRoot;
    [SerializeField] private Transform rightPonytailRoot;
    [SerializeField] private Transform leftPonytailSource;
    [SerializeField] private Transform rightPonytailSource;

    private bool hasHairRigBinding;
    private Vector3 hairRigLocalPosition;
    private Quaternion hairRigLocalRotation = Quaternion.identity;
    private bool hasLeftFallbackBinding;
    private Vector3 leftFallbackLocalPosition;
    private Quaternion leftFallbackLocalRotation = Quaternion.identity;
    private bool hasRightFallbackBinding;
    private Vector3 rightFallbackLocalPosition;
    private Quaternion rightFallbackLocalRotation = Quaternion.identity;

    public void AutoAssign(Animator animator, Transform root)
    {
        if (!enabled || root == null)
        {
            return;
        }

        Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);

        if (headBone == null)
        {
            headBone = FindChildByName(hierarchy, HeadBoneCandidates);

            if (animator != null && animator.isHuman && headBone == null)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        if (leftPonytailRoot == null)
        {
            leftPonytailRoot = FindChildByName(hierarchy, LeftPonytailTargetCandidates);
        }

        if (rightPonytailRoot == null)
        {
            rightPonytailRoot = FindChildByName(hierarchy, RightPonytailTargetCandidates);
        }

        if (hairRigRoot == null)
        {
            hairRigRoot = FindHairRigRoot(leftPonytailRoot, rightPonytailRoot, headBone, root);
        }

        if (leftPonytailSource == null)
        {
            leftPonytailSource = FindFollowSource(hierarchy, headBone, leftPonytailRoot, LeftPonytailSourceCandidates);
        }

        if (rightPonytailSource == null)
        {
            rightPonytailSource = FindFollowSource(hierarchy, headBone, rightPonytailRoot, RightPonytailSourceCandidates);
        }
    }

    public void InitializeRuntimeBinding()
    {
        hasHairRigBinding = false;
        hasLeftFallbackBinding = false;
        hasRightFallbackBinding = false;

        if (!enabled || headBone == null)
        {
            return;
        }

        if (IsRuntimeFollowTarget(hairRigRoot))
        {
            hasHairRigBinding = true;
            hairRigLocalPosition = headBone.InverseTransformPoint(hairRigRoot.position);
            hairRigLocalRotation = Quaternion.Inverse(headBone.rotation) * hairRigRoot.rotation;
        }

        CacheFallbackBinding(
            leftPonytailRoot,
            leftPonytailSource,
            ref hasLeftFallbackBinding,
            ref leftFallbackLocalPosition,
            ref leftFallbackLocalRotation);

        CacheFallbackBinding(
            rightPonytailRoot,
            rightPonytailSource,
            ref hasRightFallbackBinding,
            ref rightFallbackLocalPosition,
            ref rightFallbackLocalRotation);
    }

    public void Apply()
    {
        if (!enabled)
        {
            return;
        }

        if (ApplyHairRigRoot())
        {
            return;
        }

        ApplyFollow(
            leftPonytailRoot,
            leftPonytailSource,
            hasLeftFallbackBinding,
            leftFallbackLocalPosition,
            leftFallbackLocalRotation);

        ApplyFollow(
            rightPonytailRoot,
            rightPonytailSource,
            hasRightFallbackBinding,
            rightFallbackLocalPosition,
            rightFallbackLocalRotation);
    }

    private bool ApplyHairRigRoot()
    {
        if (!hasHairRigBinding || !IsRuntimeFollowTarget(hairRigRoot))
        {
            return false;
        }

        hairRigRoot.SetPositionAndRotation(
            headBone.TransformPoint(hairRigLocalPosition),
            headBone.rotation * hairRigLocalRotation);
        return true;
    }

    private void ApplyFollow(
        Transform ponytailRoot,
        Transform followSource,
        bool hasFallbackBinding,
        Vector3 fallbackLocalPosition,
        Quaternion fallbackLocalRotation)
    {
        if (ponytailRoot == null)
        {
            return;
        }

        if (headBone != null && (ponytailRoot == headBone || ponytailRoot.IsChildOf(headBone)))
        {
            return;
        }

        if (HasDirectFollowSource(ponytailRoot, followSource))
        {
            ponytailRoot.SetPositionAndRotation(followSource.position, followSource.rotation);
            return;
        }

        if (!hasFallbackBinding || headBone == null)
        {
            return;
        }

        ponytailRoot.SetPositionAndRotation(
            headBone.TransformPoint(fallbackLocalPosition),
            headBone.rotation * fallbackLocalRotation);
    }

    private void CacheFallbackBinding(
        Transform ponytailRoot,
        Transform followSource,
        ref bool hasFallbackBinding,
        ref Vector3 fallbackLocalPosition,
        ref Quaternion fallbackLocalRotation)
    {
        hasFallbackBinding = false;
        if (!IsRuntimeFollowTarget(ponytailRoot))
        {
            return;
        }

        if (HasDirectFollowSource(ponytailRoot, followSource))
        {
            return;
        }

        hasFallbackBinding = true;
        fallbackLocalPosition = headBone.InverseTransformPoint(ponytailRoot.position);
        fallbackLocalRotation = Quaternion.Inverse(headBone.rotation) * ponytailRoot.rotation;
    }

    private static Transform FindChildByName(Transform[] transforms, string[] candidates)
    {
        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            string candidate = candidates[candidateIndex];
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                if (transforms[transformIndex].name == candidate)
                {
                    return transforms[transformIndex];
                }
            }
        }

        return null;
    }

    private static Transform FindFollowSource(Transform[] transforms, Transform head, Transform ponytailRoot, string[] candidates)
    {
        if (ponytailRoot == null)
        {
            return null;
        }

        if (head != null && ponytailRoot.IsChildOf(head))
        {
            return null;
        }

        for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            string candidate = candidates[candidateIndex];
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Transform candidateTransform = transforms[transformIndex];
                if (candidateTransform == null || candidateTransform.name != candidate || candidateTransform == ponytailRoot)
                {
                    continue;
                }

                if (candidateTransform.IsChildOf(ponytailRoot))
                {
                    continue;
                }

                return candidateTransform;
            }
        }

        return head;
    }

    private static bool HasDirectFollowSource(Transform ponytailRoot, Transform followSource)
    {
        return followSource != null
            && followSource != ponytailRoot
            && !followSource.IsChildOf(ponytailRoot);
    }

    private static Transform FindHairRigRoot(Transform leftRoot, Transform rightRoot, Transform head, Transform sceneRoot)
    {
        Transform commonAncestor = FindCommonAncestor(leftRoot, rightRoot);
        if (IsValidHairRigRoot(commonAncestor, head, sceneRoot))
        {
            return commonAncestor;
        }

        if (IsValidHairRigRoot(leftRoot != null ? leftRoot.parent : null, head, sceneRoot))
        {
            return leftRoot.parent;
        }

        if (IsValidHairRigRoot(rightRoot != null ? rightRoot.parent : null, head, sceneRoot))
        {
            return rightRoot.parent;
        }

        return null;
    }

    private static Transform FindCommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null)
        {
            return null;
        }

        Transform currentA = a;
        while (currentA != null)
        {
            Transform currentB = b;
            while (currentB != null)
            {
                if (currentA == currentB)
                {
                    return currentA;
                }

                currentB = currentB.parent;
            }

            currentA = currentA.parent;
        }

        return null;
    }

    private static bool IsValidHairRigRoot(Transform candidate, Transform head, Transform sceneRoot)
    {
        if (candidate == null || candidate == head || candidate == sceneRoot)
        {
            return false;
        }

        if (head != null && candidate.IsChildOf(head))
        {
            return false;
        }

        return true;
    }

    private bool IsRuntimeFollowTarget(Transform target)
    {
        return target != null
            && headBone != null
            && target != headBone
            && !target.IsChildOf(headBone);
    }
}
