using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Одноразовый эффект (вспышка выстрела, искра попадания, взрыв смерти, волна граунд-слэма).
    /// Живёт <see cref="lifetime"/> секунд, затем возвращается в пул.
    /// Как плейсхолдер умеет анимировать SpriteRenderer: рост масштаба + затухание альфы.
    ///
    /// Замена на настоящие эффекты:
    ///   • киньте сюда ParticleSystem (он будет проигран при спавне), или
    ///   • назначьте SpriteAnimation в соседний SpriteAnimator, или
    ///   • просто поставьте свой префаб эффекта в нужное поле (muzzle/hit/death).
    /// </summary>
    public class OneShotEffect : MonoBehaviour, IPoolable
    {
        [Tooltip("Сколько живёт эффект до возврата в пул.")]
        public float lifetime = 0.4f;

        [Header("Плейсхолдерная анимация спрайта (необязательно)")]
        public bool animatePlaceholder = true;
        public float startScale = 0.3f;
        public float endScale = 1.4f;
        public bool fadeOut = true;

        [Header("Опциональные настоящие эффекты")]
        public ParticleSystem particles;
        public AudioClip soundClip;   // если задан — проиграется через AudioManager в точке спавна
        [Range(0f, 1f)] public float soundVolume = 1f;

        private float _timer;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private Vector3 _baseScale;

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
            _baseScale = transform.localScale;
        }

        public void OnSpawned()
        {
            _timer = 0f;
            if (particles != null) { particles.Clear(); particles.Play(); }
            if (soundClip != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayClipAt(soundClip, transform.position, soundVolume);
            if (animatePlaceholder && _sr != null)
            {
                _sr.color = _baseColor;
                transform.localScale = _baseScale * startScale;
            }
        }

        public void OnDespawned() { }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = lifetime > 0f ? Mathf.Clamp01(_timer / lifetime) : 1f;

            if (animatePlaceholder && _sr != null)
            {
                float scale = Mathf.Lerp(startScale, endScale, t);
                transform.localScale = _baseScale * scale;
                if (fadeOut)
                {
                    var c = _baseColor;
                    c.a = _baseColor.a * (1f - t);
                    _sr.color = c;
                }
            }

            if (_timer >= lifetime)
                PoolManager.Despawn(gameObject);
        }
    }
}
