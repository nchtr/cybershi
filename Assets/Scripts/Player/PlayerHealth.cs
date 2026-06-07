using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Тонкая надстройка над <see cref="Health"/> для игрока: проигрывает звук урона,
    /// мигает спрайтом при попадании и перезапускает уровень при смерти.
    /// Вешать рядом с Health (faction = Player).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealth : MonoBehaviour
    {
        public SpriteRenderer flashRenderer;
        public Color flashColor = Color.red;
        public float flashDuration = 0.12f;
        [Tooltip("Перезагрузить активную сцену при смерти игрока.")]
        public bool reloadSceneOnDeath = true;
        public float reloadDelay = 1.5f;

        private Health _health;
        private Color _baseColor;
        private float _flashTimer;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (flashRenderer != null) _baseColor = flashRenderer.color;
        }

        private void OnEnable()
        {
            _health.OnDamaged.AddListener(HandleDamaged);
            _health.Died += HandleDied;
        }

        private void OnDisable()
        {
            _health.OnDamaged.RemoveListener(HandleDamaged);
            _health.Died -= HandleDied;
        }

        private void HandleDamaged(DamageInfo info)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.PlayerHurt, transform.position);
            if (flashRenderer != null)
            {
                flashRenderer.color = flashColor;
                _flashTimer = flashDuration;
            }
        }

        private void HandleDied(Health h, DamageInfo info)
        {
            if (reloadSceneOnDeath)
                Invoke(nameof(ReloadScene), reloadDelay);
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && flashRenderer != null)
                    flashRenderer.color = _baseColor;
            }
        }

        private void ReloadScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
