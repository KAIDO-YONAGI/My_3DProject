using UnityEngine;

namespace Core.Scripts.Audio
{
    /// <summary>保存一个音频片段及其在同类片段中的随机选取权重。</summary>
    [System.Serializable]
    public struct AudioItem
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float weight;
    }

    /// <summary>按音效类型组织可随机播放的音频片段。</summary>
    [System.Serializable]
    public class SFXEntry
    {
        public AudioType type;
        public AudioItem[] items;
    }

    /// <summary>按背景音乐类型组织可随机播放的音频片段。</summary>
    [System.Serializable]
    public class BGMEntry
    {
        public BGMType type;
        public AudioItem[] items;
    }

    /// <summary>
    /// 音频配置表：音效与背景音乐按枚举登记，同类型多片段按权重随机取一条。
    /// 权重全为零时取第一条片段。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/AudioConfig", order = 0)]
    public class AudioConfigSO : ScriptableObject
    {
        public SFXEntry[] sfxEntries;
        public BGMEntry[] bgmEntries;

        /// <summary>按音效类型取一条片段，未登记该类型时返回空</summary>
        public AudioClip GetSFXClip(AudioType type)
        {
            foreach (var entry in sfxEntries)
            {
                if (entry.type == type && entry.items != null && entry.items.Length > 0)
                    return PickWeighted(entry.items);
            }
            return null;
        }

        /// <summary>按背景音乐类型取一条片段，未登记该类型时返回空</summary>
        public AudioClip GetBGMClip(BGMType type)
        {
            foreach (var entry in bgmEntries)
            {
                if (entry.type == type && entry.items != null && entry.items.Length > 0)
                    return PickWeighted(entry.items);
            }
            return null;
        }

        private AudioClip PickWeighted(AudioItem[] items)
        {
            float total = 0f;
            foreach (var item in items) total += item.weight;
            float r = Random.Range(0f, total);
            foreach (var item in items)
            {
                r -= item.weight;
                if (r <= 0f) return item.clip;
            }
            return items[items.Length - 1].clip;
        }
    }
}
