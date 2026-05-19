using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed class SimpleCharacterAnimationRig : IDisposable
{
    private const int RootStateCount = 4;
    private const int RootGrounded = 0;
    private const int RootJumpStart = 1;
    private const int RootJumpLoop = 2;
    private const int RootLand = 3;

    private readonly SimpleCharacterAnimationSettings settings;
    private readonly AnimationClip idleClip;
    private readonly SimpleCharacterJumpAnimationSet jumpAnimationSet;
    private readonly bool useFullJumpClip;

    private readonly PlayableGraph graph;
    private readonly AnimationMixerPlayable rootMixer;
    private readonly AnimationMixerPlayable groundedMixer;
    private readonly AnimationMixerPlayable jumpLoopMixer;
    private readonly AnimationMixerPlayable landMixer;

    private readonly DirectionalBlendPlayable runBlendPlayable;
    private readonly DirectionalBlendPlayable sprintBlendPlayable;
    private readonly AnimationClipPlayable idlePlayable;
    private readonly AnimationClipPlayable jumpStartPlayable;
    private readonly AnimationClipPlayable jumpUpPlayable;
    private readonly AnimationClipPlayable jumpDownPlayable;
    private readonly AnimationClipPlayable landIdlePlayable;
    private readonly AnimationClipPlayable landMovePlayable;

    private int currentStateIndex = RootGrounded;
    private int targetStateIndex = RootGrounded;
    private float blendElapsed;
    private bool blendInProgress;
    private bool fullJumpSequenceActive;

    public SimpleCharacterAnimationRig(
        Animator animator,
        AnimationClip idleClip,
        DirectionalAnimationSet runDirectionalSet,
        DirectionalAnimationSet sprintDirectionalSet,
        SimpleCharacterJumpAnimationSet jumpAnimationSet,
        SimpleCharacterAnimationSettings settings)
    {
        this.idleClip = idleClip;
        this.jumpAnimationSet = jumpAnimationSet;
        this.settings = settings;
        useFullJumpClip = jumpAnimationSet != null && jumpAnimationSet.HasFullJumpClip;

        graph = PlayableGraph.Create($"{animator.name}_{nameof(SimpleCharacterAnimationRig)}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        rootMixer = AnimationMixerPlayable.Create(graph, RootStateCount);
        groundedMixer = AnimationMixerPlayable.Create(graph, 3);
        jumpLoopMixer = AnimationMixerPlayable.Create(graph, 2);
        landMixer = AnimationMixerPlayable.Create(graph, 2);

        idlePlayable = CreateClipPlayable(graph, idleClip);
        AnimationClip jumpEntryClip = useFullJumpClip ? jumpAnimationSet.FullJumpClip : jumpAnimationSet.JumpStartClip;
        jumpStartPlayable = CreateClipPlayable(graph, jumpEntryClip);
        jumpUpPlayable = CreateClipPlayable(graph, jumpAnimationSet.JumpUpLoopClip);
        jumpDownPlayable = CreateClipPlayable(graph, jumpAnimationSet.JumpDownLoopClip);
        landIdlePlayable = CreateClipPlayable(graph, jumpAnimationSet.JumpLandIdleClip);
        landMovePlayable = CreateClipPlayable(graph, jumpAnimationSet.JumpLandMoveClip);

        runBlendPlayable = new DirectionalBlendPlayable(graph, runDirectionalSet);
        sprintBlendPlayable = new DirectionalBlendPlayable(graph, sprintDirectionalSet);

        ConnectPlayable(groundedMixer, 0, idlePlayable);
        ConnectPlayable(groundedMixer, 1, runBlendPlayable.Playable);
        ConnectPlayable(groundedMixer, 2, sprintBlendPlayable.Playable);
        groundedMixer.SetInputWeight(0, 1f);
        groundedMixer.SetInputWeight(1, 0f);
        groundedMixer.SetInputWeight(2, 0f);

        ConnectPlayable(jumpLoopMixer, 0, jumpUpPlayable);
        ConnectPlayable(jumpLoopMixer, 1, jumpDownPlayable);
        jumpLoopMixer.SetInputWeight(0, jumpUpPlayable.IsValid() ? 1f : 0f);
        jumpLoopMixer.SetInputWeight(1, jumpDownPlayable.IsValid() && !jumpUpPlayable.IsValid() ? 1f : 0f);

        ConnectPlayable(landMixer, 0, landIdlePlayable);
        ConnectPlayable(landMixer, 1, landMovePlayable);
        landMixer.SetInputWeight(0, landIdlePlayable.IsValid() ? 1f : 0f);
        landMixer.SetInputWeight(1, 0f);

        ConnectPlayable(rootMixer, RootGrounded, groundedMixer);
        ConnectPlayable(rootMixer, RootJumpStart, jumpStartPlayable);
        ConnectPlayable(rootMixer, RootJumpLoop, jumpLoopMixer);
        ConnectPlayable(rootMixer, RootLand, landMixer);
        SetRootStateImmediate(RootGrounded);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "CharacterAnimation", animator);
        output.SetSourcePlayable(rootMixer);
        animator.applyRootMotion = false;
        graph.Play();
    }

    public bool HasRequiredBindings => HasGroundedBindings && jumpAnimationSet != null && jumpAnimationSet.HasAnyClip;

    public void SetImmediateState(SimpleCharacterAnimationState state)
    {
        int stateIndex = ToStateIndex(state);
        currentStateIndex = stateIndex;
        targetStateIndex = stateIndex;
        blendElapsed = 0f;
        blendInProgress = false;
        fullJumpSequenceActive = useFullJumpClip && state != SimpleCharacterAnimationState.Grounded;
        SetRootStateImmediate(stateIndex);
    }

    public float GetJumpStartExitTime()
    {
        AnimationClip clip = useFullJumpClip ? jumpAnimationSet.FullJumpClip : jumpAnimationSet.JumpStartClip;
        if (clip == null)
        {
            return 0.06f;
        }

        return clip.length * settings.JumpStartExitNormalizedTime;
    }

    public float GetLandHoldTime(bool moving)
    {
        AnimationClip landClip = jumpAnimationSet != null ? jumpAnimationSet.GetLandingClip(moving) : null;

        if (landClip == null)
        {
            return settings.LandLockTime;
        }

        return Mathf.Min(settings.LandLockTime, landClip.length);
    }

    public void Apply(SimpleCharacterMotorFrame frame, SimpleCharacterAnimationState state)
    {
        UpdateGroundedBlend(frame);
        UpdateJumpLoopBlend(frame.VerticalSpeed);
        UpdateLandBlend(frame.MoveMagnitude01);

        if (frame.JumpStarted && !blendInProgress && currentStateIndex == RootJumpStart)
        {
            RestartStatePlayables(RootJumpStart);
        }

        bool holdGroundedFullJump = false;
        if (useFullJumpClip)
        {
            if (state != SimpleCharacterAnimationState.Grounded)
            {
                fullJumpSequenceActive = true;
            }
            else if (fullJumpSequenceActive)
            {
                holdGroundedFullJump = ShouldHoldFullJumpClip();
                if (!holdGroundedFullJump)
                {
                    fullJumpSequenceActive = false;
                }
            }
        }

        int desiredStateIndex = ToStateIndex(state);
        if (holdGroundedFullJump)
        {
            desiredStateIndex = RootJumpStart;
        }

        if (desiredStateIndex != targetStateIndex || (!blendInProgress && desiredStateIndex != currentStateIndex))
        {
            BeginStateTransition(desiredStateIndex);
        }
    }

    public void EvaluateImmediate()
    {
        if (graph.IsValid())
        {
            graph.Evaluate(0f);
        }
    }

    public void Tick(float deltaTime)
    {
        if (!blendInProgress)
        {
            return;
        }

        blendElapsed += deltaTime;
        float t = Mathf.Clamp01(blendElapsed / settings.StateFadeDuration);

        for (int inputIndex = 0; inputIndex < RootStateCount; inputIndex++)
        {
            rootMixer.SetInputWeight(inputIndex, 0f);
        }

        rootMixer.SetInputWeight(currentStateIndex, 1f - t);
        rootMixer.SetInputWeight(targetStateIndex, t);

        if (t < 1f)
        {
            return;
        }

        currentStateIndex = targetStateIndex;
        blendInProgress = false;
        SetRootStateImmediate(currentStateIndex);
    }

    public void Dispose()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    private void UpdateGroundedBlend(SimpleCharacterMotorFrame frame)
    {
        float locomotionWeight = frame.MoveMagnitude01;
        bool useSprint = frame.WantsSprint && sprintBlendPlayable.HasValidSamples;
        bool useRun = !useSprint && runBlendPlayable.HasValidSamples;

        if (!useRun && !useSprint && runBlendPlayable.HasValidSamples)
        {
            useRun = true;
        }

        if (!useRun && !useSprint && sprintBlendPlayable.HasValidSamples)
        {
            useSprint = true;
        }

        bool hasLocomotion = useRun || useSprint;
        float idleWeight = idlePlayable.IsValid() ? 1f - (hasLocomotion ? locomotionWeight : 0f) : 0f;
        //idle±£»¤
        float runWeight = useRun ? locomotionWeight : 0f;
        float sprintWeight = useSprint ? locomotionWeight : 0f;

        groundedMixer.SetInputWeight(0, idleWeight);
        groundedMixer.SetInputWeight(1, runWeight);
        groundedMixer.SetInputWeight(2, sprintWeight);

        runBlendPlayable.SetDirection(frame.LocalDirection);
        sprintBlendPlayable.SetDirection(frame.LocalDirection);
    }

    private void UpdateJumpLoopBlend(float verticalSpeed)
    {
        if (!jumpUpPlayable.IsValid() && !jumpDownPlayable.IsValid())
        {
            return;
        }

        if (!jumpUpPlayable.IsValid())
        {
            jumpLoopMixer.SetInputWeight(0, 0f);
            jumpLoopMixer.SetInputWeight(1, 1f);
            return;
        }

        if (!jumpDownPlayable.IsValid())
        {
            jumpLoopMixer.SetInputWeight(0, 1f);
            jumpLoopMixer.SetInputWeight(1, 0f);
            return;
        }

        float range = settings.AirVerticalBlendRange;
        float upWeight = Mathf.InverseLerp(-range, range, verticalSpeed);
        jumpLoopMixer.SetInputWeight(0, upWeight);
        jumpLoopMixer.SetInputWeight(1, 1f - upWeight);
    }

    private void UpdateLandBlend(float moveMagnitude01)
    {
        float landMoveWeight = landMovePlayable.IsValid() ? moveMagnitude01 : 0f;
        float landIdleWeight = landIdlePlayable.IsValid() ? 1f - landMoveWeight : 0f;
        landMixer.SetInputWeight(0, landIdleWeight);
        landMixer.SetInputWeight(1, landMoveWeight);
    }

    private void BeginStateTransition(int desiredStateIndex)
    {
        if (blendInProgress)
        {
            currentStateIndex = targetStateIndex;
            blendInProgress = false;
            SetRootStateImmediate(currentStateIndex);
        }

        if (desiredStateIndex == currentStateIndex)
        {
            return;
        }

        RestartStatePlayables(desiredStateIndex);
        targetStateIndex = desiredStateIndex;
        blendElapsed = 0f;
        blendInProgress = true;
    }

    private void RestartStatePlayables(int stateIndex)
    {
        switch (stateIndex)
        {
            case RootJumpStart:
                RestartPlayable(jumpStartPlayable);
                break;

            case RootLand:
                RestartPlayable(landIdlePlayable);
                RestartPlayable(landMovePlayable);
                break;
        }
    }

    private void SetRootStateImmediate(int stateIndex)
    {
        for (int inputIndex = 0; inputIndex < RootStateCount; inputIndex++)
        {
            rootMixer.SetInputWeight(inputIndex, inputIndex == stateIndex ? 1f : 0f);
        }
    }

    private int ToStateIndex(SimpleCharacterAnimationState state)
    {
        if (useFullJumpClip && state != SimpleCharacterAnimationState.Grounded)
        {
            return RootJumpStart;
        }

        switch (state)
        {
            case SimpleCharacterAnimationState.JumpStart:
                return RootJumpStart;
            case SimpleCharacterAnimationState.JumpLoop:
                return RootJumpLoop;
            case SimpleCharacterAnimationState.Land:
                return RootLand;
            default:
                return RootGrounded;
        }
    }

    private bool ShouldHoldFullJumpClip()
    {
        if (!useFullJumpClip || !jumpStartPlayable.IsValid())
        {
            return false;
        }

        AnimationClip clip = jumpAnimationSet.FullJumpClip;
        if (clip == null || clip.length <= 0.0001f)
        {
            return false;
        }

        double normalizedTime = jumpStartPlayable.GetTime() / clip.length;
        return normalizedTime < settings.FullJumpGroundedExitNormalizedTime;
    }

    private bool HasGroundedBindings =>
        idleClip != null || runBlendPlayable.HasValidSamples || sprintBlendPlayable.HasValidSamples;

    private static AnimationClipPlayable CreateClipPlayable(PlayableGraph graph, AnimationClip clip)
    {
        if (clip == null)
        {
            return default;
        }

        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetTime(0d);
        playable.SetSpeed(1d);
        return playable;
    }

    private static void RestartPlayable(AnimationClipPlayable playable)
    {
        if (!playable.IsValid())
        {
            return;
        }

        playable.SetTime(0d);
        playable.SetSpeed(1d);
    }

    private static void ConnectPlayable(AnimationMixerPlayable mixer, int inputIndex, Playable playable)
    {
        if (!playable.IsValid())
        {
            return;
        }

        mixer.ConnectInput(inputIndex, playable, 0);
    }

    private sealed class DirectionalBlendPlayable
    {
        private readonly AnimationMixerPlayable mixer;
        private readonly List<SampleData> samples = new List<SampleData>();
        private readonly float[] weights;

        private struct SampleData
        {
            public Vector2 position;
            public AnimationClipPlayable playable;
        }

        public DirectionalBlendPlayable(PlayableGraph graph, DirectionalAnimationSet set)
        {
            int inputCount = Mathf.Max(1, set != null ? set.ValidSampleCount : 0);
            mixer = AnimationMixerPlayable.Create(graph, inputCount);

            if (set == null)
            {
                weights = Array.Empty<float>();
                return;
            }

            IReadOnlyList<DirectionalAnimationSample> sourceSamples = set.Samples;
            int mixerIndex = 0;
            for (int sampleIndex = 0; sampleIndex < sourceSamples.Count; sampleIndex++)
            {
                DirectionalAnimationSample sample = sourceSamples[sampleIndex];
                if (sample == null || !sample.IsValid)
                {
                    continue;
                }

                AnimationClipPlayable clipPlayable = CreateClipPlayable(graph, sample.Clip);
                mixer.ConnectInput(mixerIndex, clipPlayable, 0);
                mixer.SetInputWeight(mixerIndex, 0f);

                samples.Add(new SampleData
                {
                    position = sample.Position,
                    playable = clipPlayable,
                });

                mixerIndex++;
            }

            weights = new float[samples.Count];
        }

        public Playable Playable => mixer;
        public bool HasValidSamples => samples.Count > 0;

        public void SetDirection(Vector2 localDirection)
        {
            if (samples.Count == 0)
            {
                return;
            }

            Vector2 blendPosition = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector2.up;
            float totalWeight = 0f;

            for (int index = 0; index < samples.Count; index++)
            {
                float distance = Vector2.Distance(blendPosition, samples[index].position);
                if (distance <= 0.0001f)
                {
                    for (int clearIndex = 0; clearIndex < samples.Count; clearIndex++)
                    {
                        mixer.SetInputWeight(clearIndex, clearIndex == index ? 1f : 0f);
                    }

                    return;
                }

                float weight = 1f / (distance * distance);
                weights[index] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return;
            }

            for (int index = 0; index < samples.Count; index++)
            {
                mixer.SetInputWeight(index, weights[index] / totalWeight);
            }
        }
    }
}
