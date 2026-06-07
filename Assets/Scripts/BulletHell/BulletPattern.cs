using UnityEngine;

namespace Cybershi
{
    public enum BulletPatternType
    {
        Aimed,   // одиночный точно в цель
        Fan,     // веер в направлении цели
        Ring,    // кольцо на 360°
        Spiral   // кольцо, проворачивающееся с каждым залпом
    }

    /// <summary>
    /// Паттерн стрельбы в духе Touhou — данные для <see cref="BulletEmitter"/>.
    /// Create → Cybershi → Bullet Pattern.
    /// </summary>
    [CreateAssetMenu(menuName = "Cybershi/Bullet Pattern", fileName = "BulletPattern")]
    public class BulletPattern : ScriptableObject
    {
        public BulletPatternType type = BulletPatternType.Fan;
        public GameObject bulletPrefab;

        [Tooltip("Число пуль в залпе (для Aimed игнорируется).")]
        public int count = 12;
        [Tooltip("Угол веера (для Fan).")]
        public float spreadAngle = 90f;
        public float bulletSpeed = 9f;
        public float damage = 8f;
        [Tooltip("Доворот кольца за залп (для Spiral), градусы.")]
        public float spinPerShot = 13f;
    }
}
