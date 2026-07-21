using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理一套跑步或冲刺方向动画。
/// PlayerCharacterController 保存该配置，CharacterAnimator 根据模板方向槽位查询最接近的片段。
/// </summary>
[Serializable]
public sealed class DirectionalAnimationSet
{
    // label 仅用于区分 Inspector 中的动画组。
    [SerializeField] private string label = "Directional Blend";
    [SerializeField] private List<DirectionalAnimationSample> samples = new List<DirectionalAnimationSample>();

    public string Label => label;
    public IReadOnlyList<DirectionalAnimationSample> Samples => samples;

    /// <summary>
    /// 至少存在一个有效片段时，这套动画才可以参与 Idle/Run/Sprint 混合。
    /// </summary>
    public bool HasValidSamples
    {
        get
        {
            for (int index = 0; index < samples.Count; index++)
            {
                if (samples[index] != null && samples[index].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 使用二维距离寻找最接近目标方向的动画。
    /// 某个方向素材缺失时，附近方向仍可作为回退，避免模板槽位为空。
    /// </summary>
    public AnimationClip FindClosestClip(Vector2 direction, AnimationClip fallback = null)
    {
        AnimationClip closestClip = fallback;
        float closestDistance = float.PositiveInfinity;

        for (int index = 0; index < samples.Count; index++)
        {
            DirectionalAnimationSample sample = samples[index];
            if (sample == null || !sample.IsValid)
            {
                continue;
            }

            // 只比较平方距离，结果相同但不需要执行开方。
            float distance = (sample.Position - direction).sqrMagnitude;
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestClip = sample.Clip;
        }

        return closestClip;
    }
}
