using UnityEngine;

namespace Core.Scripts.Character.Animation
{
    /// <summary>
    /// 读取 CharacterMotion，驱动编辑器配置的 Animator，并处理落地衔接。
    /// </summary>
    public class CharacterAnimator
    {
        // 缓存参数哈希，避免 Update 中反复按字符串查找 Animator 参数。
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IdleWeightHash = Animator.StringToHash("IdleWeight");
        private static readonly int RunWeightHash = Animator.StringToHash("RunWeight");
        private static readonly int SprintWeightHash = Animator.StringToHash("SprintWeight");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");

        private readonly Animator animator;
        private bool initialized;
        private bool wasGrounded = true;

        /// <summary>
        /// 使用模型 Animator 上的 Controller 配置。
        /// </summary>
        public CharacterAnimator(Animator animator)
        {
            this.animator = animator;
        }

        /// <summary>
        /// 把 Animator 初始化为 Grounded + Idle。
        /// </summary>
        public bool Initialize()
        {
            initialized = false;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.SetBool(GroundedHash, true);
            ResetMovementParameters();
            wasGrounded = true;
            initialized = true;
            return true;
        }

        /// <summary>
        /// 把 CharacterMotion 应用到 Animator。
        /// Grounded 参数负责进入和退出 Jump，移动权重只负责 Grounded 内部的直接混合。
        /// </summary>
        public void Apply(CharacterMotion motion)
        {
            // 模型切换期间旧 Animator 可能已被 Unity 销毁，避免把失效引用传给动画 API。
            if (!initialized || animator == null)
            {
                return;
            }

            if (motion.IsGrounded && !wasGrounded)
            {
                // 落地先回到 Idle，避免空中保留的跑步权重影响衔接。
                ResetMovementParameters();
            }
            else
            {
                ApplyGroundedBlend(motion);
            }

            // 单一布尔值同时覆盖主动跳跃和走下平台，不再与 Jump Trigger 形成两条竞争路径。
            animator.SetBool(GroundedHash, motion.IsGrounded);
            wasGrounded = motion.IsGrounded;
        }

        /// <summary>
        /// 把 Grounded Blend Tree 立即设为纯 Idle，用于初始化和落地首帧。
        /// </summary>
        private void ResetMovementParameters()
        {
            animator.SetFloat(MoveXHash, 0f);
            animator.SetFloat(MoveYHash, 0f);
            animator.SetFloat(IdleWeightHash, 1f);
            animator.SetFloat(RunWeightHash, 0f);
            animator.SetFloat(SprintWeightHash, 0f);
        }

        /// <summary>
        /// 根据输入意图直接设置 Idle、Run、Sprint 权重，并把局部方向写入二维 Blend Tree。
        /// </summary>
        private void ApplyGroundedBlend(CharacterMotion motion)
        {
            float locomotionWeight = Mathf.Clamp01(motion.LocomotionWeight01);

            animator.SetFloat(MoveXHash, motion.LocalDirection.x);
            animator.SetFloat(MoveYHash, motion.LocalDirection.y);
            animator.SetFloat(IdleWeightHash, 1f - locomotionWeight);
            animator.SetFloat(RunWeightHash, motion.WantsSprint ? 0f : locomotionWeight);
            animator.SetFloat(SprintWeightHash, motion.WantsSprint ? locomotionWeight : 0f);
        }
    }
}
