using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Дальнобойный снайпер. Стоит на дистанции и бьёт точными атаками с телеграфом:
    ///  • PreciseShot — быстрый точный снаряд;
    ///  • Laser — мгновенный хитскан-луч;
    ///  • Alternate — чередует то и другое.
    ///
    /// Прицеливание телеграфируется мигающей линией (`telegraphPrefab`, обычно FX_Tracer):
    /// точка выстрела ФИКСИРУЕТСЯ незадолго до залпа — от луча можно уйти рывком.
    /// Если игрок подобрался ближе <see cref="fleeRange"/> — снайпер УБЕГАЕТ.
    /// </summary>
    public class SniperEnemy : EnemyBase
    {
        public enum AttackType { PreciseShot, Laser, Alternate }

        [Header("Дистанции")]
        [Tooltip("Ближе этой дистанции снайпер бросает прицел и убегает.")]
        public float fleeRange = 7f;
        public float fleeSpeed = 8f;

        [Header("Атака")]
        public AttackType attackType = AttackType.Alternate;
        [Tooltip("Время прицеливания (телеграф), сек.")]
        public float aimTime = 1.0f;
        [Tooltip("За сколько до выстрела точка прицела фиксируется (окно уворота).")]
        public float lockTime = 0.35f;
        public float cooldown = 2.2f;

        [Header("Точный снаряд")]
        public GameObject bulletPrefab;
        public float bulletSpeed = 32f;
        public float bulletDamage = 14f;

        [Header("Лазер")]
        public float laserDamage = 18f;
        public float laserRange = 60f;
        public GameObject laserHitEffectPrefab;

        [Header("Визуал прицела")]
        [Tooltip("Префаб линии (TracerEffect) для телеграфа и луча.")]
        public GameObject telegraphPrefab;
        public float telegraphInterval = 0.09f;

        private enum State { Idle, Aiming, Cooldown }
        private State _state = State.Idle;
        private float _timer;
        private float _telegraphTimer;
        private Vector3 _lockedPoint;
        private bool _locked;
        private bool _nextIsLaser;

        private Vector3 Muzzle => transform.position + Vector3.up * 0.4f;

        protected override void TickAI(float dt)
        {
            // Игрок слишком близко — бросаем всё и убегаем (стрельба прерывается).
            if (PlayerDist < fleeRange)
            {
                if (_state == State.Aiming) { _state = State.Cooldown; _timer = 0.5f; Tint(null); }
                return;
            }

            _timer -= dt;
            switch (_state)
            {
                case State.Idle:
                    _state = State.Aiming;
                    _timer = aimTime;
                    _locked = false;
                    Tint(new Color(1f, 0.6f, 0.6f));
                    break;

                case State.Aiming:
                    // До фиксации целимся в игрока, потом точка замораживается.
                    if (!_locked)
                    {
                        var p = Player;
                        if (p != null) _lockedPoint = p.position + Vector3.up * 0.3f;
                        if (_timer <= lockTime) _locked = true;
                    }
                    DrawTelegraph(dt);
                    if (_timer <= 0f)
                    {
                        Fire();
                        _state = State.Cooldown;
                        _timer = cooldown;
                        Tint(null);
                    }
                    break;

                case State.Cooldown:
                    if (_timer <= 0f) _state = State.Idle;
                    break;
            }
        }

        protected override void TickMove(float dt)
        {
            if (PlayerDist < fleeRange)
            {
                // Бежим ОТ игрока.
                MoveHorizontal(-DirToPlayerX, fleeSpeed);
            }
            else
            {
                StopHorizontal(); // снайпер стреляет стоя
            }
        }

        private void DrawTelegraph(float dt)
        {
            if (telegraphPrefab == null) return;
            _telegraphTimer -= dt;
            if (_telegraphTimer > 0f) return;
            _telegraphTimer = telegraphInterval;

            var go = PoolManager.Spawn(telegraphPrefab, Muzzle, Quaternion.identity);
            var tracer = go != null ? go.GetComponent<TracerEffect>() : null;
            if (tracer != null) tracer.Show(Muzzle, _lockedPoint);
        }

        private void Fire()
        {
            bool laser = attackType == AttackType.Laser ||
                         (attackType == AttackType.Alternate && _nextIsLaser);
            _nextIsLaser = !_nextIsLaser;

            Vector3 dir = (_lockedPoint - Muzzle).normalized;

            if (laser)
            {
                Sfx(SoundId.EnemyLaser);
                FireLaser(dir);
            }
            else
            {
                Sfx(SoundId.EnemyShoot);
                if (bulletPrefab == null) return;
                var go = PoolManager.Spawn(bulletPrefab, Muzzle, Quaternion.identity);
                var proj = go != null ? go.GetComponent<Projectile>() : null;
                if (proj != null)
                    proj.Launch(dir, Faction.Enemy, gameObject, bulletSpeed, bulletDamage);
            }
        }

        private void FireLaser(Vector3 dir)
        {
            Vector3 end = Muzzle + dir * laserRange;
            var hits = Physics.RaycastAll(Muzzle, dir, laserRange, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                var dmg = h.collider.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    if (dmg.Faction == Faction.Enemy) continue; // сквозь своих
                    dmg.TakeDamage(new DamageInfo(laserDamage, h.point, h.normal, Faction.Enemy, gameObject));
                }
                end = h.point;
                if (laserHitEffectPrefab != null)
                    PoolManager.Spawn(laserHitEffectPrefab, end, Quaternion.identity);
                break;
            }

            // Толстый «луч» — три трассера рядом.
            if (telegraphPrefab != null)
            {
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 offset = Vector3.up * (i * 0.07f);
                    var go = PoolManager.Spawn(telegraphPrefab, Muzzle, Quaternion.identity);
                    var tracer = go != null ? go.GetComponent<TracerEffect>() : null;
                    if (tracer != null) tracer.Show(Muzzle + offset, end + offset);
                }
            }
        }
    }
}
