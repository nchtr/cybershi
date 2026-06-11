using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Частица здоровья, вылетающая из врага при «вампирском» убийстве (см. <see cref="Vampirism"/>).
    /// Короткий разлёт в сторону, затем ускоряющееся самонаведение на игрока; при касании лечит
    /// (с учётом усталости вампиризма). Спавнится через пул.
    /// </summary>
    public class HealOrb : MonoBehaviour, IPoolable
    {
        public float healAmount = 4f;
        public float scatterSpeed = 6f;
        public float homingAccel = 60f;
        public float maxSpeed = 26f;
        public float pickupRadius = 0.8f;
        public float lifeTime = 6f;

        private Vector3 _velocity;
        private float _age;

        public void OnSpawned()
        {
            _age = 0f;
            Vector2 r = Random.insideUnitCircle.normalized;
            _velocity = new Vector3(r.x, Mathf.Abs(r.y) + 0.3f, 0f) * scatterSpeed;
        }

        public void OnDespawned() { }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { PoolManager.Despawn(gameObject); return; }

            var player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 to = player.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist <= pickupRadius)
                {
                    if (Vampirism.Instance != null) Vampirism.Instance.ConsumeOrb(healAmount);
                    PoolManager.Despawn(gameObject);
                    return;
                }
                // Чем дольше живёт — тем агрессивнее тянется к игроку.
                _velocity += to.normalized * (homingAccel * Mathf.Clamp01(_age * 2f) * dt);
                _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
            }

            transform.position += _velocity * dt;
        }
    }
}
