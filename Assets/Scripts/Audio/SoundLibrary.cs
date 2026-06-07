using System;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Идентификаторы звуковых событий игры. Код просит "сыграй SoundId.PlayerDash",
    /// а какой именно AudioClip прозвучит — настраивается в <see cref="SoundLibrary"/>.
    /// Так звуки можно подменять без правки кода.
    /// </summary>
    public enum SoundId
    {
        None = 0,
        PlayerShootPistol,
        PlayerShootShotgun,
        PlayerShootSMG,
        PlayerJump,
        PlayerWallJump,
        PlayerDash,
        PlayerSlide,
        PlayerGroundSlam,
        PlayerLand,
        PlayerHurt,
        WeaponSwitch,
        EnemyShoot,
        EnemyHurt,
        EnemyDeath,
        Impact
    }

    /// <summary>
    /// Библиотека звуков: таблица SoundId → клипы. Создаётся как ассет:
    /// Create → Cybershi → Sound Library. Привяжите её к <see cref="AudioManager"/>.
    ///
    /// Ничего не синтезируется — вы сами кладёте свои AudioClip в нужные строки.
    /// Если для SoundId несколько клипов — берётся случайный (приятная вариативность).
    /// </summary>
    [CreateAssetMenu(menuName = "Cybershi/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundId id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Случайный разброс высоты тона ±value, чтобы выстрелы не звучали одинаково.")]
            [Range(0f, 0.5f)] public float pitchRandom = 0.05f;
        }

        public Entry[] entries;

        public Entry Get(SoundId id)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].id == id)
                    return entries[i];
            return null;
        }
    }
}
