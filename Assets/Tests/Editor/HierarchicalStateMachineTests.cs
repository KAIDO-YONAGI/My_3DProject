using System;
using System.Collections.Generic;
using Core.My_FrameWork.Scripts.StateMachine;
using NUnit.Framework;

public sealed class HierarchicalStateMachineTests
{
    [Test]
    public void StartEntersDefaultSubtreeFromTopDown()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);

        machine.Start("start");

        Assert.That(tree.Log, Is.EqualTo(new[] { "enter:Root", "enter:Left", "enter:LeftA" }));
        Assert.That(machine.Started, Is.True);
        Assert.That(machine.Leaf, Is.SameAs(tree.LeftA));
    }

    [Test]
    public void SiblingSwitchInsideSubtreeKeepsCommonAncestor()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);
        machine.Start("start");
        tree.Log.Clear();

        tree.Left.Transition = context => context == "next" ? tree.LeftB : null;
        machine.Update("next");

        // Left 是最近公共祖先，既不被退出也不被重新进入，只切换差异部分。
        Assert.That(tree.Log, Is.EqualTo(new[]
        {
            "exit:LeftA",
            "enter:LeftB",
            "update:Root",
            "update:Left",
            "update:LeftB"
        }));
        Assert.That(machine.Leaf, Is.SameAs(tree.LeftB));
    }

    [Test]
    public void ParentInitiatedTransitionExitsActiveChildBeforeLeaving()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);
        machine.Start("start");
        tree.Log.Clear();

        // 根状态直接给出另一棵子树的目标：当前叶子及其父状态都必须先收到退出。
        tree.Root.Transition = context => context == "right" ? tree.RightA : null;
        machine.Update("right");

        Assert.That(tree.Log, Is.EqualTo(new[]
        {
            "exit:LeftA",
            "exit:Left",
            "enter:Right",
            "enter:RightA",
            "update:Root",
            "update:Right",
            "update:RightA"
        }));
    }

    [Test]
    public void TransitionCompletesWithinTheSameFrame()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);
        machine.Start("start");
        tree.Log.Clear();

        tree.Root.Transition = context => context == "right" ? tree.RightB : null;
        machine.Update("right");

        // 同一次更新内已经推进到新叶子，不出现既不退出也不更新的空转帧。
        Assert.That(tree.Log[tree.Log.Count - 1], Is.EqualTo("update:RightB"));
        Assert.That(machine.Leaf, Is.SameAs(tree.RightB));
    }

    [Test]
    public void TargetingCurrentLeafDoesNotRetriggerHooks()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);
        machine.Start("start");
        tree.Log.Clear();

        tree.Left.Transition = context => tree.LeftA;
        machine.Update("stay");

        Assert.That(tree.Log, Is.EqualTo(new[] { "update:Root", "update:Left", "update:LeftA" }));
    }

    [Test]
    public void ReturningToAncestorRebuildsDefaultBranch()
    {
        Tree tree = BuildTree();
        var machine = new HierarchicalStateMachine<string>(tree.Root);
        machine.Start("start");

        // 先切到兄弟叶子，再让父状态把目标指回默认叶子。
        tree.Left.Transition = context => context == "next" ? tree.LeftB : tree.LeftA;
        machine.Update("next");
        tree.Log.Clear();

        machine.Update("back");

        Assert.That(tree.Log, Is.EqualTo(new[]
        {
            "exit:LeftB",
            "enter:LeftA",
            "update:Root",
            "update:Left",
            "update:LeftA"
        }));
        Assert.That(machine.Leaf, Is.SameAs(tree.LeftA));
    }

    private sealed class Tree
    {
        public List<string> Log { get; } = new List<string>();

        public ProbeState Root { get; set; }

        public ProbeState Left { get; set; }

        public ProbeState Right { get; set; }

        public ProbeState LeftA { get; set; }

        public ProbeState LeftB { get; set; }

        public ProbeState RightA { get; set; }

        public ProbeState RightB { get; set; }
    }

    // Root 下分 Left、Right 两棵子树，各自带两个叶子，用于观察跨子树与子树内的转换差异。
    private static Tree BuildTree()
    {
        var tree = new Tree();
        tree.Root = new ProbeState("Root", tree.Log);
        tree.Left = new ProbeState("Left", tree.Log, tree.Root);
        tree.Right = new ProbeState("Right", tree.Log, tree.Root);
        tree.LeftA = new ProbeState("LeftA", tree.Log, tree.Left);
        tree.LeftB = new ProbeState("LeftB", tree.Log, tree.Left);
        tree.RightA = new ProbeState("RightA", tree.Log, tree.Right);
        tree.RightB = new ProbeState("RightB", tree.Log, tree.Right);

        tree.Root.InitialChildState = tree.Left;
        tree.Left.InitialChildState = tree.LeftA;
        tree.Right.InitialChildState = tree.RightA;
        return tree;
    }

    private sealed class ProbeState : HierarchicalState<string>
    {
        private readonly string name;
        private readonly List<string> log;

        public ProbeState(string name, List<string> log, HierarchicalState<string> parent = null)
            : base(parent)
        {
            this.name = name;
            this.log = log;
        }

        public HierarchicalState<string> InitialChildState { get; set; }

        public Func<string, HierarchicalState<string>> Transition { get; set; }

        protected override HierarchicalState<string> InitialChild => InitialChildState;

        protected override HierarchicalState<string> ResolveTransition(string context)
        {
            return Transition?.Invoke(context);
        }

        protected override void OnEnter(string context)
        {
            log.Add($"enter:{name}");
        }

        protected override void OnExit(string context)
        {
            log.Add($"exit:{name}");
        }

        protected override void OnUpdate(string context)
        {
            log.Add($"update:{name}");
        }
    }
}
