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

    public int ValidSampleCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < samples.Count; index++)
            {
                if (samples[index] != null && samples[index].IsValid)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasValidSamples => ValidSampleCount > 0;

    public void SetLabel(string newLabel)
    {
        label = newLabel;
    }

    public void ReplaceSamples(IReadOnlyList<DirectionalAnimationSample> newSamples)
    {
        samples.Clear();
        if (newSamples == null)
        {
            return;
        }

        for (int index = 0; index < newSamples.Count; index++)
        {
            DirectionalAnimationSample source = newSamples[index];
            if (source == null)
            {
                continue;
            }

            samples.Add(new DirectionalAnimationSample(source.Label, source.Position, source.Clip));
        }
    }
}
