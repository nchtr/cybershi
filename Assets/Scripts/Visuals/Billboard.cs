using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Разворачивает спрайт лицом к камере. В 2D-режиме (камера смотрит вдоль +Z)
    /// фактически не меняет ничего, но при переходе в 3D держит плоский спрайт читаемым.
    /// Вешается на дочерний "Visual" объект.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        public enum Mode { FaceCamera, YawOnly, Off }

        public Mode mode = Mode.FaceCamera;
        private Transform _cam;

        private void LateUpdate()
        {
            if (mode == Mode.Off) return;
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }

            if (mode == Mode.FaceCamera)
            {
                // Спрайт смотрит в ту же сторону, что и камера (плоскости параллельны).
                transform.rotation = Quaternion.LookRotation(_cam.forward, _cam.up);
            }
            else // YawOnly — поворот только вокруг вертикали
            {
                Vector3 dir = transform.position - _cam.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }
}
