using UnityEngine;
using UnityEngine.Events;

namespace Cybershi
{
    /// <summary>
    /// Конкретный сериализуемый UnityEvent с аргументом DamageInfo.
    /// (Обобщённый UnityEvent&lt;T&gt; Unity не сериализует и не создаёт — нужен такой подкласс.)
    /// </summary>
    [System.Serializable]
    public class DamageEvent : UnityEvent<DamageInfo> { }

    /// <summary>
    /// Универсальный компонент здоровья. Реализует <see cref="IDamageable"/>,
    /// поэтому подходит и игроку, и врагам, и разрушаемым объектам.
    /// Визуал/звук/эффекты вешаются через UnityEvent в инспекторе либо через C#-события.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Принадлежность")]
        public Faction faction = Faction.Enemy;

        [Header("Параметры")]
        public float maxHealth = 100f;
        [SerializeField] private bool invulnerable;

        [Header("Смерть")]
        [Tooltip("Эффект, спавнящийся в точке смерти (через пул). Можно оставить пустым.")]
        public GameObject deathEffectPrefab;
        [Tooltip("Уничтожать/прятать объект при смерти. Выключите для боссов с кастомной логикой.")]
        public bool destroyOnDeath = true;

        [Header("События (привяжите звук/эффекты в инспекторе)")]
        public DamageEvent OnDamaged = new DamageEvent();
        public UnityEvent OnHealed = new UnityEvent();
        public UnityEvent OnDeathEvent = new UnityEvent();

        /// <summary>C#-событие смерти для подписки из кода (враг сообщает трекеру боя и т.п.).</summary>
        public event System.Action<Health, DamageInfo> Died;

        private float _current;

        public Faction Faction => faction;
        public bool IsAlive => _current > 0f;
        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? _current / maxHealth : 0f;
        public bool Invulnerable { get => invulnerable; set => invulnerable = value; }

        private void Awake()
        {
            _current = maxHealth;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || invulnerable) return;

            _current -= info.Amount;
            OnDamaged?.Invoke(info);

            if (_current <= 0f)
            {
                _current = 0f;
                Die(info);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            OnHealed?.Invoke();
        }

        /// <summary>Полностью восстановить (например, при респавне/выдаче из пула).</summary>
        public void ResetHealth()
        {
            _current = maxHealth;
        }

        private void Die(DamageInfo info)
        {
            Died?.Invoke(this, info);
            OnDeathEvent?.Invoke();

            if (deathEffectPrefab != null)
                PoolManager.Spawn(deathEffectPrefab, transform.position, Quaternion.identity);

            if (!destroyOnDeath) return;

            var pooled = GetComponent<PooledInstance>();
            if (pooled != null && pooled.SourcePrefab != null)
                PoolManager.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
