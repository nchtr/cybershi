using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Простой враг ближнего боя: бежит к игроку по земле и бьёт, когда подошёл.
    /// Атака телеграфируется (замирает и белеет на windup) — её можно перепрыгнуть/проskip рывком.
    /// </summary>
    public class MeleeRusher : EnemyBase
    {
        [Header("Движение")]
        public float moveSpeed = 7f;

        [Header("Ближняя атака")]
        public float attackRange = 2.3f;
        [Tooltip("Замах перед ударом, сек (окно уворота).")]
        public float windupTime = 0.35f;
        public float damage = 12f;
        public float knockback = 9f;
        public float cooldown = 1.0f;
        [Tooltip("Радиус поражения удара вокруг точки перед врагом.")]
        public float strikeRadius = 1.6f;

        private enum State { Chase, Windup, Recover }
        private State _state = State.Chase;
        private float _timer;

        protected override void TickAI(float dt)
        {
            _timer -= dt;
            switch (_state)
            {
                case State.Chase:
                    if (PlayerDist <= attackRange)
                    {
                        _state = State.Windup;
                        _timer = windupTime;
                        Tint(Color.white); // телеграф
                    }
                    break;

                case State.Windup:
                    if (_timer <= 0f)
                    {
                        Strike();
                        _state = State.Recover;
                        _timer = cooldown;
                        Tint(null);
                    }
                    break;

                case State.Recover:
                    if (_timer <= 0f) _state = State.Chase;
                    break;
            }
        }

        protected override void TickMove(float dt)
        {
            if (_state == State.Chase) MoveHorizontal(DirToPlayerX, moveSpeed);
            else StopHorizontal();
        }

        private void Strike()
        {
            Vector3 from = transform.position + Vector3.right * (DirToPlayerX * attackRange * 0.5f);
            Sfx(SoundId.EnemyMelee);
            TryHitPlayer(from, strikeRadius, damage, knockback);
        }
    }
}
