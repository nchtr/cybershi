using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// «Мячик для пинг-понга» — спутник ракетки. Выпускается на ПКМ, летит с гравитацией
    /// по дуге и рикошетит от поверхностей и врагов.
    ///
    /// Физика (реалистичная):
    ///  • полноценный вектор скорости + гравитация — мяч летит по дуге, а не по прямой;
    ///  • отскок раскладывается на составляющие: нормальная гасится коэффициентом
    ///    восстановления (<see cref="restitution"/>), касательная почти сохраняется
    ///    (<see cref="bounceKeep"/>) — как у настоящего шарика;
    ///  • удар ракеткой (<see cref="Smash"/>) задаёт направление и добавляет импульс.
    ///
    /// Награды: УРОН = damagePerSpeed × скорость; ВРЕМЯ ЖИЗНИ = baseLife × (скорость/refSpeed) —
    /// разогнанный мяч живёт дольше и бьёт больнее. Бьёт и игрока (вполсилы).
    /// Отбивается ТОЛЬКО ближней атакой (ракеткой).
    /// </summary>
    public class PongBall : MonoBehaviour, IPoolable
    {
        public static readonly List<PongBall> Active = new();

        [Header("Урон")]
        [Tooltip("Урон = это значение * текущая скорость.")]
        public float damagePerSpeed = 0.9f;
        [Range(0f, 1f)] public float playerDamageFactor = 0.5f;

        [Header("Физика")]
        [Tooltip("Гравитация — мяч летит по дуге.")]
        public float gravity = 24f;
        [Tooltip("Коэффициент восстановления: сколько НОРМАЛЬНОЙ скорости сохраняет отскок.")]
        [Range(0.3f, 1f)] public float restitution = 0.78f;
        [Tooltip("Сколько КАСАТЕЛЬНОЙ скорости сохраняет отскок (трение о поверхность).")]
        [Range(0.5f, 1f)] public float bounceKeep = 0.92f;
        public float maxSpeed = 60f;

        [Header("Скорость и жизнь")]
        [Tooltip("Базовое время жизни при опорной скорости.")]
        public float baseLife = 4.5f;
        [Tooltip("Опорная скорость для пересчёта времени жизни.")]
        public float refSpeed = 22f;
        public float minLife = 0.8f;
        public float maxLife = 9f;
        [Tooltip("Мяч медленнее этой скорости испаряется.")]
        public float dieSpeed = 2.5f;

        [Header("Прочее")]
        public LayerMask collisionMask = ~0;
        public GameObject hitEffectPrefab;

        private Vector3 _velocity;
        private float _remainingLife;
        private float _ownerGrace; // не бьём игрока сразу после запуска/удара
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public float Speed => _velocity.magnitude;

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
            _velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
            RecalcLife();
            _ownerGrace = 0.25f;
        }

        /// <summary>
        /// Удар ракеткой: задаёт направление по прицелу и ДОБАВЛЯЕТ импульс к текущей скорости.
        /// Скорость растёт → растут урон и время жизни.
        /// </summary>
        public void Smash(Vector3 aimDir, float impulse)
        {
            Vector3 dir = aimDir.sqrMagnitude > 0.001f ? aimDir.normalized
                : (_velocity.sqrMagnitude > 0.001f ? _velocity.normalized : Vector3.right);
            float newSpeed = Mathf.Min(maxSpeed, Speed + impulse);
            _velocity = dir * newSpeed;
            RecalcLife();
            _ownerGrace = 0.3f;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.BallBounce, transform.position);
            if (StyleSystem.Instance != null) StyleSystem.Instance.AddStyle(15f, "ПОДАЧА!");
        }

        private void RecalcLife()
        {
            _remainingLife = Mathf.Clamp(baseLife * (Speed / Mathf.Max(1f, refSpeed)), minLife, maxLife);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _remainingLife -= dt;
            if (_ownerGrace > 0f) _ownerGrace -= dt;

            if (_remainingLife <= 0f || Speed < dieSpeed)
            {
                if (hitEffectPrefab != null)
                    PoolManager.Spawn(hitEffectPrefab, transform.position, Quaternion.identity);
                PoolManager.Despawn(gameObject);
                return;
            }

            // Гравитация — дуга.
            _velocity += Vector3.down * (gravity * dt);
            _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);

            float speed = Speed;
            Vector3 dir = _velocity / Mathf.Max(0.001f, speed);
            float dist = speed * dt;

            int n = Physics.RaycastNonAlloc(new Ray(transform.position, dir), _hits, dist + 0.05f,
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
                    float amount = damagePerSpeed * speed;
                    if (dmg.Faction == Faction.Player) amount *= playerDamageFactor;
                    dmg.TakeDamage(new DamageInfo(amount, h.point, h.normal,
                        dmg.Faction == Faction.Player ? Faction.Neutral : Faction.Player,
                        gameObject, _velocity * 0.3f));
                    if (hitEffectPrefab != null)
                        PoolManager.Spawn(hitEffectPrefab, h.point, Quaternion.identity);
                }

                // Реалистичный отскок: нормальная составляющая гасится restitution,
                // касательная — почти сохраняется (bounceKeep).
                Vector3 normal = h.normal;
                Vector3 vNormal = Vector3.Project(_velocity, normal);
                Vector3 vTangent = _velocity - vNormal;
                _velocity = vTangent * bounceKeep - vNormal * restitution;

                transform.position = h.point + normal * 0.08f;
                RecalcLife();
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.BallBounce, h.point);
            }
            else
            {
                transform.position += dir * dist;
            }

            // Держим мяч в игровой плоскости (2D-режим).
            if (PerspectiveManager.Instance == null ||
                PerspectiveManager.Instance.CurrentMode == PerspectiveMode.Side2D)
            {
                var p = transform.position;
                p.z = 0f;
                transform.position = p;
                _velocity.z = 0f;
            }
        }
    }
}
