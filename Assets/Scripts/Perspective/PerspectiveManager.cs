using System;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Режим перспективы. Side2D — классический вид сбоку (движение в плоскости XY,
    /// ось Z заморожена). ThreeD — камера уходит за спину, открывается движение по XZ.
    /// </summary>
    public enum PerspectiveMode { Side2D, ThreeD }

    /// <summary>
    /// Набор параметров камеры для одного режима перспективы.
    /// </summary>
    [Serializable]
    public struct CameraView
    {
        [Tooltip("Смещение камеры относительно цели в режиме следования (Follow).")]
        public Vector3 offset;
        [Tooltip("Углы поворота камеры (Euler).")]
        public Vector3 lookEuler;
        [Tooltip("Угол обзора. Большое расстояние + маленький FOV ≈ ортографика (для вида сбоку).")]
        public float fieldOfView;
    }

    /// <summary>
    /// Управляет текущей перспективой игры и тем, как должна стоять камера.
    /// Сам по себе НЕ двигает камеру и НЕ трогает физику — он лишь хранит целевой режим:
    ///   • <see cref="PlayerController"/> читает <see cref="CurrentMode"/> и выбирает схему движения
    ///     (в Side2D замораживает Z, в ThreeD — открывает плоскость XZ);
    ///   • <see cref="CameraController"/> читает <see cref="CurrentView"/> и плавно подстраивается.
    ///
    /// Сменить перспективу можно из кода (SetMode) или через <see cref="CameraZone"/> на уровне —
    /// например, влетел в арену → переход в 3D.
    /// </summary>
    public class PerspectiveManager : MonoBehaviour
    {
        public static PerspectiveManager Instance { get; private set; }

        [SerializeField] private PerspectiveMode startMode = PerspectiveMode.Side2D;

        [Header("Камера: вид сбоку (2D)")]
        public CameraView side2DView = new CameraView
        {
            offset = new Vector3(0f, 1.5f, -18f),
            lookEuler = new Vector3(0f, 0f, 0f),
            fieldOfView = 35f
        };

        [Header("Камера: вид 3D")]
        public CameraView threeDView = new CameraView
        {
            offset = new Vector3(0f, 6f, -10f),
            lookEuler = new Vector3(28f, 0f, 0f),
            fieldOfView = 60f
        };

        public PerspectiveMode CurrentMode { get; private set; }
        public event Action<PerspectiveMode> ModeChanged;

        /// <summary>Целевые параметры камеры для текущего режима.</summary>
        public CameraView CurrentView => CurrentMode == PerspectiveMode.ThreeD ? threeDView : side2DView;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            CurrentMode = startMode;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetMode(PerspectiveMode mode)
        {
            if (mode == CurrentMode) return;
            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }

        public void Toggle()
        {
            SetMode(CurrentMode == PerspectiveMode.Side2D ? PerspectiveMode.ThreeD : PerspectiveMode.Side2D);
        }
    }
}
