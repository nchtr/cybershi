using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Триггер-зона на уровне, меняющая поведение камеры и/или перспективу, когда в неё
    /// входит игрок. Примеры:
    ///   • вход на арену босса → камера фиксируется (Static), даёт общий план;
    ///   • динамичная секция → фикс камеры;
    ///   • особый коридор → переход в 3D.
    ///
    /// Нужен Collider с включённым isTrigger. Игрок определяется по <see cref="PlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZone : MonoBehaviour
    {
        public enum Action { StaticFraming, Follow }

        [Header("Камера")]
        public Action onEnter = Action.StaticFraming;
        [Tooltip("Куда встать камере в режиме Static (позиция+поворот этого объекта).")]
        public Transform staticAnchor;
        [Tooltip("FOV для статичного плана (0 — оставить текущий).")]
        public float staticFov = 0f;
        [Tooltip("Вернуть слежение (Follow) при выходе из зоны.")]
        public bool revertToFollowOnExit = true;

        [Header("Перспектива (необязательно)")]
        public bool changePerspective = false;
        public PerspectiveMode perspectiveOnEnter = PerspectiveMode.Side2D;
        public bool revertPerspectiveOnExit = false;
        public PerspectiveMode perspectiveOnExit = PerspectiveMode.Side2D;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;

            if (CameraController.Instance != null)
            {
                if (onEnter == Action.StaticFraming && staticAnchor != null)
                    CameraController.Instance.SetStatic(staticAnchor, staticFov);
                else
                    CameraController.Instance.SetFollow();
            }

            if (changePerspective && PerspectiveManager.Instance != null)
                PerspectiveManager.Instance.SetMode(perspectiveOnEnter);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;

            if (revertToFollowOnExit && CameraController.Instance != null)
                CameraController.Instance.SetFollow();

            if (changePerspective && revertPerspectiveOnExit && PerspectiveManager.Instance != null)
                PerspectiveManager.Instance.SetMode(perspectiveOnExit);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            if (staticAnchor != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(staticAnchor.position, 0.5f);
                Gizmos.DrawLine(staticAnchor.position, staticAnchor.position + staticAnchor.forward * 3f);
            }
        }
    }
}
