using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Камера с двумя режимами:
    ///   • Follow — плавно следует за игроком (есть упреждение по скорости/прицелу);
    ///   • Static — стоит на месте, давая общий план (арена с боссом, динамичная секция).
    ///
    /// Параметры ракурса (смещение, поворот, FOV) берутся из <see cref="PerspectiveManager"/>
    /// и плавно интерполируются — поэтому переход 2D↔3D выглядит как красивый облёт.
    /// Тряску камеры дают выстрелы и граунд-слэм через статический <see cref="Shake"/>.
    ///
    /// Переключают режимы триггеры <see cref="CameraZone"/> на уровне.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        public enum Mode { Follow, Static }

        [Tooltip("Цель слежения. Если пусто — найдётся PlayerController.Instance.")]
        public Transform target;

        [Header("Слежение")]
        public float followSmoothTime = 0.15f;
        [Tooltip("Упреждение в сторону движения игрока.")]
        public float velocityLookAhead = 0.25f;
        [Tooltip("Упреждение в сторону прицела.")]
        public float aimLookAhead = 1.5f;

        [Header("Смена ракурса/перспективы")]
        public float viewBlendSpeed = 3f;

        [Header("Тряска")]
        public float maxShakeOffset = 0.6f;
        public float shakeDecay = 1.8f;

        private Camera _cam;
        private Mode _mode = Mode.Follow;
        private Vector3 _staticPosition;
        private Quaternion _staticRotation;
        private float _staticFov;
        private bool _staticOverridesView;

        private Vector3 _vel; // для SmoothDamp
        private CameraView _view;
        private float _trauma;
        private PlayerAiming _aiming;
        private PlayerController _player;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (target == null && PlayerController.Instance != null)
                target = PlayerController.Instance.transform;

            if (PerspectiveManager.Instance != null) _view = PerspectiveManager.Instance.CurrentView;
            else _view = new CameraView { offset = new Vector3(0, 1.5f, -18f), lookEuler = Vector3.zero, fieldOfView = 35f };

            ResolvePlayerRefs();
        }

        private void ResolvePlayerRefs()
        {
            if (_player == null && PlayerController.Instance != null)
            {
                _player = PlayerController.Instance;
                _aiming = _player.GetComponent<PlayerAiming>();
            }
        }

        private void LateUpdate()
        {
            if (target == null) ResolveTarget();
            ResolvePlayerRefs();

            // Плавно подтягиваем параметры ракурса к целевому виду перспективы.
            if (PerspectiveManager.Instance != null)
            {
                CameraView goal = PerspectiveManager.Instance.CurrentView;
                float t = viewBlendSpeed * Time.deltaTime;
                _view.offset = Vector3.Lerp(_view.offset, goal.offset, t);
                _view.lookEuler = Vector3.Lerp(_view.lookEuler, goal.lookEuler, t);
                _view.fieldOfView = Mathf.Lerp(_view.fieldOfView, goal.fieldOfView, t);
            }

            Vector3 desiredPos;
            Quaternion desiredRot;
            float desiredFov;

            if (_mode == Mode.Static)
            {
                desiredPos = _staticPosition;
                desiredRot = _staticOverridesView ? _staticRotation : Quaternion.Euler(_view.lookEuler);
                desiredFov = _staticOverridesView ? _staticFov : _view.fieldOfView;
                transform.position = Vector3.Lerp(transform.position, desiredPos, viewBlendSpeed * Time.deltaTime);
            }
            else // Follow
            {
                if (target == null) return;
                Vector3 lookAhead = Vector3.zero;
                if (_player != null) lookAhead += _player.Velocity * velocityLookAhead;
                if (_aiming != null) lookAhead += _aiming.AimDirection * aimLookAhead;

                desiredPos = target.position + _view.offset + lookAhead;
                desiredRot = Quaternion.Euler(_view.lookEuler);
                desiredFov = _view.fieldOfView;
                transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, followSmoothTime);
            }

            transform.rotation = desiredRot;
            if (_cam != null) _cam.fieldOfView = desiredFov;

            // Тряска поверх итоговой позиции.
            if (_trauma > 0f)
            {
                float amount = _trauma * _trauma;
                Vector3 shake = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f),
                    (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f), 0f) * (2f * maxShakeOffset * amount);
                transform.position += transform.TransformVector(shake);
                _trauma = Mathf.Max(0f, _trauma - shakeDecay * Time.deltaTime);
            }
        }

        private void ResolveTarget()
        {
            if (PlayerController.Instance != null) target = PlayerController.Instance.transform;
        }

        // ------------------------------------------------------------- публичное API

        public void SetFollow()
        {
            _mode = Mode.Follow;
            _staticOverridesView = false;
        }

        /// <summary>Статичный план по якорю: камера встаёт в позицию/поворот этого Transform.</summary>
        public void SetStatic(Transform anchor, float fovOverride = 0f)
        {
            if (anchor == null) return;
            _mode = Mode.Static;
            _staticPosition = anchor.position;
            _staticRotation = anchor.rotation;
            _staticFov = fovOverride > 0f ? fovOverride : (_cam != null ? _cam.fieldOfView : 35f);
            _staticOverridesView = true;
        }

        public void SetStatic(Vector3 position, Quaternion rotation, float fov)
        {
            _mode = Mode.Static;
            _staticPosition = position;
            _staticRotation = rotation;
            _staticFov = fov;
            _staticOverridesView = true;
        }

        /// <summary>Добавить тряску камере (0..1). Безопасно вызывать, даже если камеры нет.</summary>
        public static void Shake(float amount)
        {
            if (Instance != null) Instance._trauma = Mathf.Clamp01(Instance._trauma + amount);
        }
    }
}
