using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Вампиризм на ближней дистанции: убийство врага В УПОР (любым оружием) или оружием
    /// с флагом lifesteal (дробовик) в его эффективной зоне выбивает из врага частицы
    /// здоровья (<see cref="HealOrb"/>), летящие к игроку.
    ///
    /// Анти-абьюз — «усталость вампиризма»: каждое поглощение орба копит усталость, которая
    /// линейно снижает эффективность лечения вплоть до нуля; усталость медленно спадает со
    /// временем. Нельзя бесконечно фармить здоровье — приходится дозировать агрессию.
    /// Вешается на игрока рядом с Health.
    /// </summary>
    public class Vampirism : MonoBehaviour
    {
        public static Vampirism Instance { get; private set; }

        [Header("Условия извлечения")]
        [Tooltip("Дистанция убийства «в упор» для любого оружия.")]
        public float closeKillRange = 4f;

        [Header("Орбы")]
        public GameObject healOrbPrefab;
        [Tooltip("HP врага на один орб (40 HP → 2 орба при значении 20).")]
        public float healthPerOrb = 20f;
        public int minOrbs = 1;
        public int maxOrbs = 5;

        [Header("Штраф (усталость)")]
        [Tooltip("Сколько усталости добавляет одно поглощение орба.")]
        public float fatiguePerOrb = 0.12f;
        [Tooltip("Спад усталости в секунду.")]
        public float fatigueDecay = 0.06f;

        /// <summary>Усталость 0..1. Эффективность лечения = 1 - усталость.</summary>
        public float Fatigue { get; private set; }
        public float Efficiency => Mathf.Clamp01(1f - Fatigue);

        private Health _health;

        private void Awake()
        {
            Instance = this;
            _health = GetComponent<Health>();
        }

        private void OnEnable() => Health.AnyDeath += OnAnyDeath;
        private void OnDisable() => Health.AnyDeath -= OnAnyDeath;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Fatigue > 0f)
                Fatigue = Mathf.Max(0f, Fatigue - fatigueDecay * Time.deltaTime);
        }

        private void OnAnyDeath(Health victim, DamageInfo info)
        {
            if (victim == null || victim.Faction != Faction.Enemy) return;
            if (info.SourceFaction != Faction.Player) return;
            if (healOrbPrefab == null) return;

            float dist = Vector3.Distance(transform.position, victim.transform.position);

            bool closeKill = dist <= closeKillRange;
            bool weaponSteal = info.Weapon != null && info.Weapon.lifestealOnKill &&
                               dist <= info.Weapon.lifestealRange;
            if (!closeKill && !weaponSteal) return;

            int orbs = Mathf.Clamp(Mathf.RoundToInt(victim.Max / Mathf.Max(1f, healthPerOrb)), minOrbs, maxOrbs);
            for (int i = 0; i < orbs; i++)
                PoolManager.Spawn(healOrbPrefab, victim.transform.position, Quaternion.identity);
        }

        /// <summary>Орб долетел до игрока: лечим с учётом усталости, копим усталость.</summary>
        public void ConsumeOrb(float baseHeal)
        {
            float heal = baseHeal * Efficiency;
            if (heal > 0.01f && _health != null)
            {
                _health.Heal(heal);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.Heal, transform.position);
            }
            Fatigue = Mathf.Min(1f, Fatigue + fatiguePerOrb);
        }
    }
}
