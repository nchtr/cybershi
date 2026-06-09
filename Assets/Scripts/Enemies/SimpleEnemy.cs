using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Простой враг-«летун»: висит в воздухе, держит дистанцию от игрока и поливает его
    /// паттернами буллет-хелла через <see cref="BulletEmitter"/>.
    ///
    /// Жизненный цикл боя: как только игрок входит в радиус — враг «агрится» и сообщает
    /// <see cref="CombatStateTracker"/> (это включает боевую музыку). При потере цели или
    /// смерти — снимает себя с учёта. Здоровье/смерть — через компонент <see cref="Health"/>.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleEnemy : MonoBehaviour
    {
        [Header("Восприятие")]
        public float aggroRange = 16f;
        public float deAggroRange = 24f;

        [Header("Движение")]
        public float preferredDistance = 8f;
        public float moveSpeed = 5f;
        [Tooltip("Амплитуда бокового «покачивания» для живости.")]
        public float strafeAmplitude = 2f;
        public float strafeFrequency = 1.5f;

        [Header("Стрельба")]
        public BulletEmitter emitter;
        public float fireInterval = 1.1f;
        [Tooltip("Задержка перед первым выстрелом после агра.")]
        public float aggroDelay = 0.4f;

        private Health _health;
        private Rigidbody _rb;
        private bool _aggro;
        private float _fireTimer;
        private float _strafePhase;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            _strafePhase = Random.value * Mathf.PI * 2f;
            if (emitter == null) emitter = GetComponentInChildren<BulletEmitter>();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            SetAggro(false);
        }

        private Transform Player => PlayerController.Instance != null ? PlayerController.Instance.transform : null;

        private void Update()
        {
            var player = Player;
            if (player == null) { SetAggro(false); return; }

            float dist = Vector3.Distance(transform.position, player.position);

            if (!_aggro && dist <= aggroRange) { SetAggro(true); _fireTimer = aggroDelay; }
            else if (_aggro && dist > deAggroRange) SetAggro(false);

            if (!_aggro) return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f && emitter != null)
            {
                Vector3 dir = player.position - emitter.transform.position;
                dir.z = 0f;
                emitter.Emit(dir);
                _fireTimer = fireInterval;
            }
        }

        private void FixedUpdate()
        {
            var player = Player;
            if (player == null || !_aggro) { _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, Vector3.zero, 0.1f); return; }

            // Держим предпочтительную дистанцию: цель — точка на окружности вокруг игрока.
            Vector3 fromPlayer = transform.position - player.position;
            fromPlayer.z = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = Vector3.up;
            Vector3 anchor = player.position + fromPlayer.normalized * preferredDistance;

            // Боковое покачивание.
            _strafePhase += Time.fixedDeltaTime * strafeFrequency;
            Vector3 perp = Vector3.Cross(fromPlayer.normalized, Vector3.forward);
            anchor += perp * (Mathf.Sin(_strafePhase) * strafeAmplitude);

            Vector3 toAnchor = anchor - transform.position;
            toAnchor.z = 0f;
            Vector3 desired = Vector3.ClampMagnitude(toAnchor, 1f) * moveSpeed;
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desired, 0.15f);
        }

        private void SetAggro(bool value)
        {
            if (_aggro == value) return;
            _aggro = value;
            if (CombatStateTracker.Instance != null)
            {
                if (value) CombatStateTracker.Instance.EnterCombat(this);
                else CombatStateTracker.Instance.ExitCombat(this);
            }
        }

        private void OnDied(Health h, DamageInfo info)
        {
            SetAggro(false);
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.EnemyDeath, transform.position);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, preferredDistance);
        }
    }
}
