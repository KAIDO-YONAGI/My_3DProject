using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DirectionalAnimationSet
{
    [SerializeField] private string label = "Directional Blend";
    [SerializeField] private List<DirectionalAnimationSample> samples = new List<DirectionalAnimationSample>();

    public string Label => label;
    public IReadOnlyList<DirectionalAnimationSample> Samples => samples;

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
