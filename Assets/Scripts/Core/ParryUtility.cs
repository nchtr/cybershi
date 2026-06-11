using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Общая логика парирования и выбора приоритетной цели.
    /// Парировать можно ПОДСВЕЧЕННЫЕ (parryable) вражеские снаряды — рукопашной атакой
    /// или рывком. Спарированный снаряд переходит игроку, получает огромный множитель урона
    /// и самонаводится на приоритетного врага. Успешное парирование даёт хит-стоп и стиль.
    /// </summary>
    public static class ParryUtility
    {
        private static readonly Collider[] _overlap = new Collider[32];

        /// <summary>
        /// Попытаться спарировать все parryable-снаряды в радиусе. Возвращает число спарированных.
        /// fallbackDir — куда отражать, если врагов для самонаведения не нашлось.
        /// </summary>
        public static int TryParry(Vector3 center, float radius, Vector3 fallbackDir, GameObject newOwner)
        {
            int count = 0;
            var list = Projectile.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                if (p == null || !p.Parryable || p.OwnerFaction != Faction.Enemy) continue;
                if ((p.transform.position - center).sqrMagnitude > radius * radius) continue;

                var target = FindPriorityEnemy(p.transform.position, 40f);
                p.Parry(newOwner, target, fallbackDir);
                count++;
            }

            if (count > 0)
            {
                HitStop.Do(0.08f);
                if (StyleSystem.Instance != null) StyleSystem.Instance.RegisterParry(count);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.Parry, center);
                CameraController.Shake(0.25f);
            }
            return count;
        }

        /// <summary>
        /// До maxCount РАЗНЫХ приоритетных целей в радиусе, по убыванию приоритета
        /// (макс. здоровье, затем близость). Для сплит-рикошета монеток.
        /// </summary>
        public static List<Health> FindPriorityEnemies(Vector3 from, float radius, int maxCount)
        {
            var result = new List<Health>(maxCount);
            int n = Physics.OverlapSphereNonAlloc(from, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _overlap[i] != null ? _overlap[i].GetComponentInParent<Health>() : null;
                if (h == null || !h.IsAlive || h.Faction != Faction.Enemy) continue;
                if (!result.Contains(h)) result.Add(h);
            }
            result.Sort((a, b) =>
            {
                int byHp = b.Max.CompareTo(a.Max);
                if (byHp != 0) return byHp;
                float da = (a.transform.position - from).sqrMagnitude;
                float db = (b.transform.position - from).sqrMagnitude;
                return da.CompareTo(db);
            });
            if (result.Count > maxCount) result.RemoveRange(maxCount, result.Count - maxCount);
            return result;
        }

        /// <summary>
        /// Приоритетная цель в радиусе: живой враг с НАИБОЛЬШИМ максимальным здоровьем
        /// (босс приоритетнее рядового), при равенстве — ближайший. Может вернуть null.
        /// </summary>
        public static Health FindPriorityEnemy(Vector3 from, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(from, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestHp = -1f;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _overlap[i] != null ? _overlap[i].GetComponentInParent<Health>() : null;
                if (h == null || !h.IsAlive || h.Faction != Faction.Enemy) continue;
                float d = (h.transform.position - from).sqrMagnitude;
                if (h.Max > bestHp || (Mathf.Approximately(h.Max, bestHp) && d < bestDist))
                {
                    best = h;
                    bestHp = h.Max;
                    bestDist = d;
                }
            }
            return best;
        }
    }
}
