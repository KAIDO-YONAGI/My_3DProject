using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioPlayerTests
{
    private const string AudioNamespace = "Core.Scripts.GamePlayFrameWork.Scripts.Audio.";
    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private Component audioManager;
    private object audioType;

    [SetUp]
    public void SetUp()
    {
        var audioManagerObject = new GameObject("AudioManagerTests");
        objects.Add(audioManagerObject);
        audioManagerObject.transform.position = new Vector3(12f, 3f, -8f);
        audioManagerObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        audioManager = audioManagerObject.AddComponent(RuntimeType("AudioPlayerManager"));
        SetField(audioManager, "maxAudioPlayerCount", 1);
        SetField(audioManager, "prewarmAudioPlayerCount", 0);

        AudioClip audioClip = AudioClip.Create("AudioPlayerTests", 44100, 1, 44100, false);
        objects.Add(audioClip);
        ScriptableObject audioConfig = ScriptableObject.CreateInstance(RuntimeType("AudioConfigSO"));
        objects.Add(audioConfig);
        audioType = Enum.Parse(RuntimeType("AudioType"), "Walk");
        object audioItem = Activator.CreateInstance(RuntimeType("AudioItem"));
        SetField(audioItem, "clip", audioClip);
        SetField(audioItem, "weight", 1f);
        Array items = Array.CreateInstance(RuntimeType("AudioItem"), 1);
        items.SetValue(audioItem, 0);
        object entry = Activator.CreateInstance(RuntimeType("SFXEntry"));
        SetField(entry, "type", audioType);
        SetField(entry, "items", items);
        Array entries = Array.CreateInstance(RuntimeType("SFXEntry"), 1);
        entries.SetValue(entry, 0);
        SetField(audioConfig, "sfxEntries", entries);
        SetField(audioManager, "audioConfig", audioConfig);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objects.Count - 1; index >= 0; index--)
        {
            if (objects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(objects[index]);
            }
        }

        objects.Clear();
    }

    [TestCase(0f, 0f, 0f)]
    [TestCase(-4f, 2f, 9f)]
    public void PositionedPlaybackUsesWorldCoordinatesUnderManager(float x, float y, float z)
    {
        var worldPosition = new Vector3(x, y, z);

        Play(worldPosition);
        Component audioPlayer = GetAudioPlayer();

        Assert.That(audioPlayer.transform.parent, Is.EqualTo(audioManager.transform));
        Assert.That(audioPlayer.transform.position, Is.EqualTo(worldPosition).Using(Vector3Comparer));
        Assert.That(audioPlayer.GetComponent<AudioSource>().spatialBlend, Is.EqualTo(1f));
        Assert.That(audioPlayer.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void ReusedAudioPlayerReturnsTo2DWhenPositionIsOmitted()
    {
        Play(new Vector3(-4f, 2f, 9f));
        Component audioPlayer = GetAudioPlayer();
        Invoke(audioPlayer, "StopAndReturn");

        Play(Type.Missing);

        Assert.That(GetAudioPlayer(), Is.SameAs(audioPlayer));
        Assert.That(audioPlayer.GetComponent<AudioSource>().spatialBlend, Is.Zero);
        Assert.That(audioPlayer.transform.localPosition, Is.EqualTo(Vector3.zero).Using(Vector3Comparer));
        Assert.That(audioManager.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void ReusedAudioPlayerMovesToNextWorldPosition()
    {
        Play(Vector3.zero);
        Component audioPlayer = GetAudioPlayer();
        Invoke(audioPlayer, "StopAndReturn");
        var nextPosition = new Vector3(6f, 4f, -3f);

        Play(nextPosition);

        Assert.That(GetAudioPlayer(), Is.SameAs(audioPlayer));
        Assert.That(audioPlayer.transform.position, Is.EqualTo(nextPosition).Using(Vector3Comparer));
        Assert.That(audioManager.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void StoppingAllAudioRepeatedlyKeepsOneAvailableAudioPlayer()
    {
        Play(Vector3.zero);

        Invoke(audioManager, "StopAllSfx");
        Invoke(audioManager, "StopAllSfx");

        Assert.That(AvailableCount(), Is.EqualTo(1));
        Assert.That(GetAudioPlayer().gameObject.activeSelf, Is.False);
        Play(Vector3.one);
        Assert.That(AvailableCount(), Is.Zero);
        Assert.That(audioManager.transform.childCount, Is.EqualTo(1));
    }

    [Test]
    public void PlaybackCompletionReturnsAudioPlayerToManagerPool()
    {
        Play(Vector3.one);
        Component audioPlayer = GetAudioPlayer();
        AudioSource audioSource = audioPlayer.GetComponent<AudioSource>();
        audioSource.Stop();
        var completion = (IEnumerator)Invoke(audioPlayer, "WaitForEnd");

        Assert.That(completion.MoveNext(), Is.False);
        Assert.That(AvailableCount(), Is.EqualTo(1));
        Assert.That(audioPlayer.gameObject.activeSelf, Is.False);
        Assert.That(audioPlayer.transform.parent, Is.EqualTo(audioManager.transform));
    }

    private static readonly IEqualityComparer<Vector3> Vector3Comparer =
        UnityEngine.TestTools.Utils.Vector3EqualityComparer.Instance;

    private void Play(object worldPosition)
    {
        MethodInfo play = audioManager.GetType().GetMethod(
            "PlaySfx", BindingFlags.Instance | BindingFlags.Public, null,
            new[] { RuntimeType("AudioType"), typeof(Vector3?) }, null);
        Assert.That(play, Is.Not.Null, "AudioPlayerManager.PlaySfx exposes an optional world position.");
        play.Invoke(audioManager, new[] { audioType, worldPosition });
    }

    private Component GetAudioPlayer()
    {
        return audioManager.GetComponentInChildren(RuntimeType("AudioPlayer"), true);
    }

    private int AvailableCount()
    {
        var availableAudioPlayers = (ICollection)audioManager.GetType()
            .GetField("availableAudioPlayers", InstanceMembers).GetValue(audioManager);
        return availableAudioPlayers.Count;
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType(AudioNamespace + name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name);
        return type;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, InstanceMembers).SetValue(target, value);
    }

    private static object Invoke(object target, string name)
    {
        return target.GetType().GetMethod(name, InstanceMembers).Invoke(target, null);
    }
}
