using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Монетка (альт-огонь револьвера). Подбрасывается по дуге; если хитскан-выстрел попадает
    /// в неё — выстрел РИКОШЕТИТ: с позиции монетки автонаводится на приоритетную цель
    /// (самый «жирный» враг в радиусе) и наносит увеличенный урон. Монетка одноразовая.
    ///
    /// Нужен маленький SphereCollider с isTrigger = true: обычные снаряды (Raycast с
    /// QueryTriggerInteraction.Ignore) её игнорируют, а хитскан ищет триггеры специально.
    /// </summary>
    public class Coin : MonoBehaviour, IPoolable
    {
        [Tooltip("Множитель урона рикошета.")]
        public float ricochetDamageMult = 1.5f;
        [Tooltip("Радиус поиска приоритетной цели от монетки.")]
        public float ricochetSearchRadius = 30f;
        public float gravity = 18f;
        public float lifeTime = 3.5f;

        private Vector3 _velocity;
        private float _age;
        private bool _used;

        public void Toss(Vector3 velocity)
        {
            _velocity = velocity;
        }

        public void OnSpawned()
        {
            _age = 0f;
            _used = false;
        }

        public void OnDespawned() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            _velocity += Vector3.down * (gravity * dt);
            transform.position += _velocity * dt;
        }

        /// <summary>
        /// Хитскан попал в монетку: рикошет в приоритетную цель. Возвращает цель (или null).
        /// Монетка расходуется в любом случае.
        /// </summary>
        public Health Ricochet()
        {
            if (_used) return null;
            _used = true;
            var target = ParryUtility.FindPriorityEnemy(transform.position, ricochetSearchRadius);
            PoolManager.Despawn(gameObject);
            return target;
        }
    }
}
