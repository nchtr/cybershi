using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Менеджер звуковых эффектов. Берёт клипы из <see cref="SoundLibrary"/> по <see cref="SoundId"/>
    /// и проигрывает их через небольшой пул AudioSource (чтобы звуки не обрывали друг друга).
    ///
    /// Вызовы из кода:
    ///   AudioManager.Instance.Play(SoundId.PlayerDash);                 // 2D
    ///   AudioManager.Instance.Play(SoundId.EnemyShoot, transform.position); // 3D в точке
    /// Если клип для id не назначен — просто тишина (ошибки нет), можно настраивать звук постепенно.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("Назначьте ассет SoundLibrary со своими клипами.")]
        public SoundLibrary library;

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Tooltip("Сколько одновременных звуков может играть.")]
        public int voiceCount = 16;

        private readonly List<AudioSource> _voices = new();
        private int _next;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < Mathf.Max(1, voiceCount); i++)
            {
                var go = new GameObject($"Voice_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; // по умолчанию 2D, для позиционных звуков ставим 1 на лету
                _voices.Add(src);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource NextVoice()
        {
            // Round-robin: предпочитаем свободный источник, иначе перехватываем самый старый.
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[(_next + i) % _voices.Count];
                if (!v.isPlaying)
                {
                    _next = (_next + i + 1) % _voices.Count;
                    return v;
                }
            }
            var fallback = _voices[_next];
            _next = (_next + 1) % _voices.Count;
            return fallback;
        }

        /// <summary>Сыграть звук по идентификатору (2D, без позиции).</summary>
        public void Play(SoundId id)
        {
            PlayAt(id, Vector3.zero, false);
        }

        /// <summary>Сыграть звук по идентификатору в точке мира (3D).</summary>
        public void Play(SoundId id, Vector3 position)
        {
            PlayAt(id, position, true);
        }

        private void PlayAt(SoundId id, Vector3 position, bool spatial)
        {
            if (library == null || id == SoundId.None) return;
            var entry = library.Get(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0) return;

            var clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null) return;

            var v = NextVoice();
            v.transform.position = position;
            v.spatialBlend = spatial ? 1f : 0f;
            v.pitch = 1f + Random.Range(-entry.pitchRandom, entry.pitchRandom);
            v.volume = entry.volume * masterVolume;
            v.PlayOneShot(clip);
        }

        /// <summary>Сыграть конкретный клип в точке (используется эффектами).</summary>
        public void PlayClipAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            var v = NextVoice();
            v.transform.position = position;
            v.spatialBlend = 1f;
            v.pitch = 1f;
            v.volume = volume * masterVolume;
            v.PlayOneShot(clip);
        }
    }
}
