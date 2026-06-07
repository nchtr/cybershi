using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Выстреливает залп по заданному <see cref="BulletPattern"/>. Используется врагами/боссами
    /// для буллет-хелла. Пули — это те же <see cref="Projectile"/>, но обычно медленнее и крупнее,
    /// со своей фракцией владельца (Enemy → бьют игрока).
    /// </summary>
    public class BulletEmitter : MonoBehaviour
    {
        public BulletPattern pattern;
        [Tooltip("Фракция владельца пуль.")]
        public Faction ownerFaction = Faction.Enemy;
        public SoundId fireSound = SoundId.EnemyShoot;

        private float _spin;

        /// <summary>Выпустить залп. baseDir — направление на цель (для Aimed/Fan/начала Spiral).</summary>
        public void Emit(Vector3 baseDir)
        {
            if (pattern == null || pattern.bulletPrefab == null) return;

            Vector3 origin = transform.position;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.right;
            baseDir.Normalize();

            // Ось вращения: в 2D — Z, в 3D — мировая вертикаль.
            bool is3D = PerspectiveManager.Instance != null &&
                        PerspectiveManager.Instance.CurrentMode == PerspectiveMode.ThreeD;
            Vector3 axis = is3D ? Vector3.up : Vector3.forward;

            switch (pattern.type)
            {
                case BulletPatternType.Aimed:
                    Spawn(baseDir, origin);
                    break;

                case BulletPatternType.Fan:
                {
                    int n = Mathf.Max(1, pattern.count);
                    for (int i = 0; i < n; i++)
                    {
                        float t = n > 1 ? (i / (float)(n - 1)) - 0.5f : 0f;
                        float angle = t * pattern.spreadAngle;
                        Spawn(Quaternion.AngleAxis(angle, axis) * baseDir, origin);
                    }
                    break;
                }

                case BulletPatternType.Ring:
                {
                    int n = Mathf.Max(1, pattern.count);
                    for (int i = 0; i < n; i++)
                    {
                        float angle = i * (360f / n);
                        Spawn(Quaternion.AngleAxis(angle, axis) * baseDir, origin);
                    }
                    break;
                }

                case BulletPatternType.Spiral:
                {
                    int n = Mathf.Max(1, pattern.count);
                    for (int i = 0; i < n; i++)
                    {
                        float angle = i * (360f / n) + _spin;
                        Spawn(Quaternion.AngleAxis(angle, axis) * baseDir, origin);
                    }
                    _spin += pattern.spinPerShot;
                    break;
                }
            }

            if (AudioManager.Instance != null && fireSound != SoundId.None)
                AudioManager.Instance.Play(fireSound, origin);
        }

        private void Spawn(Vector3 dir, Vector3 origin)
        {
            var go = PoolManager.Spawn(pattern.bulletPrefab, origin, Quaternion.identity);
            var proj = go != null ? go.GetComponent<Projectile>() : null;
            if (proj != null)
                proj.Launch(dir, ownerFaction, gameObject, pattern.bulletSpeed, pattern.damage);
        }
    }
}
