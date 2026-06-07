using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Снаряд. Движется без физического Rigidbody — каждый кадр делает RaycastAll по пути,
    /// что исключает "протыкание" быстрых пуль сквозь тонкие стены (важно для шутера).
    /// Не имеет своего коллайдера, поэтому снаряды не сталкиваются друг с другом —
    /// это держит буллет-хелл дешёвым. Всегда спавнится через <see cref="PoolManager"/>.
    ///
    /// Свою/чужую фракцию различает по <see cref="IDamageable.Faction"/>: пуля бьёт всех,
    /// кроме фракции владельца. Всё без коллайдеров (только Default-окружение) считается стеной.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [Header("Параметры (могут перезаписываться оружием/паттерном)")]
        public float speed = 24f;
        public float damage = 10f;
        public float lifeTime = 4f;
        public float knockback = 0f;
        [Tooltip("Дуга: множитель гравитации (0 — летит прямо).")]
        public float gravityScale = 0f;
        [Tooltip("Поворачивать спрайт по направлению полёта (спрайт смотрит вправо, +X).")]
        public bool orientToVelocity = true;

        [Header("Эффекты")]
        public GameObject hitEffectPrefab;

        // Маска столкновений: что вообще может остановить пулю. ~0 = всё, кроме триггеров.
        public LayerMask collisionMask = ~0;

        private Vector3 _velocity;
        private float _age;
        private Faction _ownerFaction;
        private GameObject _owner;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Запустить снаряд. Вызывается оружием/эмиттером сразу после Spawn.</summary>
        public void Launch(Vector3 direction, Faction ownerFaction, GameObject owner, float? speedOverride = null, float? damageOverride = null)
        {
            _ownerFaction = ownerFaction;
            _owner = owner;
            _age = 0f;
            float spd = speedOverride ?? speed;
            if (damageOverride.HasValue) damage = damageOverride.Value;

            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            _velocity = dir * spd;
            Orient(dir);
        }

        public void OnSpawned() { _age = 0f; }
        public void OnDespawned() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            if (gravityScale != 0f)
                _velocity += Physics.gravity * (gravityScale * dt);

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
                    // Игнорируем владельца и его дочерние коллайдеры.
                    if (_owner != null && col.transform.IsChildOf(_owner.transform)) continue;

                    var dmg = col.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        if (dmg.Faction == _ownerFaction) continue; // своих не задеваем — летим дальше
                        Vector3 push = dir * knockback;
                        dmg.TakeDamage(new DamageInfo(damage, _hits[i].point, _hits[i].normal, _ownerFaction, _owner, push));
                        Impact(_hits[i].point, _hits[i].normal);
                        return;
                    }

                    // Объект без IDamageable, но с коллайдером — стена/пол.
                    Impact(_hits[i].point, _hits[i].normal);
                    return;
                }
            }

            transform.position += step;
            if (orientToVelocity && gravityScale != 0f) Orient(dir);
        }

        private void Impact(Vector3 point, Vector3 normal)
        {
            if (hitEffectPrefab != null)
                PoolManager.Spawn(hitEffectPrefab, point, Quaternion.LookRotation(Vector3.forward, normal));
            if (AudioManager.Instance != null)
                AudioManager.Instance.Play(SoundId.Impact, point);
            PoolManager.Despawn(gameObject);
        }

        private void Orient(Vector3 dir)
        {
            if (!orientToVelocity) return;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Простая вставочная сортировка попаданий по дистанции (n мало).
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
