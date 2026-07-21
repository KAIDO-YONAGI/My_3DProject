using System;
using UnityEngine;

/// <summary>
/// 描述一个局部移动方向与动画片段的对应关系。
/// DirectionalAnimationSet 会聚合多个采样点，CharacterAnimator 再用它们填充二维 Blend Tree。
/// </summary>
[Serializable]
public sealed class DirectionalAnimationSample
{
    // label 只用于 Inspector 辨认素材，不参与运行时计算。
    [SerializeField] private string label;

    // x 表示左右，y 表示前后，坐标约定与 Animator 的 MoveX/MoveY 一致。
    [SerializeField] private Vector2 position;
    [SerializeField] private AnimationClip clip;

    /// <summary>
    /// 创建一个方向采样点，通常用于编辑器工具自动生成配置。
    /// </summary>
    public DirectionalAnimationSample(string label, Vector2 position, AnimationClip clip)
    {
        this.label = label;
        this.position = position;
        this.clip = clip;
    }

    public string Label => label;
    public Vector2 Position => position;
    public AnimationClip Clip => clip;

    // 空片段不会参与最近方向搜索，避免覆盖器得到无效动画。
    public bool IsValid => clip != null;
}
