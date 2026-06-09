using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Прицеливание мышью. Проецирует курсор на игровую плоскость и выдаёт точку прицела
    /// и направление от дула к ней. Этим пользуются <see cref="WeaponController"/> (куда стрелять)
    /// и <see cref="PlayerController"/> (в какую сторону смотрит персонаж).
    /// </summary>
    public class PlayerAiming : MonoBehaviour
    {
        [Tooltip("Камера для луча. Если пусто — Camera.main.")]
        public Camera cam;
        [Tooltip("Точка вылета снарядов (дуло). Если пусто — позиция игрока.")]
        public Transform firePoint;

        /// <summary>Точка прицела в мире (на игровой плоскости).</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>Нормализованное направление от дула к точке прицела.</summary>
        public Vector3 AimDirection { get; private set; } = Vector3.right;

        private float _planeZ;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            _planeZ = transform.position.z;
        }

        private void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            // В 2D плоскость игры — Z = const. В 3D берём плоскость на высоте дула,
            // нормаль вверх, чтобы целиться по земле.
            bool is3D = PerspectiveManager.Instance != null &&
                        PerspectiveManager.Instance.CurrentMode == PerspectiveMode.ThreeD;

            Ray ray = cam.ScreenPointToRay(InputReader.Instance.PointerPosition);
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;

            Plane plane = is3D
                ? new Plane(Vector3.up, origin)
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ));

            if (plane.Raycast(ray, out float dist))
            {
                AimPoint = ray.GetPoint(dist);
                Vector3 dir = AimPoint - origin;
                if (!is3D) dir.z = 0f;
                if (dir.sqrMagnitude > 0.0001f) AimDirection = dir.normalized;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(AimPoint, 0.3f);
        }
    }
}
