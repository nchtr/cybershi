using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Выход с уровня: триггер-зона, при входе игрока загружает следующую сцену
    /// (по порядку Build Settings) или конкретную по имени.
    ///
    /// Можно ЗАПЕРЕТЬ выход за аренами: укажите в <see cref="requiredEncounters"/> энкаунтеры —
    /// выход активируется только когда все они завершены (визуал, если задан, при этом
    /// перекрашивается из «заперто» в «открыто»).
    ///
    /// Сборка: пустой объект → BoxCollider (isTrigger) → LevelExit (+ при желании дочерний
    /// Visual со SpriteRenderer, привяжите его в поле visual).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("Имя следующей сцены. Пусто — следующая по индексу Build Settings.")]
        public string nextSceneName = "";

        [Tooltip("Выход откроется только после завершения всех этих арен (можно оставить пустым).")]
        public WaveEncounter[] requiredEncounters;

        [Header("Визуал (опционально)")]
        public SpriteRenderer visual;
        public Color lockedColor = new Color(0.5f, 0.2f, 0.2f);
        public Color openColor = new Color(0.3f, 1f, 0.5f);

        private bool _used;

        public bool IsOpen
        {
            get
            {
                if (requiredEncounters == null) return true;
                for (int i = 0; i < requiredEncounters.Length; i++)
                {
                    var e = requiredEncounters[i];
                    if (e != null && !e.IsCompleted) return false;
                }
                return true;
            }
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            if (visual != null)
                visual.color = IsOpen ? openColor : lockedColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_used || !IsOpen) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            _used = true;

            if (string.IsNullOrEmpty(nextSceneName)) SceneFlow.LoadNext();
            else SceneFlow.Load(nextSceneName);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsOpen ? new Color(0.3f, 1f, 0.5f, 0.25f) : new Color(1f, 0.3f, 0.3f, 0.25f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
        }
    }
}
