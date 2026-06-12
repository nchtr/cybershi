using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Снаряд. Движется без Rigidbody — каждый кадр Raycast по пути (быстрые пули не
    /// протыкают стены, и пули не сталкиваются между собой). Всегда через <see cref="PoolManager"/>.
    ///
    /// Новое:
    ///  • статический реестр <see cref="Active"/> — по нему работают грейз, парирование и магнит;
    ///  • <see cref="Parryable"/> — подсвеченный снаряд можно отбить (см. <see cref="Parry"/>):
    ///    он переходит игроку, получает огромный урон и самонаводится на приоритетную цель;
    ///  • <see cref="attractable"/> — гвозди притягиваются к <see cref="NailMagnet"/>;
    ///  • урон считается В МОМЕНТ ПОПАДАНИЯ с учётом дистанционного модификатора оружия
    ///    (<see cref="sourceWeapon"/>): дробовик силён в упор, слаб издали и т.п.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        /// <summary>Все активные снаряды в сцене (для грейза/парирования/магнита).</summary>
        public static readonly List<Projectile> Active = new();

        [Header("Параметры (могут перезаписываться оружием/паттерном)")]
        public float speed = 24f;
        public float damage = 10f;
        public float lifeTime = 4f;
        public float knockback = 0f;
        [Tooltip("Дуга: множитель гравитации (0 — летит прямо).")]
        public float gravityScale = 0f;
        [Tooltip("Поворачивать спрайт по направлению полёта.")]
        public bool orientToVelocity = true;

        [Header("Парирование")]
        [Tooltip("Множитель урона спарированного снаряда (огромный — награда за риск).")]
        public float parryDamageMult = 10f;
        public float parrySpeedMult = 1.6f;
        [Tooltip("Скорость доворота самонаведения после парирования, град/с.")]
        public float parryHomingTurnSpeed = 540f;
        public Color parryableTint = new Color(0.4f, 1f, 1f);
        public Color parriedTint = new Color(1f, 1f, 0.5f);

        [Header("Магнит (для гвоздей)")]
        [Tooltip("Снаряд притягивается к активным NailMagnet.")]
        public bool attractable = false;
        public float attractTurnSpeed = 480f;

        [Header("Взрыв при попадании (ракеты)")]
        [Tooltip("Взрываться при ударе: AoE-урон всем вокруг, ВКЛЮЧАЯ игрока (рокет-джамп!).")]
        public bool explodeOnImpact = false;
        public float explosionRadius = 3.5f;
        [Tooltip("Доля урона снаряда, идущая в AoE.")]
        public float explosionDamageFactor = 0.8f;
        [Tooltip("Доля AoE-урона, который получает сам игрок (рокет-джамп без суицида).")]
        [Range(0f, 1f)] public float playerSelfDamageFactor = 0.4f;
        public float explosionKnockback = 16f;
        public GameObject explosionEffectPrefab;

        [Header("Эффекты")]
        public GameObject hitEffectPrefab;

        public LayerMask collisionMask = ~0;

        // --- рантайм ---
        [System.NonSerialized] public WeaponDefinition sourceWeapon; // для falloff и DamageInfo
        public bool Parryable { get; private set; }
        public bool Grazed { get; set; }                 // уже засчитан грейзом
        public Faction OwnerFaction => _ownerFaction;

        private Vector3 _velocity;
        private float _age;
        private Faction _ownerFaction;
        private GameObject _owner;
        private Vector3 _launchOrigin;
        private bool _parried;
        private Health _homingTarget;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Запустить снаряд. Вызывается оружием/эмиттером сразу после Spawn.</summary>
        public void Launch(Vector3 direction, Faction ownerFaction, GameObject owner, float? speedOverride = null, float? damageOverride = null)
        {
            _ownerFaction = ownerFaction;
            _owner = owner;
            _age = 0f;
            _launchOrigin = transform.position;
            float spd = speedOverride ?? speed;
            if (damageOverride.HasValue) damage = damageOverride.Value;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            _velocity = dir * spd;
            Orient(dir);
        }

        /// <summary>Пометить снаряд парируемым (подсветка другим цветом).</summary>
        public void SetParryable(bool value)
        {
            Parryable = value;
            if (_sr != null) _sr.color = value ? parryableTint : _baseColor;
        }

        /// <summary>
        /// Парировать: снаряд переходит новому владельцу (игроку), урон умножается,
        /// включается самонаведение на цель (или отражение по fallbackDir).
        /// </summary>
        public void Parry(GameObject newOwner, Health target, Vector3 fallbackDir)
        {
            if (_parried) return;
            _parried = true;
            Parryable = false;
            _ownerFaction = Faction.Player;
            _owner = newOwner;
            damage *= parryDamageMult;
            _homingTarget = target;
            _launchOrigin = transform.position;

            Vector3 dir = target != null
                ? (target.transform.position - transform.position).normalized
                : (fallbackDir.sqrMagnitude > 0.001f ? fallbackDir.normalized : -_velocity.normalized);
            _velocity = dir * (_velocity.magnitude * parrySpeedMult);
            Orient(dir);
            if (_sr != null) _sr.color = parriedTint;
        }

        public void OnSpawned()
        {
            _age = 0f;
            _parried = false;
            Parryable = false;
            Grazed = false;
            _homingTarget = null;
            sourceWeapon = null;
            if (_sr != null) _sr.color = _baseColor;
        }

        public void OnDespawned() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            if (gravityScale != 0f)
                _velocity += Physics.gravity * (gravityScale * dt);

            // Самонаведение после парирования.
            if (_parried && _homingTarget != null && _homingTarget.IsAlive)
                Steer(_homingTarget.transform.position, parryHomingTurnSpeed, dt);

            // Притяжение к магниту.
            if (attractable && !_parried)
            {
                var magnet = NailMagnet.FindNearest(transform.position);
                if (magnet != null)
                    Steer(magnet.transform.position, attractTurnSpeed, dt);
            }

            Vector3 step = _velocity * dt;
            float dist = step.magnitude;
            if (dist <= 0.0001f) return;

            Vector3 dir = step / dist;
            int n = Physics.RaycastNonAlloc(new Ray(transform.position, dir), _hits, dist, collisionMask, QueryTriggerInteraction.Ignore);
            if (n > 0)
            {
                SortByDistance(n);
                for (int i = 0; i < n; i++)
                {
                    var col = _hits[i].collider;
                    if (_owner != null && col.transform.IsChildOf(_owner.transform)) continue;

                    var dmg = col.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        if (dmg.Faction == _ownerFaction) continue;

                        float final = damage;
                        if (sourceWeapon != null)
                            final *= sourceWeapon.DistanceDamageMult(Vector3.Distance(_launchOrigin, _hits[i].point));

                        var info = new DamageInfo(final, _hits[i].point, _hits[i].normal, _ownerFaction, _owner, dir * knockback)
                        {
                            Weapon = sourceWeapon,
                            WasParried = _parried
                        };
                        dmg.TakeDamage(info);
                        Impact(_hits[i].point, _hits[i].normal);
                        return;
                    }

                    Impact(_hits[i].point, _hits[i].normal);
                    return;
                }
            }

            transform.position += step;
            if (orientToVelocity && (gravityScale != 0f || _parried || attractable))
                Orient(dir);
        }

        private void Steer(Vector3 target, float turnSpeedDeg, float dt)
        {
            Vector3 want = target - transform.position;
            if (want.sqrMagnitude < 0.0001f) return;
            float spd = _velocity.magnitude;
            Vector3 newDir = Vector3.RotateTowards(_velocity.normalized, want.normalized,
                turnSpeedDeg * Mathf.Deg2Rad * dt, 0f);
            _velocity = newDir * spd;
        }

        private static readonly Collider[] _aoeOverlap = new Collider[24];

        private void Impact(Vector3 point, Vector3 normal)
        {
            if (explodeOnImpact) Explode(point);

            if (hitEffectPrefab != null)
                PoolManager.Spawn(hitEffectPrefab, point, Quaternion.LookRotation(Vector3.forward, normal));
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(SoundId.Impact, point);
            PoolManager.Despawn(gameObject);
        }

        /// <summary>AoE-взрыв: бьёт всех в радиусе (игрока — с понижающим фактором → рокет-джамп).</summary>
        private void Explode(Vector3 point)
        {
            if (explosionEffectPrefab != null)
                PoolManager.Spawn(explosionEffectPrefab, point, Quaternion.identity);
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(SoundId.Explosion, point);
            CameraController.Shake(0.5f);

            float aoeDamage = damage * explosionDamageFactor;
            int n = Physics.OverlapSphereNonAlloc(point, explosionRadius, _aoeOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var dmg = _aoeOverlap[i] != null ? _aoeOverlap[i].GetComponentInParent<IDamageable>() : null;
                if (dmg == null || !dmg.IsAlive) continue;

                Vector3 targetPos = _aoeOverlap[i].transform.position;
                float amount = aoeDamage;
                if (dmg.Faction == Faction.Player) amount *= playerSelfDamageFactor;

                Vector3 push = (targetPos - point).normalized * explosionKnockback + Vector3.up * 4f;
                // Убийства сплэшем засчитываются владельцу (стиль/вампиризм);
                // самоурон игроку — «нейтральный» (рокет-джамп без штрафов системе стиля).
                Faction srcFaction = dmg.Faction == Faction.Player ? Faction.Neutral : _ownerFaction;
                var info = new DamageInfo(amount, targetPos, Vector3.up, srcFaction, _owner, push)
                { Weapon = sourceWeapon };
                dmg.TakeDamage(info);

                // Физический толчок игроку (рокет-джамп) — урон его Rigidbody не двигает сам.
                if (dmg.Faction == Faction.Player && PlayerController.Instance != null)
                {
                    var rb = PlayerController.Instance.GetComponent<Rigidbody>();
                    if (rb != null) rb.linearVelocity += push;
                }
            }
        }

        private void Orient(Vector3 dir)
        {
            if (!orientToVelocity) return;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SortByDistance(int n)
        {
            for (int i = 1; i < n; i++)
            {
                var key = _hits[i];
                int j = i - 1;
                while (j >= 0 && _hits[j].distance > key.distance)
                {
                    _hits[j + 1] = _hits[j];
                    j--;
                }
                _hits[j + 1] = key;
            }
        }
    }
}
