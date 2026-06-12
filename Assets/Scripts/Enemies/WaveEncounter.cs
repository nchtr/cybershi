using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>Одна «пачка» спавна внутри волны: префаб врага и сколько штук.</summary>
    [Serializable]
    public class SpawnEntry
    {
        public GameObject enemyPrefab;
        [Min(1)] public int count = 1;
        [Tooltip("Помечает врага как босса — для отдельной полоски здоровья в HUD.")]
        public bool isBoss;
    }

    /// <summary>Одна волна: список пачек + задержка перед стартом.</summary>
    [Serializable]
    public class WaveDefinition
    {
        public string label = "Волна";
        public SpawnEntry[] spawns;
        [Tooltip("Задержка перед началом этой волны, сек.")]
        public float startDelay = 0.5f;
    }

    /// <summary>
    /// Арена с волнами врагов, запускаемая по триггеру. Последовательно спавнит волны:
    /// следующая начинается, когда все враги предыдущей мертвы. Последняя волна обычно — босс.
    /// На время боя фиксирует камеру (общий план арены) и держит боевую музыку.
    /// HUD читает прогресс через статическое свойство <see cref="Active"/>.
    ///
    /// Нужен Collider с isTrigger. Враги — это ПРЕФАБЫ (Instantiate), не из пула.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaveEncounter : MonoBehaviour
    {
        [Header("Описание")]
        public string encounterName = "Арена";
        public WaveDefinition[] waves;

        [Header("Где появляются враги")]
        [Tooltip("Точки спавна (по кругу). Если пусто — спавн у самой зоны со случайным разбросом.")]
        public Transform[] spawnPoints;
        public float fallbackSpawnRadius = 5f;

        [Header("Запуск")]
        public bool triggerOnPlayerEnter = true;
        public bool oneShot = true;
        public float timeBetweenWaves = 1.5f;

        [Header("Камера арены (необязательно)")]
        public bool lockCameraStatic = true;
        public Transform cameraAnchor;
        public float cameraFov = 45f;

        // ---- состояние / API для HUD ----
        public static WaveEncounter Active { get; private set; }
        public bool InProgress { get; private set; }
        /// <summary>Арена пройдена (все волны зачищены). Это читает <see cref="LevelExit"/>.</summary>
        public bool IsCompleted { get; private set; }
        public int CurrentWaveIndex { get; private set; }
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int AliveCount => _alive.Count;
        public string EncounterName => encounterName;
        public Health CurrentBoss { get; private set; }
        public string BossName { get; private set; } = "BOSS";

        public event Action Completed;

        private readonly List<Health> _alive = new();
        private bool _started;
        private int _spawnPointCursor;

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnPlayerEnter || _started) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            Begin();
        }

        /// <summary>Запустить арену вручную (если не по триггеру).</summary>
        public void Begin()
        {
            if (_started) return;
            _started = true;
            Active = this;

            // Боевая музыка на всё время арены (вне зависимости от агра отдельных врагов).
            if (CombatStateTracker.Instance != null) CombatStateTracker.Instance.EnterCombat(this);

            if (lockCameraStatic && cameraAnchor != null && CameraController.Instance != null)
                CameraController.Instance.SetStatic(cameraAnchor, cameraFov);

            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            InProgress = true;

            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    var wave = waves[i];
                    CurrentWaveIndex = i + 1;

                    if (wave.startDelay > 0f) yield return new WaitForSeconds(wave.startDelay);

                    SpawnWave(wave);

                    // Ждём, пока вся волна не будет уничтожена.
                    // Подчищаем «висящие» ссылки на случай, если враг пропал без события Died.
                    while (true)
                    {
                        _alive.RemoveAll(h => h == null);
                        if (_alive.Count == 0) break;
                        yield return null;
                    }

                    if (timeBetweenWaves > 0f) yield return new WaitForSeconds(timeBetweenWaves);
                }
            }

            Finish();
        }

        private void SpawnWave(WaveDefinition wave)
        {
            if (wave.spawns == null) return;
            foreach (var entry in wave.spawns)
            {
                if (entry == null || entry.enemyPrefab == null) continue;
                for (int n = 0; n < Mathf.Max(1, entry.count); n++)
                    SpawnOne(entry);
            }
        }

        private void SpawnOne(SpawnEntry entry)
        {
            Vector3 pos = NextSpawnPosition();
            var go = Instantiate(entry.enemyPrefab, pos, Quaternion.identity);

            var health = go.GetComponent<Health>();
            if (health == null) health = go.GetComponentInChildren<Health>();
            if (health == null)
            {
                Debug.LogWarning($"Cybershi: у врага {go.name} нет Health — волна не сможет его отследить.");
                return;
            }

            _alive.Add(health);
            health.Died += OnEnemyDied;

            if (entry.isBoss)
            {
                CurrentBoss = health;
                BossName = go.name.Replace("(Clone)", "").Trim();
            }
        }

        private Vector3 NextSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var t = spawnPoints[_spawnPointCursor % spawnPoints.Length];
                _spawnPointCursor++;
                if (t != null) return t.position;
            }
            Vector2 r = UnityEngine.Random.insideUnitCircle * fallbackSpawnRadius;
            return transform.position + new Vector3(r.x, Mathf.Abs(r.y) + 1f, 0f);
        }

        private void OnEnemyDied(Health h, DamageInfo info)
        {
            h.Died -= OnEnemyDied;
            _alive.Remove(h);
            if (CurrentBoss == h) CurrentBoss = null;
        }

        private void Finish()
        {
            InProgress = false;
            IsCompleted = true;
            CurrentBoss = null;

            if (CombatStateTracker.Instance != null) CombatStateTracker.Instance.ExitCombat(this);
            if (lockCameraStatic && CameraController.Instance != null) CameraController.Instance.SetFollow();

            Completed?.Invoke();
            if (Active == this) Active = null;

            if (!oneShot) _started = false;
        }

        private void OnDisable()
        {
            // Снимаем подписки, чтобы не словить вызовы на уничтоженном объекте.
            foreach (var h in _alive) if (h != null) h.Died -= OnEnemyDied;
            _alive.Clear();
            if (Active == this) Active = null;
            if (CombatStateTracker.Instance != null) CombatStateTracker.Instance.ExitCombat(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.15f);
            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
            if (spawnPoints != null)
            {
                Gizmos.color = Color.red;
                foreach (var p in spawnPoints) if (p != null) Gizmos.DrawWireSphere(p.position, 0.6f);
            }
        }
    }
}
