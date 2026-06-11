using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Счётчик стиля и комбо (в духе ULTRAKILL).
    ///
    /// Очки стиля растут за: убийства (бонусы за убийство В ВОЗДУХЕ, В УПОР, мульти-килл),
    /// грейз, парирование и быструю СМЕНУ ОРУЖИЯ (урон разными стволами подряд).
    /// Очки постоянно затухают → ранг (D…SS) держится только активной игрой.
    ///
    /// «Свежесть» оружия: стрельба одним и тем же стволом снижает его свежесть, простаивающие
    /// стволы её восстанавливают. Свежесть даёт МОДИФИКАТОР УРОНА (DamageMult: 0.6…1.0) и
    /// множитель получаемого стиля — это вынуждает крутить весь арсенал.
    ///
    /// Здесь же — хит-стоп при убийстве крупных врагов.
    /// </summary>
    public class StyleSystem : MonoBehaviour
    {
        public static StyleSystem Instance { get; private set; }

        [Header("Очки и ранги")]
        [Tooltip("Границы рангов: D | C | B | A | S | SS")]
        public float[] rankThresholds = { 0f, 100f, 250f, 450f, 700f, 1000f };
        public string[] rankNames = { "D", "C", "B", "A", "S", "SS" };
        [Tooltip("Базовое затухание очков в секунду (растёт с рангом).")]
        public float baseDecay = 12f;
        public float decayPerRank = 10f;
        [Tooltip("Пауза затухания после получения очков, сек.")]
        public float decayGrace = 1.2f;

        [Header("Награды")]
        public float killStyle = 50f;
        public float aerialKillBonus = 35f;
        public float closeKillBonus = 25f;
        public float closeKillRange = 4f;
        public float multiKillBonus = 30f;
        public float multiKillWindow = 1.6f;
        public float swapBonus = 25f;
        public float swapWindow = 3f;

        [Header("Свежесть оружия")]
        [Tooltip("Сколько свежести съедает один выстрел.")]
        public float freshnessPerShot = 0.07f;
        [Tooltip("Восстановление свежести незадействованных стволов в секунду.")]
        public float freshnessRegen = 0.25f;
        [Range(0f, 1f)] public float minFreshness = 0.15f;
        [Tooltip("Урон = базовый * (damageFloor + (1-damageFloor) * свежесть).")]
        [Range(0f, 1f)] public float damageFloor = 0.6f;

        [Header("Хит-стоп")]
        [Tooltip("Порог maxHealth врага, считающегося «крупным» (хит-стоп при убийстве).")]
        public float bigKillHealthThreshold = 150f;
        public float bigKillHitStop = 0.12f;

        // --- состояние ---
        private float _score;
        private float _graceTimer;
        private float _lastKillTime = -99f;
        private int _killChain;
        private WeaponDefinition _lastDamageWeapon;
        private float _lastDamageTime = -99f;
        private readonly Dictionary<WeaponDefinition, float> _freshness = new();
        private readonly List<WeaponDefinition> _known = new();

        private string _lastEvent = "";
        private float _lastEventTimer;

        public float Score => _score;
        public int RankIndex
        {
            get
            {
                int r = 0;
                for (int i = 0; i < rankThresholds.Length; i++)
                    if (_score >= rankThresholds[i]) r = i;
                return r;
            }
        }
        public string RankName => rankNames[Mathf.Clamp(RankIndex, 0, rankNames.Length - 1)];
        /// <summary>Заполнение полосы внутри текущего ранга (0..1) для HUD.</summary>
        public float RankProgress
        {
            get
            {
                int r = RankIndex;
                float lo = rankThresholds[r];
                float hi = r + 1 < rankThresholds.Length ? rankThresholds[r + 1] : lo * 1.5f + 200f;
                return Mathf.Clamp01((_score - lo) / Mathf.Max(1f, hi - lo));
            }
        }
        /// <summary>Последнее стилевое событие («ВОЗДУШНЫЙ!», «ПАРИРОВАНИЕ!») для HUD.</summary>
        public string LastEvent => _lastEventTimer > 0f ? _lastEvent : "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => Health.AnyDeath += OnAnyDeath;
        private void OnDisable() => Health.AnyDeath -= OnAnyDeath;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_graceTimer > 0f) _graceTimer -= dt;
            else if (_score > 0f)
                _score = Mathf.Max(0f, _score - (baseDecay + decayPerRank * RankIndex) * dt);

            if (_lastEventTimer > 0f) _lastEventTimer -= dt;

            // Восстановление свежести всех известных стволов, кроме последнего стрелявшего «сейчас».
            for (int i = 0; i < _known.Count; i++)
            {
                var w = _known[i];
                if (w == null) continue;
                float f = _freshness.TryGetValue(w, out var v) ? v : 1f;
                _freshness[w] = Mathf.Min(1f, f + freshnessRegen * dt);
            }
        }

        // ------------------------------------------------------------- публичное API

        public void AddStyle(float amount, string eventName = null)
        {
            if (amount <= 0f) return;
            _score += amount;
            _graceTimer = decayGrace;
            if (!string.IsNullOrEmpty(eventName))
            {
                _lastEvent = eventName;
                _lastEventTimer = 1.6f;
            }
        }

        public float GetFreshness(WeaponDefinition w)
        {
            if (w == null) return 1f;
            return _freshness.TryGetValue(w, out var v) ? v : 1f;
        }

        /// <summary>Модификатор урона оружия от свежести (0.6…1.0 по умолчанию).</summary>
        public float DamageMult(WeaponDefinition w)
        {
            return damageFloor + (1f - damageFloor) * GetFreshness(w);
        }

        /// <summary>Вызывается оружием при каждом выстреле/взмахе: тратит свежесть, даёт бонус смены.</summary>
        public void RegisterFire(WeaponDefinition w)
        {
            if (w == null) return;
            if (!_freshness.ContainsKey(w)) { _freshness[w] = 1f; _known.Add(w); }
            _freshness[w] = Mathf.Max(minFreshness, _freshness[w] - freshnessPerShot);

            // Бонус за быструю смену оружия: урон другим стволом вскоре после предыдущего.
            if (_lastDamageWeapon != null && _lastDamageWeapon != w &&
                Time.time - _lastDamageTime <= swapWindow)
            {
                AddStyle(swapBonus * GetFreshness(w), "СМЕНА ОРУЖИЯ");
            }
            _lastDamageWeapon = w;
            _lastDamageTime = Time.time;
        }

        public void RegisterGraze() => AddStyle(6f, "ГРЕЙЗ");
        public void RegisterParry(int count) => AddStyle(80f * count, "ПАРИРОВАНИЕ!");

        // ------------------------------------------------------------- убийства

        private void OnAnyDeath(Health victim, DamageInfo info)
        {
            if (victim == null || victim.Faction != Faction.Enemy) return;
            if (info.SourceFaction != Faction.Player) return;

            float gain = killStyle;
            string label = "УБИЙСТВО";

            var player = PlayerController.Instance;
            if (player != null)
            {
                if (!player.IsGrounded) { gain += aerialKillBonus; label = "ВОЗДУШНЫЙ!"; }
                float dist = Vector3.Distance(player.transform.position, victim.transform.position);
                if (dist <= closeKillRange) { gain += closeKillBonus; label = "В УПОР!"; }
            }

            // Цепочка мульти-килла.
            if (Time.time - _lastKillTime <= multiKillWindow) _killChain++;
            else _killChain = 1;
            _lastKillTime = Time.time;
            if (_killChain >= 2)
            {
                gain += multiKillBonus * (_killChain - 1);
                label = $"МУЛЬТИ x{_killChain}";
            }

            if (info.WasParried) { gain += 40f; label = "ВОЗВРАТ!"; }

            // Свежесть оружия масштабирует получаемый стиль.
            gain *= GetFreshness(info.Weapon);
            AddStyle(gain, label);

            // Хит-стоп на крупных целях.
            if (victim.Max >= bigKillHealthThreshold)
                HitStop.Do(bigKillHitStop);
        }
    }
}
