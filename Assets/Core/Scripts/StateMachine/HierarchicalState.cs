namespace Core.Scripts.StateMachine
{
    /// <summary>
    /// 分层状态机的状态节点。
    /// 父状态承担整棵子树共有的转换规则，子状态只表达自身差异；
    /// 状态机沿活动路径自上而下询问转换目标，因此父状态优先于子状态。
    /// </summary>
    /// <typeparam name="TContext">每帧传入状态机的上下文快照。</typeparam>
    public abstract class HierarchicalState<TContext>
    {
        protected HierarchicalState(HierarchicalState<TContext> parent)
        {
            Parent = parent;
        }

        /// <summary>父状态；根状态的父状态为 null。</summary>
        public HierarchicalState<TContext> Parent { get; }

        /// <summary>处于活动路径上的直接子状态；没有子状态或本状态未激活时为 null。</summary>
        public HierarchicalState<TContext> ActiveChild { get; private set; }

        /// <summary>活动路径最深处的状态；叶子状态返回自身。</summary>
        public HierarchicalState<TContext> Leaf
        {
            get
            {
                HierarchicalState<TContext> node = this;
                while (node.ActiveChild != null)
                {
                    node = node.ActiveChild;
                }

                return node;
            }
        }

        /// <summary>
        /// 首次激活本状态时顺带激活的默认子状态；返回 null 表示本状态就是叶子。
        /// 只在状态机启动时使用一次，之后进入子树由状态机显式给出目标状态。
        /// </summary>
        protected virtual HierarchicalState<TContext> InitialChild => null;

        /// <summary>
        /// 本状态对当前帧给出的转换目标；返回 null 表示本帧不转换，继续询问子状态。
        /// 子树内的切换需要自行比较 ActiveChild，避免每帧重复触发进入和退出。
        /// </summary>
        protected virtual HierarchicalState<TContext> ResolveTransition(TContext context) => null;

        /// <summary>进入本状态时调用，此时父状态的 ActiveChild 已指向本状态。</summary>
        protected virtual void OnEnter(TContext context)
        {
        }

        /// <summary>退出本状态时调用，此时子状态已经全部退出。</summary>
        protected virtual void OnExit(TContext context)
        {
        }

        /// <summary>本状态处于活动路径上时每帧调用，先于子状态。</summary>
        protected virtual void OnUpdate(TContext context)
        {
        }

        // 以下成员由状态机在程序集内驱动，子类只重写上面的生命周期钩子。

        internal HierarchicalState<TContext> DefaultChild => InitialChild;

        internal HierarchicalState<TContext> EvaluateTransition(TContext context) => ResolveTransition(context);

        /// <summary>激活本状态并挂到父状态的活动子节点上。</summary>
        internal void Enter(TContext context)
        {
            if (Parent != null)
            {
                Parent.ActiveChild = this;
            }

            OnEnter(context);
        }

        /// <summary>
        /// 退出本状态。退出前先与父状态断开，避免父状态再沿活动路径退出本状态。
        /// </summary>
        internal void Exit(TContext context)
        {
            if (Parent != null && ReferenceEquals(Parent.ActiveChild, this))
            {
                Parent.ActiveChild = null;
            }

            ActiveChild = null;
            OnExit(context);
        }

        /// <summary>自上而下更新整条活动路径。</summary>
        internal void UpdateActivePath(TContext context)
        {
            OnUpdate(context);
            ActiveChild?.UpdateActivePath(context);
        }
    }
}
