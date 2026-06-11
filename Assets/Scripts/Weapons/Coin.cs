using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Монетка (альт-огонь револьвера). Подбрасывается по дуге; хитскан-выстрел, попавший в неё,
    /// РИКОШЕТИТ с автонаводкой на приоритетную цель и доп. уроном.
    ///
    /// Цепочки: если в радиусе <see cref="chainRadius"/> висит другая монетка — рикошет
    /// перепрыгивает на неё, и множители урона ПЕРЕМНОЖАЮТСЯ за каждое звено цепи.
    ///
    /// Созревание: монетка, провисевшая в воздухе ≥ <see cref="splitAge"/> сек, «созревает»
    /// (слегка увеличивается). Если в цепи была хотя бы одна созревшая монетка — финальный
    /// рикошет РАЗДЕЛЯЕТСЯ на несколько лучей по разным целям (<see cref="splitTargets"/>),
    /// каждый с долей накопленного урона.
    ///
    /// Нужен маленький SphereCollider (isTrigger): обычные снаряды её игнорируют,
    /// хитскан ищет триггеры специально. Оркестрация цепи — в WeaponController.RicochetChain.
    /// </summary>
    public class Coin : MonoBehaviour, IPoolable
    {
        /// <summary>Все активные монетки (для поиска звеньев цепи).</summary>
        public static readonly List<Coin> Active = new();

        [Header("Рикошет")]
        [Tooltip("Множитель урона за это звено цепи (перемножается по цепочке).")]
        public float ricochetDamageMult = 1.5f;
        [Tooltip("Радиус поиска приоритетной цели от монетки.")]
        public float ricochetSearchRadius = 30f;

        [Header("Цепочка")]
        [Tooltip("Радиус, в котором рикошет перепрыгивает на следующую монетку.")]
        public float chainRadius = 18f;

        [Header("Созревание (сплит)")]
        [Tooltip("Сколько секунд монетка должна провисеть, чтобы «созреть».")]
        public float splitAge = 1.0f;
        [Tooltip("На сколько целей делится выстрел, если в цепи была созревшая монетка.")]
        public int splitTargets = 3;
        [Tooltip("Доля накопленного урона на каждый луч сплита.")]
        [Range(0.2f, 1f)] public float splitDamageFactor = 0.65f;
        [Tooltip("Масштаб визуала созревшей монетки (подсказка игроку).")]
        public float ripeScale = 1.35f;

        [Header("Полёт")]
        public float gravity = 18f;
        public float lifeTime = 3.5f;

        private Vector3 _velocity;
        private float _age;
        private bool _used;
        private Transform _visual;
        private Vector3 _visualBaseScale;
        private bool _ripeShown;

        /// <summary>Монетка «созрела» — рикошет через неё разделит выстрел.</summary>
        public bool IsRipe => _age >= splitAge;

        private void Awake()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                _visual = sr.transform;
                _visualBaseScale = _visual.localScale;
            }
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public void Toss(Vector3 velocity)
        {
            _velocity = velocity;
        }

        public void OnSpawned()
        {
            _age = 0f;
            _used = false;
            _ripeShown = false;
            if (_visual != null) _visual.localScale = _visualBaseScale;
        }

        public void OnDespawned() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            _velocity += Vector3.down * (gravity * dt);
            transform.position += _velocity * dt;

            // Визуальная подсказка созревания.
            if (!_ripeShown && IsRipe && _visual != null)
            {
                _ripeShown = true;
                _visual.localScale = _visualBaseScale * ripeScale;
            }
        }

        /// <summary>Израсходовать монетку (вызывается цепочкой рикошета).</summary>
        public void Consume()
        {
            if (_used) return;
            _used = true;
            PoolManager.Despawn(gameObject);
        }

        /// <summary>
        /// Ближайшая другая активная монетка в радиусе maxRadius от точки (или null).
        /// Использованные уже выведены из <see cref="Active"/> (деактивация при Consume).
        /// </summary>
        public static Coin FindNearest(Vector3 from, float maxRadius)
        {
            Coin best = null;
            float bestD = maxRadius * maxRadius;
            for (int i = 0; i < Active.Count; i++)
            {
                var c = Active[i];
                if (c == null || c._used) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d > 0.01f && d < bestD)
                {
                    best = c;
                    bestD = d;
                }
            }
            return best;
        }
    }
}
