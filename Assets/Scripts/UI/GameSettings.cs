using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Глобальные настройки игры с сохранением в PlayerPrefs. Громкости применяются к
    /// <see cref="AudioManager"/> (SFX), <see cref="DynamicMusicManager"/> (музыка) и общему
    /// <see cref="AudioListener"/> (мастер). Меню и пауза читают/пишут отсюда.
    /// </summary>
    public static class GameSettings
    {
        private const string KMaster = "cyb_master";
        private const string KMusic = "cyb_music";
        private const string KSfx = "cyb_sfx";
        private const string KFullscreen = "cyb_fullscreen";

        public static float Master { get; private set; } = 0.9f;
        public static float Music { get; private set; } = 0.7f;
        public static float Sfx { get; private set; } = 1f;
        public static bool Fullscreen { get; private set; }

        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            Master = PlayerPrefs.GetFloat(KMaster, 0.9f);
            Music = PlayerPrefs.GetFloat(KMusic, 0.7f);
            Sfx = PlayerPrefs.GetFloat(KSfx, 1f);
            Fullscreen = PlayerPrefs.GetInt(KFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            _loaded = true;
            Apply();
        }

        public static void SetMaster(float v) { Master = Mathf.Clamp01(v); Save(); Apply(); }
        public static void SetMusic(float v) { Music = Mathf.Clamp01(v); Save(); Apply(); }
        public static void SetSfx(float v) { Sfx = Mathf.Clamp01(v); Save(); Apply(); }

        public static void SetFullscreen(bool v)
        {
            Fullscreen = v;
            Save();
            Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.fullScreen = v;
        }

        /// <summary>Применить текущие значения к живым менеджерам/листенеру.</summary>
        public static void Apply()
        {
            AudioListener.volume = Master;
            if (AudioManager.Instance != null) AudioManager.Instance.masterVolume = Sfx;
            if (DynamicMusicManager.Instance != null) DynamicMusicManager.Instance.masterVolume = Music;
        }

        private static void Save()
        {
            PlayerPrefs.SetFloat(KMaster, Master);
            PlayerPrefs.SetFloat(KMusic, Music);
            PlayerPrefs.SetFloat(KSfx, Sfx);
            PlayerPrefs.SetInt(KFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
