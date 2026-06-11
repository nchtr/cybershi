using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// «Мячик для пинг-понга» — спутник оружия ближнего боя. Выпускается на ПКМ, рикошетит
    /// от поверхностей и врагов, как шарик.
    ///
    /// Физика наград:
    ///  • ИМПУЛЬС (удары ракеткой, <see cref="Smash"/>) добавляет скорость → от скорости
    ///    зависит УРОН (damage = damagePerSpeed * speed);
    ///  • от СКОРОСТИ зависит ВРЕМЯ ЖИЗНИ: life = baseLife * (speed / refSpeed) — быстрый мяч
    ///    живёт дольше, затухающий быстро испаряется;
    ///  • мяч бьёт И ИГРОКА (вполсилы) — швыряться им вслепую опасно.
    ///
    /// Отбивается ТОЛЬКО ближней атакой (ракеткой), не рывком.
    /// </summary>
    public class PongBall : MonoBehaviour, IPoolable
    {
        public static readonly List<PongBall> Active = new();

        [Header("Урон")]
        [Tooltip("Урон = это значение * текущая скорость.")]
        public float damagePerSpeed = 1.1f;
        [Range(0f, 1f)] public float playerDamageFactor = 0.5f;

        [Header("Скорость и жизнь")]
        [Tooltip("Базовое время жизни при опорной скорости.")]
        public float baseLife = 4f;
        [Tooltip("Опорная скорость для пересчёта времени жизни.")]
        public float refSpeed = 16f;
        [Tooltip("Потеря скорости при отскоке от поверхности.")]
        [Range(0.5f, 1f)] public float bounceKeep = 0.88f;
        public float minLife = 0.8f;
        public float maxLife = 9f;

        [Header("Прочее")]
        public LayerMask collisionMask = ~0;
        public GameObject hitEffectPrefab;

        private Vector3 _dir = Vector3.right;
        private float _speed;
        private float _remainingLife;
        private float _ownerGrace; // не бьём игрока сразу после запуска/удара
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public float Speed => _speed;

        public void OnSpawned()
        {
            Active.Add(this);
            _ownerGrace = 0.25f;
        }

        public void OnDespawned()
        {
            Active.Remove(this);
        }

        /// <summary>Запуск мяча (ПКМ ракетки).</summary>
        public void Launch(Vector3 velocity)
        {
            _dir = velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector3.right;
            SetSpeed(velocity.magnitude);
            _ownerGrace = 0.25f;
        }

        /// <summary>
        /// Удар ракеткой: новое направление + импульс. Скорость растёт → растут урон и время жизни.
        /// </summary>
        public void Smash(Vector3 aimDir, float impulse)
        {
            _dir = aimDir.sqrMagnitude > 0.001f ? aimDir.normalized : _dir;
            SetSpeed(_speed + impulse);
            _ownerGrace = 0.3f;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.BallBounce, transform.position);
            if (StyleSystem.Instance != null) StyleSystem.Instance.AddStyle(15f, "ПОДАЧА!");
        }

        private void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0f, speed);
            // Время жизни пересчитывается от скорости при каждом её изменении.
            _remainingLife = Mathf.Clamp(baseLife * (_speed / Mathf.Max(1f, refSpeed)), minLife, maxLife);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _remainingLife -= dt;
            if (_ownerGrace > 0f) _ownerGrace -= dt;
            if (_remainingLife <= 0f || _speed < 1f)
            {
                if (hitEffectPrefab != null)
                    PoolManager.Spawn(hitEffectPrefab, transform.position, Quaternion.identity);
                PoolManager.Despawn(gameObject);
                return;
            }

            float dist = _speed * dt;
            int n = Physics.RaycastNonAlloc(new Ray(transform.position, _dir), _hits, dist + 0.05f,
                collisionMask, QueryTriggerInteraction.Ignore);

            RaycastHit? hit = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                bool isPlayer = PlayerController.Instance != null &&
                                col.transform.IsChildOf(PlayerController.Instance.transform);
                if (isPlayer && _ownerGrace > 0f) continue;
                if (_hits[i].distance < bestD) { bestD = _hits[i].distance; hit = _hits[i]; }
            }

            if (hit.HasValue)
            {
                var h = hit.Value;
                var dmg = h.collider.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    float amount = damagePerSpeed * _speed;
                    if (dmg.Faction == Faction.Player) amount *= playerDamageFactor;
                    dmg.TakeDamage(new DamageInfo(amount, h.point, h.normal,
                        dmg.Faction == Faction.Player ? Faction.Neutral : Faction.Player,
                        gameObject, _dir * _speed * 0.3f));
                    if (hitEffectPrefab != null)
                        PoolManager.Spawn(hitEffectPrefab, h.point, Quaternion.identity);
                }

                // Отскок (и от стен, и от тел).
                _dir = Vector3.Reflect(_dir, h.normal);
                _dir.z = 0f;
                if (_dir.sqrMagnitude < 0.001f) _dir = h.normal;
                _dir.Normalize();
                transform.position = h.point + h.normal * 0.08f;
                SetSpeed(_speed * bounceKeep);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.BallBounce, h.point);
            }
            else
            {
                transform.position += _dir * dist;
            }

            // Держим мяч в игровой плоскости (2D-режим).
            if (PerspectiveManager.Instance == null ||
                PerspectiveManager.Instance.CurrentMode == PerspectiveMode.Side2D)
            {
                var p = transform.position;
                p.z = 0f;
                transform.position = p;
            }
        }
    }
}
