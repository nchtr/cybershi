using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Контроллер игрока. Мувсет вдохновлён V1 из ULTRAKILL: быстрый бег, рывок (dash) с
    /// неуязвимостью и зарядами, подкат (slide), высокий прыжок с регулируемой высотой,
    /// прыжок от стен и граунд-слэм с ударной волной.
    ///
    /// Физика: 3D Rigidbody без штатной гравитации (своя — для отзывчивого ощущения).
    /// В режиме Side2D ось Z заморожена (вид сбоку). В ThreeD открывается движение по XZ.
    /// Режим берётся из <see cref="PerspectiveManager"/>.
    ///
    /// Слои/теги не требуются: проверки земли/стен фильтруют собственные коллайдеры.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        private enum MoveState { Normal, Sliding, Dashing, GroundSlamming }

        [Header("Бег")]
        public float moveSpeed = 9f;
        public float groundAccel = 90f;
        public float airAccel = 45f;
        public float groundFriction = 60f;

        [Header("Прыжок")]
        public float jumpHeight = 4.5f;
        public float gravity = 32f;
        [Tooltip("Множитель гравитации на падении — для резкого приземления.")]
        public float fallGravityMult = 1.8f;
        public float maxFallSpeed = 45f;
        [Tooltip("Гашение прыжка при раннем отпускании. 1 = высота НЕ зависит от времени зажатия.")]
        [Range(0f, 1f)] public float jumpCutMultiplier = 1f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;

        [Header("Стены")]
        public float wallCheckDistance = 0.18f;
        public float wallSlideSpeed = 4f;
        public Vector2 wallJumpForce = new Vector2(11f, 14f);
        [Tooltip("Сколько прыжков от стен доступно до касания земли.")]
        public int maxWallJumps = 3;

        [Header("Рывок (Dash)")]
        public float dashSpeed = 28f;
        public float dashDuration = 0.16f;
        public int maxDashCharges = 3;
        public float dashRechargeTime = 0.7f;
        [Tooltip("Неуязвимость во время рывка (требует Health на игроке).")]
        public bool dashInvulnerable = true;

        [Header("Подкат (Slide)")]
        [Tooltip("Постоянная скорость подката: держится, пока зажат Ctrl.")]
        public float slideSpeed = 16f;
        [Tooltip("Во сколько раз уменьшается высота капсулы во время подката.")]
        [Range(0.3f, 1f)] public float slideHeightScale = 0.5f;
        public float slideJumpBoost = 4f;

        [Header("Парирование рывком")]
        [Tooltip("Радиус, в котором рывок отбивает подсвеченные снаряды.")]
        public float dashParryRadius = 1.8f;

        [Header("Граунд-слэм (Ground Slam)")]
        public float slamSpeed = 42f;
        public float slamRadius = 4f;
        public float slamDamage = 40f;
        public float slamKnockback = 12f;
        public GameObject slamEffectPrefab;
        [Tooltip("Сила прыжка-отскока, если нажать прыжок сразу после слэма.")]
        public float slamJumpBoost = 6f;

        // Управление читается централизованно через InputReader (новая Input System):
        // Space — прыжок, Shift — рывок, Ctrl — подкат/слэм, WASD — движение.

        [Header("Ссылки")]
        [Tooltip("Объект Visual со SpriteRenderer/SpriteAnimator для отражения по направлению.")]
        public Transform visual;
        [Tooltip("Маска для проверки земли/стен. По умолчанию Everything — собственные коллайдеры отсеиваются.")]
        public LayerMask environmentMask = ~0;

        // --- Состояние ---
        private Rigidbody _rb;
        private CapsuleCollider _col;
        private Health _health;
        private PlayerAiming _aiming;
        private SpriteAnimator _animator;

        private MoveState _state = MoveState.Normal;
        private bool _grounded;
        private int _wallDir; // -1 слева, +1 справа, 0 нет
        private int _facing = 1;
        private Vector3 _facing3D = Vector3.right;

        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _wallCoyoteTimer;
        private bool _hasWall;            // есть вертикальная поверхность рядом (любая)
        private Vector3 _wallNormal;      // её нормаль (куда отталкиваться)
        private Vector3 _lastWallNormal;
        private int _wallJumpsLeft;
        private Vector3 _slideDir;
        private float _dashTimer;
        private float _dashChargeTimer;
        private int _dashCharges;
        private Vector3 _dashDir;
        private float _defaultColHeight;
        private Vector3 _defaultColCenter;
        private bool _wasGrounded;

        // Залатанный ввод (GetKeyDown ловим в Update, потребляем в FixedUpdate)
        private bool _jumpQueued;
        private bool _jumpHeld;
        private bool _dashQueued;
        private bool _slideSlamQueued;
        private bool _slideSlamHeld;

        private readonly Collider[] _overlap = new Collider[8];

        public bool IsGrounded => _grounded;
        public int FacingSign => _facing;
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public int DashCharges => _dashCharges;
        public int MaxDashCharges => maxDashCharges;
        public Health Health => _health;

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<CapsuleCollider>();
            _health = GetComponent<Health>();
            _aiming = GetComponent<PlayerAiming>();
            if (visual != null) _animator = visual.GetComponent<SpriteAnimator>();

            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.freezeRotation = true;

            _defaultColHeight = _col.height;
            _defaultColCenter = _col.center;
            _dashCharges = maxDashCharges;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool Is2D => PerspectiveManager.Instance == null
            || PerspectiveManager.Instance.CurrentMode == PerspectiveMode.Side2D;

        private void Update()
        {
            // Латаем разовые нажатия, чтобы не потерять их между кадрами физики.
            var input = InputReader.Instance;
            if (input.JumpPressed) { _jumpQueued = true; _jumpBufferTimer = jumpBuffer; }
            _jumpHeld = input.JumpHeld;
            if (input.DashPressed) _dashQueued = true;
            if (input.SlideSlamPressed) _slideSlamQueued = true;
            _slideSlamHeld = input.SlideSlamHeld;

            UpdateFacing();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            ApplyConstraints();
            GroundCheck();
            WallCheck();
            Timers(dt);

            Vector3 vel = _rb.linearVelocity;

            switch (_state)
            {
                case MoveState.Normal: vel = TickNormal(vel, dt); break;
                case MoveState.Sliding: vel = TickSlide(vel, dt); break;
                case MoveState.Dashing: vel = TickDash(vel, dt); break;
                case MoveState.GroundSlamming: vel = TickSlam(vel, dt); break;
            }

            _rb.linearVelocity = vel;

            // Звук приземления
            if (_grounded && !_wasGrounded && _state != MoveState.GroundSlamming)
                Sfx(SoundId.PlayerLand);
            _wasGrounded = _grounded;

            // Сброс разовых флагов
            _jumpQueued = false;
            _dashQueued = false;
            _slideSlamQueued = false;
        }

        // ---------------------------------------------------------------- ввод/направления

        private float InputH => InputReader.Instance.Move.x;
        private float InputV => InputReader.Instance.Move.y;

        /// <summary>Желаемое направление движения в горизонтальной плоскости (мир), длина 0..1.</summary>
        private Vector3 WishDir()
        {
            if (Is2D)
            {
                return Vector3.right * Mathf.Clamp(InputH, -1f, 1f);
            }
            else
            {
                // Относительно поворота камеры по рысканью.
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                Vector3 fwd = cam != null ? cam.forward : Vector3.forward;
                Vector3 right = cam != null ? cam.right : Vector3.right;
                fwd.y = 0f; right.y = 0f;
                fwd.Normalize(); right.Normalize();
                Vector3 dir = right * InputH + fwd * InputV;
                return dir.sqrMagnitude > 1f ? dir.normalized : dir;
            }
        }

        private void UpdateFacing()
        {
            // Лицом к курсору, если есть прицеливание; иначе по направлению ввода.
            if (_aiming != null && _aiming.AimDirection.sqrMagnitude > 0.001f)
            {
                if (Mathf.Abs(_aiming.AimDirection.x) > 0.01f)
                    _facing = _aiming.AimDirection.x >= 0f ? 1 : -1;
            }
            else if (Mathf.Abs(InputH) > 0.01f)
            {
                _facing = InputH >= 0f ? 1 : -1;
            }

            Vector3 wish = WishDir();
            if (wish.sqrMagnitude > 0.01f) _facing3D = wish.normalized;

            if (_animator != null) _animator.SetFlipX(_facing < 0);
            else if (visual != null)
            {
                var s = visual.localScale;
                s.x = Mathf.Abs(s.x) * _facing;
                visual.localScale = s;
            }
        }

        // ---------------------------------------------------------------- проверки окружения

        private void ApplyConstraints()
        {
            var c = RigidbodyConstraints.FreezeRotation;
            if (Is2D)
            {
                float z = _rb.position.z;
                if (Mathf.Abs(z) > 0.02f)
                {
                    // Возвращаемся в плоскость игры после 3D-секции, затем замораживаем Z.
                    var p = _rb.position;
                    p.z = Mathf.MoveTowards(z, 0f, 18f * Time.fixedDeltaTime);
                    _rb.position = p;
                }
                else
                {
                    c |= RigidbodyConstraints.FreezePositionZ;
                }
            }
            _rb.constraints = c;
        }

        private void GroundCheck()
        {
            _grounded = false;
            // Небольшая сфера у подошвы. Собственный коллайдер она тоже задевает — отсеиваем его.
            float radius = _col.radius * 0.95f;
            Vector3 foot = transform.position + _col.center
                           + Vector3.down * (_col.height * 0.5f - _col.radius + 0.08f);

            int n = Physics.OverlapSphereNonAlloc(foot, radius, _overlap, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                _grounded = true;
                break;
            }
        }

        // Направления проб для поиска стен в 3D (горизонталь).
        private static readonly Vector3[] _wallProbes =
            { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        private void WallCheck()
        {
            _wallDir = 0;
            _hasWall = false;
            if (_grounded) return;

            Vector3 center = transform.position + _col.center;
            float reach = _col.radius + wallCheckDistance;

            // Лучи из ЦЕНТРА капсулы: при прижатии к стене начало луча от края оказывалось бы
            // внутри коллайдера стены и Raycast его не видел. Из центра все стороны равноценны —
            // отталкиваться можно от ЛЮБОЙ вертикальной поверхности.
            if (Is2D)
            {
                bool wallRight = TryWall(center, Vector3.right, reach, out var nR);
                bool wallLeft = TryWall(center, Vector3.left, reach, out var nL);

                if (wallRight && wallLeft)
                {
                    _wallDir = InputH > 0.01f ? 1 : (InputH < -0.01f ? -1 : _facing);
                    _wallNormal = _wallDir > 0 ? nR : nL;
                }
                else if (wallRight) { _wallDir = 1; _wallNormal = nR; }
                else if (wallLeft) { _wallDir = -1; _wallNormal = nL; }
                _hasWall = _wallDir != 0;
            }
            else
            {
                // 3D: пробуем направление движения/взгляда, затем 4 стороны света.
                if (TryWall(center, _facing3D, reach, out var n0)) { _hasWall = true; _wallNormal = n0; }
                else
                {
                    for (int i = 0; i < _wallProbes.Length; i++)
                    {
                        if (TryWall(center, _wallProbes[i], reach, out var nn))
                        {
                            _hasWall = true;
                            _wallNormal = nn;
                            break;
                        }
                    }
                }
            }

            // Койот-тайм для стен: можно отпрыгнуть ещё мгновение после отрыва.
            if (_hasWall)
            {
                _wallCoyoteTimer = coyoteTime;
                _lastWallNormal = _wallNormal;
            }
        }

        /// <summary>Есть ли стена в направлении dir; возвращает её нормаль (вертикальные поверхности).</summary>
        private bool TryWall(Vector3 origin, Vector3 dir, float dist, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (dir.sqrMagnitude < 0.001f) return false;
            dir.y = 0f;
            dir.Normalize();
            int n = Physics.RaycastNonAlloc(new Ray(origin, dir), _rayHits, dist, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (_rayHits[i].collider.transform.IsChildOf(transform)) continue;
                // Вертикальная поверхность: нормаль почти горизонтальна.
                if (Mathf.Abs(_rayHits[i].normal.y) > 0.5f) continue;
                normal = _rayHits[i].normal;
                normal.y = 0f;
                normal.Normalize();
                return true;
            }
            return false;
        }

        private bool RayHitsEnvironment(Vector3 origin, Vector3 dir, float dist)
        {
            int n = Physics.RaycastNonAlloc(new Ray(origin, dir), _rayHits, dist, environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!_rayHits[i].collider.transform.IsChildOf(transform))
                    return true;
            }
            return false;
        }
        private readonly RaycastHit[] _rayHits = new RaycastHit[8];

        private void Timers(float dt)
        {
            _coyoteTimer = _grounded ? coyoteTime : _coyoteTimer - dt;
            if (_grounded) _wallJumpsLeft = maxWallJumps; // касание земли восстанавливает прыжки от стен
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
            if (_wallCoyoteTimer > 0f) _wallCoyoteTimer -= dt;

            // Перезарядка зарядов рывка.
            if (_dashCharges < maxDashCharges)
            {
                _dashChargeTimer -= dt;
                if (_dashChargeTimer <= 0f)
                {
                    _dashCharges++;
                    _dashChargeTimer = dashRechargeTime;
                }
            }
        }

        // ---------------------------------------------------------------- состояния движения

        private Vector3 TickNormal(Vector3 vel, float dt)
        {
            // Переход в рывок
            if (_dashQueued && _dashCharges > 0)
            {
                StartDash();
                return TickDash(vel, dt);
            }

            // Подкат / граунд-слэм по одной кнопке (контекст: земля/воздух)
            if (_slideSlamQueued)
            {
                if (_grounded) return StartSlide();
                StartSlam();
                return TickSlam(vel, dt);
            }

            Vector3 wish = WishDir();
            float vUp = vel.y;
            Vector3 horiz = new Vector3(vel.x, 0f, vel.z);

            // Управление НИКОГДА не блокируется — в том числе сразу после прыжка от стены.
            float accel = _grounded ? groundAccel : airAccel;
            if (wish.sqrMagnitude > 0.01f)
            {
                Vector3 target = wish * moveSpeed;
                horiz = Vector3.MoveTowards(horiz, target, accel * dt);
            }
            else if (_grounded)
            {
                horiz = Vector3.MoveTowards(horiz, Vector3.zero, groundFriction * dt);
            }

            // Прыжок (обычный / от стены)
            bool wantJump = _jumpQueued || _jumpBufferTimer > 0f;
            if (wantJump)
            {
                if (_coyoteTimer > 0f)
                {
                    vUp = JumpVelocity();
                    _coyoteTimer = 0f;
                    _jumpBufferTimer = 0f;
                    Sfx(SoundId.PlayerJump);
                }
                else if ((_hasWall || _wallCoyoteTimer > 0f) && _wallJumpsLeft > 0)
                {
                    // Отталкивание по нормали стены — работает от ЛЮБОЙ вертикальной поверхности
                    // (в 2D и 3D). Лимит maxWallJumps прыжков до касания земли; управление в
                    // воздухе не блокируется — дальнейшее движение без штрафов.
                    Vector3 n = _hasWall ? _wallNormal : _lastWallNormal;
                    horiz = n * wallJumpForce.x;
                    vUp = wallJumpForce.y;
                    _wallJumpsLeft--;
                    _wallCoyoteTimer = 0f;
                    _jumpBufferTimer = 0f;
                    Sfx(SoundId.PlayerWallJump);
                }
            }

            // Регулируемая высота прыжка
            if (!_jumpHeld && vUp > 0f)
                vUp *= jumpCutMultiplier;

            // Гравитация + скольжение по стене (симметрично с обеих сторон).
            vUp = ApplyGravity(vUp, dt);
            bool pressingIntoWall = _wallDir != 0 && Mathf.Abs(InputH) > 0.1f && (int)Mathf.Sign(InputH) == _wallDir;
            if (pressingIntoWall && vUp < -wallSlideSpeed)
                vUp = -wallSlideSpeed;

            return new Vector3(horiz.x, vUp, horiz.z);
        }

        private Vector3 TickSlide(Vector3 vel, float dt)
        {
            // Прыжок из подката — длинный прыжок с бустом
            if (_jumpQueued || _jumpBufferTimer > 0f)
            {
                EndSlide();
                Vector3 boosted = _slideDir * (slideSpeed + slideJumpBoost);
                Sfx(SoundId.PlayerJump);
                return new Vector3(boosted.x, JumpVelocity(), boosted.z);
            }

            // Подкат ПОСТОЯННЫЙ: длится, пока зажат Ctrl. Скорость не затухает,
            // съезд с уступа не прерывает подкат (горизонталь держится, гравитация работает).
            if (!_slideSlamHeld)
            {
                EndSlide();
                return TickNormal(vel, dt);
            }

            Vector3 horiz = _slideDir * slideSpeed;
            float vUp = ApplyGravity(vel.y, dt);
            return new Vector3(horiz.x, vUp, horiz.z);
        }

        private Vector3 TickDash(Vector3 vel, float dt)
        {
            // Во время рывка скорость постоянна, гравитация выключена.
            if (_dashTimer <= 0f)
            {
                EndDash();
                return TickNormal(_dashDir * (moveSpeed * 0.6f), dt); // мягкий выход
            }
            _dashTimer -= dt;

            // Рывок ПАРИРУЕТ подсвеченные снаряды на своём пути.
            ParryUtility.TryParry(transform.position, dashParryRadius, _dashDir, gameObject);

            return _dashDir * dashSpeed;
        }

        private Vector3 TickSlam(Vector3 vel, float dt)
        {
            if (_grounded)
            {
                DoSlamImpact();
                _state = MoveState.Normal;
                // Возможность отскока, если игрок успел нажать прыжок.
                if (_jumpQueued || _jumpBufferTimer > 0f)
                {
                    Sfx(SoundId.PlayerJump);
                    return new Vector3(vel.x, JumpVelocity() + slamJumpBoost, vel.z);
                }
                return new Vector3(0f, 0f, 0f);
            }

            // Лочим горизонталь, валимся вниз с фиксированной скоростью.
            return new Vector3(0f, -slamSpeed, 0f);
        }

        // ---------------------------------------------------------------- входы в состояния

        private void StartDash()
        {
            _state = MoveState.Dashing;
            _dashTimer = dashDuration;
            _dashCharges--;
            if (_dashChargeTimer <= 0f) _dashChargeTimer = dashRechargeTime;

            _dashDir = Is2D ? Vector3.right * _facing : _facing3D;
            if (Is2D && Mathf.Abs(InputH) > 0.01f) _dashDir = Vector3.right * Mathf.Sign(InputH);

            if (dashInvulnerable && _health != null) _health.Invulnerable = true;
            Sfx(SoundId.PlayerDash);
        }

        private void EndDash()
        {
            _state = MoveState.Normal;
            if (dashInvulnerable && _health != null) _health.Invulnerable = false;
        }

        private Vector3 StartSlide()
        {
            _state = MoveState.Sliding;
            // Направление фиксируется на входе в подкат и держится до отпускания Ctrl.
            Vector3 dir = Is2D ? Vector3.right * _facing : _facing3D;
            if (Is2D && Mathf.Abs(InputH) > 0.01f) dir = Vector3.right * Mathf.Sign(InputH);
            _slideDir = dir;

            _col.height = _defaultColHeight * slideHeightScale;
            _col.center = _defaultColCenter - Vector3.up * (_defaultColHeight - _col.height) * 0.5f;
            Sfx(SoundId.PlayerSlide);

            float vUp = Mathf.Min(0f, _rb.linearVelocity.y);
            return _slideDir * slideSpeed + Vector3.up * vUp;
        }

        private void EndSlide()
        {
            _state = MoveState.Normal;
            _col.height = _defaultColHeight;
            _col.center = _defaultColCenter;
        }

        private void StartSlam()
        {
            _state = MoveState.GroundSlamming;
            Sfx(SoundId.PlayerGroundSlam);
        }

        private void DoSlamImpact()
        {
            if (slamEffectPrefab != null)
                PoolManager.Spawn(slamEffectPrefab, transform.position, Quaternion.identity);

            CameraController.Shake(0.6f);

            // Ударная волна: урон врагам в радиусе.
            int n = Physics.OverlapSphereNonAlloc(transform.position, slamRadius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var dmg = _overlap[i] != null ? _overlap[i].GetComponentInParent<IDamageable>() : null;
                if (dmg == null || dmg.Faction == Faction.Player) continue;
                Vector3 push = (_overlap[i].transform.position - transform.position).normalized * slamKnockback;
                dmg.TakeDamage(new DamageInfo(slamDamage, _overlap[i].transform.position, Vector3.up, Faction.Player, gameObject, push));
            }
        }

        // ---------------------------------------------------------------- утилиты

        private float JumpVelocity() => Mathf.Sqrt(2f * gravity * jumpHeight);

        private float ApplyGravity(float vUp, float dt)
        {
            float g = (vUp < 0f) ? gravity * fallGravityMult : gravity;
            vUp -= g * dt;
            return Mathf.Max(vUp, -maxFallSpeed);
        }

        private void Sfx(SoundId id)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, transform.position);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, slamRadius);
        }
    }
}
