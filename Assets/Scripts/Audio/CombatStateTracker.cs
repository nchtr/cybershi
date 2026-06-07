using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Глобальный "в бою / не в бою" трекер. Враги сообщают сюда, когда замечают игрока
    /// (<see cref="EnterCombat"/>) и когда теряют интерес или гибнут (<see cref="ExitCombat"/>).
    /// Пока хоть один враг "ведёт бой" — <see cref="InCombat"/> = true.
    ///
    /// Этим пользуется <see cref="DynamicMusicManager"/>, чтобы менять музыку.
    /// </summary>
    public class CombatStateTracker : MonoBehaviour
    {
        public static CombatStateTracker Instance { get; private set; }

        /// <summary>Срабатывает при смене состояния. Аргумент — новое значение InCombat.</summary>
        public event System.Action<bool> CombatStateChanged;

        // Используем множество источников: один и тот же враг не учитывается дважды,
        // а исчезновение врага (Destroy) автоматически вычищается чисткой ниже.
        private readonly HashSet<Object> _engaged = new();
        private bool _inCombat;

        public bool InCombat => _inCombat;
        public int EngagedCount => _engaged.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnterCombat(Object source)
        {
            if (source == null) return;
            _engaged.Add(source);
            Evaluate();
        }

        public void ExitCombat(Object source)
        {
            if (source == null) return;
            _engaged.Remove(source);
            Evaluate();
        }

        private void Update()
        {
            // Подстраховка: убираем уничтоженные объекты (Unity-null), которые забыли разрегистрироваться.
            _engaged.RemoveWhere(o => o == null);
            Evaluate();
        }

        private void Evaluate()
        {
            bool now = _engaged.Count > 0;
            if (now != _inCombat)
            {
                _inCombat = now;
                CombatStateChanged?.Invoke(now);
            }
        }
    }
}
