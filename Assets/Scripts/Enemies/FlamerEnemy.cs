using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Огнемётчик: подходит на среднюю дистанцию и поливает игрока КОНУСОМ ОГНЯ —
    /// очередью короткоживущих снарядов со случайным разбросом (струя). Между очередями
    /// перезаряжается. Опасен зоной, а не точностью — выдавливает игрока с позиции.
    /// </summary>
    public class FlamerEnemy : EnemyBase
    {
        [Header("Дистанции")]
        [Tooltip("Рабочая дистанция струи.")]
        public float flameRange = 6.5f;
        public float moveSpeed = 5.5f;

        [Header("Струя")]
        public GameObject flameBulletPrefab;
        [Tooltip("Снарядов огня в секунду во время очереди.")]
        public float sprayRate = 14f;
        [Tooltip("Полуугол конуса, градусы.")]
        public float coneHalfAngle = 13f;
        public float flameSpeed = 10f;
        public float flameDamage = 2.5f;
        [Tooltip("Длительность очереди, сек.")]
        public float burstTime = 1.4f;
        [Tooltip("Пауза между очередями, сек.")]
        public float restTime = 1.3f;

        private enum State { Approach, Spray, Rest }
        private State _state = State.Approach;
        private float _timer;
        private float _sprayTimer;

        private Vector3 Muzzle => transform.position + Vector3.up * 0.3f;

        protected override void TickAI(float dt)
        {
            _timer -= dt;
            switch (_state)
            {
                case State.Approach:
                    if (PlayerDist <= flameRange)
                    {
                        _state = State.Spray;
                        _timer = burstTime;
                        Sfx(SoundId.EnemyFlame);
                    }
                    break;

                case State.Spray:
                    SprayTick(dt);
                    // Игрок убежал слишком далеко — очередь не тратится впустую.
                    if (_timer <= 0f || PlayerDist > flameRange * 1.6f)
                    {
                        _state = State.Rest;
                        _timer = restTime;
                    }
                    break;

                case State.Rest:
                    if (_timer <= 0f) _state = State.Approach;
                    break;
            }
        }

        protected override void TickMove(float dt)
        {
            if (_state == State.Approach && PlayerDist > flameRange * 0.8f)
                MoveHorizontal(DirToPlayerX, moveSpeed);
            else
                StopHorizontal();
        }

        private void SprayTick(float dt)
        {
            if (flameBulletPrefab == null) return;
            _sprayTimer -= dt;
            if (_sprayTimer > 0f) return;
            _sprayTimer = 1f / Mathf.Max(1f, sprayRate);

            var p = Player;
            if (p == null) return;
            Vector3 dir = (p.position + Vector3.up * 0.3f - Muzzle).normalized;
            float angle = Random.Range(-coneHalfAngle, coneHalfAngle);
            dir = Quaternion.AngleAxis(angle, Vector3.forward) * dir;

            var go = PoolManager.Spawn(flameBulletPrefab, Muzzle, Quaternion.identity);
            var proj = go != null ? go.GetComponent<Projectile>() : null;
            if (proj != null)
            {
                // Лёгкая вариация скорости — струя «дышит».
                float speed = flameSpeed * Random.Range(0.85f, 1.15f);
                proj.Launch(dir, Faction.Enemy, gameObject, speed, flameDamage);
            }
        }
    }
}
