using System;
using UnityEngine;

[Serializable]
public sealed class DirectionalAnimationSample
{
    [SerializeField] private string label;
    [SerializeField] private Vector2 position;
    [SerializeField] private AnimationClip clip;

    public DirectionalAnimationSample(string label, Vector2 position, AnimationClip clip)
    {
        this.label = label;
        this.position = position;
        this.clip = clip;
    }

    public string Label => label;
    public Vector2 Position => position;
    public AnimationClip Clip => clip;
    public bool IsValid => clip != null;
}
