using System;
using System.Collections.Generic;

namespace Core.Scripts.StateMachine
{
    /// <summary>
    /// 同步分层状态机。
    /// 每帧沿活动路径自上而下解析目标状态，命中后按最近公共祖先切换：
    /// 退出旧分支时自下而上，进入新分支时自上而下，全部在当帧内完成。
    /// 不使用异步任务，转换后同一帧即可继续更新新状态，因此不占用额外帧。
    /// </summary>
    /// <typeparam name="TContext">每帧传入状态机的上下文快照。</typeparam>
    public sealed class HierarchicalStateMachine<TContext>
    {
        private readonly HierarchicalState<TContext> root;

        public HierarchicalStateMachine(HierarchicalState<TContext> root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>根状态。</summary>
        public HierarchicalState<TContext> Root => root;

        /// <summary>当前活动路径最深处的状态。</summary>
        public HierarchicalState<TContext> Leaf => root.Leaf;

        /// <summary>根状态是否已经激活。</summary>
        public bool Started { get; private set; }

        /// <summary>
        /// 激活根状态，并沿默认子状态下钻到初始叶子。
        /// 只触发进入钩子，不解析转换，因此初始状态与上下文无关。
        /// </summary>
        public void Start(TContext context)
        {
            if (Started)
            {
                return;
            }

            Started = true;

            HierarchicalState<TContext> node = root;
            while (node != null)
            {
                node.Enter(context);
                node = node.DefaultChild;
            }
        }

        /// <summary>
        /// 推进一帧：先解析并执行转换，再更新新的活动路径，返回更新后的叶子状态。
        /// 未显式启动时按当前上下文补做一次启动。
        /// </summary>
        public HierarchicalState<TContext> Update(TContext context)
        {
            if (!Started)
            {
                Start(context);
            }

            HierarchicalState<TContext> from = root.Leaf;
            HierarchicalState<TContext> to = ResolveTarget(context);

            // 目标即当前叶子时不切换，避免同一状态反复触发退出和进入。
            if (to != null && !ReferenceEquals(to, from))
            {
                ChangeState(from, to, context);
            }

            root.UpdateActivePath(context);
            return root.Leaf;
        }

        /// <summary>沿活动路径自上而下询问，取第一个非空的转换目标。</summary>
        private HierarchicalState<TContext> ResolveTarget(TContext context)
        {
            HierarchicalState<TContext> node = root;
            while (node != null)
            {
                HierarchicalState<TContext> target = node.EvaluateTransition(context);
                if (target != null)
                {
                    return target;
                }

                node = node.ActiveChild;
            }

            return null;
        }

        /// <summary>
        /// 按最近公共祖先切换分支：先退出旧分支，再进入新分支。
        /// 以当前叶子作为起点，父状态发起的转换同样会正确退出其活动子状态。
        /// </summary>
        private void ChangeState(
            HierarchicalState<TContext> from,
            HierarchicalState<TContext> to,
            TContext context)
        {
            HierarchicalState<TContext> ancestor = FindLowestCommonAncestor(from, to);

            for (HierarchicalState<TContext> node = from;
                 node != null && !ReferenceEquals(node, ancestor);
                 node = node.Parent)
            {
                node.Exit(context);
            }

            var enterChain = new Stack<HierarchicalState<TContext>>();
            for (HierarchicalState<TContext> node = to;
                 node != null && !ReferenceEquals(node, ancestor);
                 node = node.Parent)
            {
                enterChain.Push(node);
            }

            while (enterChain.Count > 0)
            {
                enterChain.Pop().Enter(context);
            }
        }

        /// <summary>
        /// 查找两个状态的最近公共祖先；没有公共祖先时返回 null，此时按整条路径重建。
        /// </summary>
        private static HierarchicalState<TContext> FindLowestCommonAncestor(
            HierarchicalState<TContext> first,
            HierarchicalState<TContext> second)
        {
            var ancestors = new HashSet<HierarchicalState<TContext>>();
            for (HierarchicalState<TContext> node = first; node != null; node = node.Parent)
            {
                ancestors.Add(node);
            }

            for (HierarchicalState<TContext> node = second; node != null; node = node.Parent)
            {
                if (ancestors.Contains(node))
                {
                    return node;
                }
            }

            return null;
        }
    }
}
