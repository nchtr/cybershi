using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// «Конструкторная» база для всех врагов. Берёт на себя всю рутину:
    ///  • агр/потеря цели по дистанции + регистрация в <see cref="CombatStateTracker"/> (музыка боя);
    ///  • ссылки на Rigidbody/Health/визуал, настройка физики (гравитация для наземных,
    ///    заморозка Z и вращения);
    ///  • разворот спрайта к игроку, звук смерти, удобные хелперы движения.
    ///
    /// Наследник реализует только два метода:
    ///  • <see cref="TickAI"/> — логика (таймеры атак, выбор состояния), каждый кадр при агре;
    ///  • <see cref="TickMove"/> — движение (вызывается в FixedUpdate при агре).
    ///
    /// Собрать нового врага: пустой объект → Rigidbody + CapsuleCollider + Health (faction=Enemy)
    /// + ваш наследник EnemyBase + дочерний Visual со SpriteRenderer. Всё остальное — само.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Восприятие")]
        public float aggroRange = 16f;
        public float deAggroRange = 28f;

        [Header("База")]
        [Tooltip("Наземный враг (включается гравитация). Выключите для летающих.")]
        public bool useGravity = true;
        [Tooltip("Разворачивать визуал лицом к игроку.")]
        public bool facePlayer = true;

        protected Rigidbody Rb { get; private set; }
        protected Health Hp { get; private set; }
        protected SpriteRenderer VisualSr { get; private set; }
        protected bool Aggro { get; private set; }

        protected static Transform Player =>
            PlayerController.Instance != null ? PlayerController.Instance.transform : null;

        /// <summary>Дистанция до игрока (или +∞, если игрока нет).</summary>
        protected float PlayerDist
        {
            get
            {
                var p = Player;
                return p != null ? Vector3.Distance(transform.position, p.position) : float.MaxValue;
            }
        }

        /// <summary>Горизонтальное направление на игрока: -1 / +1 (0, если игрока нет).</summary>
        protected float DirToPlayerX
        {
            get
            {
                var p = Player;
                if (p == null) return 0f;
                float dx = p.position.x - transform.position.x;
                return Mathf.Abs(dx) < 0.05f ? 0f : Mathf.Sign(dx);
            }
        }

        protected virtual void Awake()
        {
            Hp = GetComponent<Health>();
            Rb = GetComponent<Rigidbody>();
            Rb.useGravity = useGravity;
            Rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            VisualSr = GetComponentInChildren<SpriteRenderer>();
        }

        protected virtual void OnEnable() => Hp.Died += HandleDeath;

        protected virtual void OnDisable()
        {
            Hp.Died -= HandleDeath;
            SetAggro(false);
        }

        protected virtual void Update()
        {
            var p = Player;
            if (p == null) { SetAggro(false); return; }

            float dist = PlayerDist;
            if (!Aggro && dist <= aggroRange) { SetAggro(true); OnAggroStart(); }
            else if (Aggro && dist > deAggroRange) SetAggro(false);

            if (!Aggro) return;

            if (facePlayer && VisualSr != null)
                VisualSr.flipX = p.position.x < transform.position.x;

            TickAI(Time.deltaTime);
        }

        protected virtual void FixedUpdate()
        {
            if (!Aggro)
            {
                // Затухание, чтобы враг не уезжал в бесконечность после потери цели.
                Vector3 v = Rb.linearVelocity;
                Rb.linearVelocity = new Vector3(Mathf.Lerp(v.x, 0f, 0.1f), useGravity ? v.y : Mathf.Lerp(v.y, 0f, 0.1f), 0f);
                return;
            }
            TickMove(Time.fixedDeltaTime);
        }

        /// <summary>Логика (кадр). Вызывается только при агре.</summary>
        protected abstract void TickAI(float dt);

        /// <summary>Движение (FixedUpdate). Вызывается только при агре.</summary>
        protected abstract void TickMove(float dt);

        /// <summary>Хук на момент обнаружения игрока.</summary>
        protected virtual void OnAggroStart() { }

        // ------------------------------------------------------------- хелперы

        /// <summary>Бежать по горизонтали (вертикаль/гравитация не трогаются).</summary>
        protected void MoveHorizontal(float dirX, float speed)
        {
            Vector3 v = Rb.linearVelocity;
            v.x = Mathf.Lerp(v.x, dirX * speed, 0.25f);
            v.z = 0f;
            Rb.linearVelocity = v;
        }

        /// <summary>Полностью остановить горизонтальное движение.</summary>
        protected void StopHorizontal()
        {
            Vector3 v = Rb.linearVelocity;
            v.x = Mathf.Lerp(v.x, 0f, 0.4f);
            Rb.linearVelocity = v;
        }

        /// <summary>Урон игроку, если тот в радиусе от точки (ближняя атака).</summary>
        protected bool TryHitPlayer(Vector3 from, float radius, float damage, float knockback)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return false;
            if (Vector3.Distance(from, pc.transform.position) > radius) return false;

            var dmg = pc.GetComponent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) return false;
            Vector3 push = (pc.transform.position - transform.position).normalized * knockback + Vector3.up * 2f;
            dmg.TakeDamage(new DamageInfo(damage, pc.transform.position, Vector3.up, Faction.Enemy, gameObject, push));
            return true;
        }

        protected void Sfx(SoundId id)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, transform.position);
        }

        /// <summary>Подкрасить визуал (телеграф атаки). Передайте null-цвет = вернуть исходный.</summary>
        protected void Tint(Color? c)
        {
            var pv = GetComponentInChildren<PlaceholderVisual>();
            if (pv == null || VisualSr == null) return;
            VisualSr.color = c ?? pv.color;
        }

        private void SetAggro(bool value)
        {
            if (Aggro == value) return;
            Aggro = value;
            if (CombatStateTracker.Instance != null)
            {
                if (value) CombatStateTracker.Instance.EnterCombat(this);
                else CombatStateTracker.Instance.ExitCombat(this);
            }
        }

        private void HandleDeath(Health h, DamageInfo info)
        {
            SetAggro(false);
            Sfx(SoundId.EnemyDeath);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
    }
}
