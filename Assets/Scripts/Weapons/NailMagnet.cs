using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Магнит (альт-огонь гвоздомёта). Летит по дуге, прилипает к первой поверхности или врагу
    /// и притягивает к себе гвозди (снаряды с флагом <see cref="Projectile.attractable"/>) —
    /// гвозди доворачивают к магниту и осыпают всё рядом с ним. Живёт ограниченное время.
    /// </summary>
    public class NailMagnet : MonoBehaviour, IPoolable
    {
        /// <summary>Активные магниты (по ним стирятся гвозди).</summary>
        public static readonly List<NailMagnet> Active = new();

        [Tooltip("Радиус, в котором гвозди начинают притягиваться.")]
        public float attractRadius = 12f;
        public float gravity = 14f;
        public float lifeTime = 7f;
        public LayerMask collisionMask = ~0;

        private Vector3 _velocity;
        private float _age;
        private bool _stuck;
        private Transform _stuckTo;
        private Vector3 _stuckLocalPos;

        /// <summary>Ближайший активный магнит, в чей радиус попадает точка (или null).</summary>
        public static NailMagnet FindNearest(Vector3 from)
        {
            NailMagnet best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                var m = Active[i];
                if (m == null) continue;
                float d = (m.transform.position - from).sqrMagnitude;
                if (d < m.attractRadius * m.attractRadius && d < bestD)
                {
                    best = m;
                    bestD = d;
                }
            }
            return best;
        }

        public void Launch(Vector3 velocity)
        {
            _velocity = velocity;
        }

        public void OnSpawned()
        {
            _age = 0f;
            _stuck = false;
            _stuckTo = null;
            Active.Add(this);
        }

        public void OnDespawned()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            if (_stuck)
            {
                // Прилип к врагу — следуем за ним; враг умер/исчез — падаем на месте.
                if (_stuckTo != null)
                    transform.position = _stuckTo.TransformPoint(_stuckLocalPos);
                return;
            }

            _velocity += Vector3.down * (gravity * dt);
            Vector3 step = _velocity * dt;
            float dist = step.magnitude;
            if (dist > 0.0001f &&
                Physics.Raycast(transform.position, step / dist, out var hit, dist + 0.05f,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Не липнем к игроку — пролетаем сквозь.
                if (PlayerController.Instance == null ||
                    !hit.collider.transform.IsChildOf(PlayerController.Instance.transform))
                {
                    _stuck = true;
                    transform.position = hit.point + hit.normal * 0.1f;
                    var enemy = hit.collider.GetComponentInParent<Health>();
                    if (enemy != null && enemy.Faction == Faction.Enemy)
                    {
                        _stuckTo = enemy.transform;
                        _stuckLocalPos = _stuckTo.InverseTransformPoint(transform.position);
                    }
                    return;
                }
            }
            transform.position += step;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attractRadius);
        }
    }
}
