using System.Collections;
using System.Collections.Generic;
using Core.Scripts.Common;
using Core.Scripts.SO;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core.Scripts.Audio
{
    /// <summary>
    /// 全局音频管理：音效按枚举从 AudioConfigSO 取片段，交给 AudioPlayer 对象池播放；
    /// 背景音乐使用独立音源做淡入淡出，场景加载完成时播放加载音效。
    /// 常驻场景承载，随常驻场景长期存在。
    /// </summary>
    public class AudioPlayerManager : YSingleton<AudioPlayerManager>
    {
        [Header("配置")] [Tooltip("按音效与背景音乐枚举查找片段，同类型的多个片段由配置表按权重选取。")] [FormerlySerializedAs("config")] [SerializeField]
        private AudioConfigSO audioConfig;

        [Tooltip("按需扩容时使用的播放器总数上限，包含正在播放与池内空闲的实例。")] [FormerlySerializedAs("maxPoolSize")] [SerializeField]
        private int maxAudioPlayerCount = 16;

        [Tooltip("启动时预先创建的音效播放器数量。预热按此数量执行，请与池容量配合配置。")] [FormerlySerializedAs("warmupCount")] [SerializeField]
        private int prewarmAudioPlayerCount = 4;

        [Header("背景音乐")]
        [Tooltip("旧音乐淡出与新音乐淡入各自的时长，单位为秒，随游戏时间缩放。")]
        [FormerlySerializedAs("crossfadeDuration")]
        [SerializeField]
        private float bgmFadeDuration = 1f;

        [Header("启动")] [Tooltip("启动时自动播放的背景音乐类型。设为默认类型时不自动播放。")] [FormerlySerializedAs("startupBGM")] [SerializeField]
        private BGMType startupBgmType = BGMType.Gaming;

        [Header("场景事件")] [Tooltip("订阅场景加载完成事件，每次收到通知时播放 LoadScene 音效。")] [SerializeField]
        private SceneLoadedEventSO sceneLoadedEvent;

        [Header("音量")] [Tooltip("音效开始播放时使用的音量。")] [Range(0f, 1f)]
        public float sfxVolume = 0.5f;

        [Tooltip("背景音乐淡入的目标音量，淡入完成后保留该音量。")] [Range(0f, 1f)]
        public float bgmVolume = 0.5f;

        // 背景音乐独占一个循环音源，切换时先淡出旧片段，再淡入新片段。
        private AudioSource bgmSource;

        // 队列保存待复用的音效播放器；总创建数还包含正在播放的实例。
        private Queue<AudioPlayer> availableAudioPlayers = new();
        private int createdAudioPlayerCount;

        // 记录最近一次接受的背景音乐类型与当前切换协程。
        private BGMType currentBgmType;
        private Coroutine bgmFadeCoroutine;

        /// <summary>在单例注册完成后创建背景音乐音源。</summary>
        protected override void OnSingletonInitialized()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
        }

        private void OnEnable()
        {
            if (sceneLoadedEvent != null)
            {
                sceneLoadedEvent.SceneLoadedEvent += OnSceneLoaded;
            }
        }

        private void Start()
        {
            Prewarm();

            // 启动时自动播放全局 BGM（AudioConfig 未配置该类型时安全跳过）
            if (startupBgmType != BGMType.Default)
            {
                PlayBGM(startupBgmType);
            }
        }

        private void OnDisable()
        {
            if (sceneLoadedEvent != null)
            {
                sceneLoadedEvent.SceneLoadedEvent -= OnSceneLoaded;
            }
        }

        /// <summary>场景加载完成时播放统一的加载音效。</summary>
        private void OnSceneLoaded(GameSceneSO currentScene)
        {
            PlaySfx(AudioType.LoadScene);
        }

        /// <summary>根据音效枚举播放</summary>
        /// <param name="audioType">配置表中的音效类型，片段由配置表选取。</param>
        /// <param name="worldPosition">传入世界坐标时在该位置播放 3D 音效，省略时播放 2D 音效。</param>
        public void PlaySfx(AudioType audioType, Vector3? worldPosition = null)
        {
            if (audioConfig == null)
                return;

            AudioClip sfxClip = audioConfig.GetSFXClip(audioType);
            if (sfxClip == null)
                return;

            AudioPlayer audioPlayer = GetPlayer();
            if (audioPlayer == null)
                return;
            audioPlayer.Play(sfxClip, sfxVolume, worldPosition: worldPosition);
        }

        /// <summary>停止子节点中的所有音效播放器，并通过播放器自身的流程归还对象池。</summary>
        public void StopAllSfx()
        {
            foreach (var audioPlayer in GetComponentsInChildren<AudioPlayer>(true))
            {
                audioPlayer.StopAndReturn();
            }
        }

        /// <summary>播放 BGM（带淡入淡出）</summary>
        /// <param name="bgmType">配置表中的背景音乐类型，当前类型正在播放时保持原曲。</param>
        public void PlayBGM(BGMType bgmType)
        {
            if (audioConfig == null)
            {
                return;
            }

            if (bgmType == currentBgmType && bgmSource.isPlaying)
            {
                return;
            }

            AudioClip bgmClip = audioConfig.GetBGMClip(bgmType);
            if (bgmClip == null)
            {
                return;
            }

            // 新请求接替正在执行的切换，从音源当前音量继续淡出。
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }

            bgmFadeCoroutine = StartCoroutine(CrossfadeBGM(bgmClip));
            currentBgmType = bgmType;
        }

        /// <summary>中止背景音乐切换并立即停止当前音乐。</summary>
        public void StopBGM()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            bgmSource.Stop();
        }

        /// <summary>使用同一音源依次完成旧片段淡出、片段替换与新片段淡入。</summary>
        private IEnumerator CrossfadeBGM(AudioClip nextBgmClip)
        {
            // Time.deltaTime 让淡入淡出随游戏时间推进，游戏暂停时也暂停计时。
            if (bgmSource.isPlaying && bgmSource.clip != null)
            {
                float fadeOutStartVolume = bgmSource.volume;
                float fadeOutElapsed = 0f;
                while (fadeOutElapsed < bgmFadeDuration)
                {
                    fadeOutElapsed += Time.deltaTime;
                    bgmSource.volume = Mathf.Lerp(fadeOutStartVolume, 0f, fadeOutElapsed / bgmFadeDuration);
                    yield return null;
                }

                bgmSource.Stop();
            }

            bgmSource.clip = nextBgmClip;
            bgmSource.volume = 0f;
            bgmSource.Play();

            float fadeInElapsed = 0f;
            while (fadeInElapsed < bgmFadeDuration)
            {
                fadeInElapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, fadeInElapsed / bgmFadeDuration);
                yield return null;
            }

            // 循环退出时明确写入目标音量，消除逐帧插值的尾差。
            bgmSource.volume = bgmVolume;
            bgmFadeCoroutine = null;
        }

        /// <summary>按预热数量创建空闲播放器，集中挂在管理器节点下。</summary>
        private void Prewarm()
        {
            for (int audioPlayerIndex = 0; audioPlayerIndex < prewarmAudioPlayerCount; audioPlayerIndex++)
            {
                var audioPlayerObject = new GameObject("AudioPlayer");
                audioPlayerObject.transform.SetParent(transform);
                var audioPlayer = audioPlayerObject.AddComponent<AudioPlayer>();
                audioPlayer.Init(this);
                audioPlayer.StopAllCoroutines();
                audioPlayerObject.SetActive(false);
                availableAudioPlayers.Enqueue(audioPlayer);
                createdAudioPlayerCount++;
            }
        }

        /// <summary>优先复用池内播放器，容量允许时创建新实例。</summary>
        /// <returns>可用播放器；池内无可用实例且容量已满时返回空，跳过本次音效。</returns>
        private AudioPlayer GetPlayer()
        {
            if (availableAudioPlayers.Count > 0)
            {
                var audioPlayer = availableAudioPlayers.Dequeue();
                if (audioPlayer != null)
                {
                    return audioPlayer;
                }
            }

            if (createdAudioPlayerCount < maxAudioPlayerCount)
            {
                var audioPlayerObject = new GameObject("AudioPlayer");
                audioPlayerObject.transform.SetParent(transform);
                var audioPlayer = audioPlayerObject.AddComponent<AudioPlayer>();
                audioPlayer.Init(this);
                createdAudioPlayerCount++;
                return audioPlayer;
            }

            return null;
        }

        /// <summary>由 AudioPlayer 在播放结束或主动停止时调用，停用实例并归还队列。</summary>
        /// <param name="audioPlayer">由当前管理器创建、需要回收的音效播放器。</param>
        public void ReturnPlayer(AudioPlayer audioPlayer)
        {
            if (audioPlayer == null || !audioPlayer.gameObject.activeSelf)
            {
                return;
            }

            // 结束播放器的等待协程后再入队，避免等待结束回调再次归还同一实例。
            audioPlayer.StopAllCoroutines();
            if (audioPlayer.gameObject != null)
            {
                audioPlayer.gameObject.SetActive(false);
                audioPlayer.transform.SetParent(transform);
            }

            availableAudioPlayers.Enqueue(audioPlayer);
        }
    }
}
