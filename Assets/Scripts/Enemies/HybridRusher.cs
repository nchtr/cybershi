using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Гибрид: носится по всей арене (случайные точки вокруг игрока), периодически делает
    /// ПЕРЕКАТЫ (рывок с краткой неуязвимостью — i-frames), а при сближении лупит ближней атакой.
    /// Самый подвижный рядовой враг — заставляет вести цель.
    /// </summary>
    public class HybridRusher : EnemyBase
    {
        [Header("Перемещение по арене")]
        public float moveSpeed = 8f;
        [Tooltip("Разброс точек назначения вокруг игрока по X.")]
        public float roamExtent = 12f;
        [Tooltip("Смена точки назначения каждые N сек.")]
        public Vector2 roamInterval = new Vector2(1.2f, 2.6f);

        [Header("Перекат")]
        public float rollSpeed = 18f;
        public float rollDuration = 0.28f;
        public Vector2 rollInterval = new Vector2(1.6f, 3.4f);
        [Tooltip("Неуязвимость во время переката.")]
        public bool rollInvulnerable = true;

        [Header("Ближняя атака")]
        public float attackRange = 2.4f;
        public float windupTime = 0.25f;
        public float damage = 14f;
        public float knockback = 10f;
        public float cooldown = 0.9f;
        public float strikeRadius = 1.7f;

        private enum State { Roam, Roll, Windup, Recover }
        private State _state = State.Roam;
        private float _timer;
        private float _roamTimer;
        private float _rollTimer;
        private float _targetX;
        private float _rollDir;

        protected override void OnAggroStart()
        {
            PickDestination();
            _rollTimer = Random.Range(rollInterval.x, rollInterval.y);
        }

        protected override void TickAI(float dt)
        {
            _timer -= dt;
            _roamTimer -= dt;
            _rollTimer -= dt;

            switch (_state)
            {
                case State.Roam:
                    if (_roamTimer <= 0f || Mathf.Abs(transform.position.x - _targetX) < 1f)
                        PickDestination();

                    // Игрок рядом — замах.
                    if (PlayerDist <= attackRange)
                    {
                        _state = State.Windup;
                        _timer = windupTime;
                        Tint(Color.white);
                        break;
                    }

                    // Пора перекатиться.
                    if (_rollTimer <= 0f)
                    {
                        StartRoll();
                    }
                    break;

                case State.Roll:
                    if (_timer <= 0f) EndRoll();
                    break;

                case State.Windup:
                    if (_timer <= 0f)
                    {
                        Vector3 from = transform.position + Vector3.right * (DirToPlayerX * attackRange * 0.5f);
                        Sfx(SoundId.EnemyMelee);
                        TryHitPlayer(from, strikeRadius, damage, knockback);
                        _state = State.Recover;
                        _timer = cooldown;
                        Tint(null);
                    }
                    break;

                case State.Recover:
                    if (_timer <= 0f) _state = State.Roam;
                    break;
            }
        }

        protected override void TickMove(float dt)
        {
            switch (_state)
            {
                case State.Roam:
                {
                    float dir = Mathf.Sign(_targetX - transform.position.x);
                    MoveHorizontal(dir, moveSpeed);
                    break;
                }
                case State.Roll:
                    MoveHorizontal(_rollDir, rollSpeed);
                    break;
                default:
                    StopHorizontal();
                    break;
            }
        }

        private void PickDestination()
        {
            var p = Player;
            float anchor = p != null ? p.position.x : transform.position.x;
            _targetX = anchor + Random.Range(-roamExtent, roamExtent);
            _roamTimer = Random.Range(roamInterval.x, roamInterval.y);
        }

        private void StartRoll()
        {
            _state = State.Roll;
            _timer = rollDuration;
            // Катимся в сторону движения, иногда — на игрока.
            _rollDir = Random.value < 0.4f ? DirToPlayerX : Mathf.Sign(_targetX - transform.position.x);
            if (_rollDir == 0f) _rollDir = 1f;
            if (rollInvulnerable) Hp.Invulnerable = true;
            Tint(new Color(0.6f, 0.8f, 1f));
            Sfx(SoundId.PlayerSlide); // переиспользуем «шорх» подката
        }

        private void EndRoll()
        {
            _state = State.Roam;
            _rollTimer = Random.Range(rollInterval.x, rollInterval.y);
            if (rollInvulnerable) Hp.Invulnerable = false;
            Tint(null);
        }

        protected override void OnDisable()
        {
            // Не оставляем врага вечно неуязвимым, если выключили посреди переката.
            if (rollInvulnerable && Hp != null) Hp.Invulnerable = false;
            base.OnDisable();
        }
    }
}
