using UnityEngine;

namespace Core.Scripts.GamePlayFrameWork.Scripts.Audio
{
    /// <summary>
    /// 单个音效播放器：由 AudioPlayerManager 池化复用，播完后自行归还。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        private AudioSource audioSource;
        private AudioPlayerManager _audioPlayerManager;

        /// <summary>绑定所属管理器并初始化池内音源。</summary>
        public void Init(AudioPlayerManager playerManager)
        {
            _audioPlayerManager = playerManager;
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            gameObject.SetActive(false);
        }

        /// <summary>在指定世界坐标播放音效，省略坐标时使用 2D 播放。</summary>
        public void Play(AudioClip clip, float volume, float pitch = 1f, Vector3? worldPosition = null)
        {
            // 池中实例可交替用于 2D 与 3D 音效，每次播放都重置空间状态。
            transform.position = worldPosition ?? _audioPlayerManager.transform.position;
            audioSource.spatialBlend = worldPosition.HasValue ? 1f : 0f;
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.pitch = pitch;
            gameObject.SetActive(true);
            audioSource.Play();
            StartCoroutine(WaitForEnd());
        }

        /// <summary>等待音源播放结束，再由管理器停用并回收实例。</summary>
        private System.Collections.IEnumerator WaitForEnd()
        {
            while (audioSource.isPlaying)
                yield return null;
            _audioPlayerManager.ReturnPlayer(this);
        }

        /// <summary>立即停止音效并归还对象池。</summary>
        public void StopAndReturn()
        {
            StopAllCoroutines();
            if (audioSource != null) audioSource.Stop();
            _audioPlayerManager.ReturnPlayer(this);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
