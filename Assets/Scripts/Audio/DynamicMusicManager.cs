using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Динамическая музыка. Держит два зацикленных трека — "исследование" и "бой" —
    /// проигрывает их одновременно и синхронно, но перекрёстно меняет громкость
    /// в зависимости от <see cref="CombatStateTracker.InCombat"/>.
    /// Получается мгновенный, но плавный переход без рассинхрона ритма.
    ///
    /// Ничего не синтезируется: вы кладёте свои AudioClip в поля explorationTrack/combatTrack.
    /// Поля можно оставить пустыми — тогда просто ничего не играет.
    /// </summary>
    public class DynamicMusicManager : MonoBehaviour
    {
        public static DynamicMusicManager Instance { get; private set; }

        [Header("Треки (привяжите свои клипы)")]
        public AudioClip explorationTrack;
        public AudioClip combatTrack;

        [Header("Микс")]
        [Range(0f, 1f)] public float masterVolume = 0.7f;
        [Tooltip("Скорость кроссфейда (единиц громкости в секунду).")]
        public float fadeSpeed = 1.5f;
        [Tooltip("Старая громкость трека вне его состояния (0 = полностью затихает).")]
        [Range(0f, 1f)] public float idleLevel = 0f;

        private AudioSource _exploration;
        private AudioSource _combat;
        private bool _inCombat;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _exploration = CreateSource("Music_Exploration", explorationTrack);
            _combat = CreateSource("Music_Combat", combatTrack);
        }

        private AudioSource CreateSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private void Start()
        {
            // Запускаем оба трека синхронно; слышен будет только нужный.
            if (_exploration.clip != null) _exploration.Play();
            if (_combat.clip != null) _combat.Play();

            if (CombatStateTracker.Instance != null)
            {
                CombatStateTracker.Instance.CombatStateChanged += OnCombatStateChanged;
                _inCombat = CombatStateTracker.Instance.InCombat;
            }
        }

        private void OnDestroy()
        {
            if (CombatStateTracker.Instance != null)
                CombatStateTracker.Instance.CombatStateChanged -= OnCombatStateChanged;
            if (Instance == this) Instance = null;
        }

        private void OnCombatStateChanged(bool inCombat) => _inCombat = inCombat;

        private void Update()
        {
            float targetExploration = (_inCombat ? idleLevel : 1f) * masterVolume;
            float targetCombat = (_inCombat ? 1f : idleLevel) * masterVolume;

            float step = fadeSpeed * Time.unscaledDeltaTime;
            _exploration.volume = Mathf.MoveTowards(_exploration.volume, targetExploration, step);
            _combat.volume = Mathf.MoveTowards(_combat.volume, targetCombat, step);
        }
    }
}
