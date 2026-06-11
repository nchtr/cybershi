using UnityEngine;

namespace Cybershi
{
    /// <summary>Тип основного огня.</summary>
    public enum WeaponKind
    {
        Projectile, // физические снаряды (дробовик, гвоздомёт)
        Hitscan,    // мгновенный луч без снаряда (револьвер)
        Melee       // ближний бой по радиусу (ракетка)
    }

    /// <summary>Альтернативный огонь (ПКМ).</summary>
    public enum AltFireKind
    {
        None,
        Coin,   // монетка: хитскан рикошетит от неё с автонаводкой и доп. уроном
        Pump,   // накачка дробовика: больше дроби; перекачка — взрыв на месте игрока
        Magnet, // магнит: притягивает гвозди
        Ball    // мячик для пинг-понга (отбивается ближней атакой)
    }

    /// <summary>
    /// Описание одного оружия — чистые данные (ScriptableObject). Логика — в WeaponController.
    /// Create → Cybershi → Weapon.
    /// </summary>
    [CreateAssetMenu(menuName = "Cybershi/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        public string displayName = "Weapon";
        public WeaponKind kind = WeaponKind.Projectile;

        [Header("Основной огонь")]
        public float damage = 12f;
        [Tooltip("Выстрелов в секунду.")]
        public float fireRate = 5f;
        [Tooltip("Авто (зажать ЛКМ) или полуавтомат (клик).")]
        public bool automatic = false;
        public float knockback = 0f;

        [Header("Снаряды (kind = Projectile)")]
        public GameObject projectilePrefab;
        public float projectileSpeed = 28f;
        [Tooltip("Снарядов за выстрел (дробовик > 1).")]
        public int pelletsPerShot = 1;
        [Tooltip("Полный угол разброса, градусы.")]
        public float spreadAngle = 0f;
        [Tooltip("Случайный разброс дробинок внутри конуса (дробовик) вместо равномерного веера.")]
        public bool randomSpread = false;

        [Header("Хитскан (kind = Hitscan)")]
        public float hitscanRange = 70f;
        public GameObject tracerPrefab;
        public GameObject hitEffectPrefab;
        [Tooltip("Разрешить «мощный выстрел» от полного заряда грейза: пробивает все цели.")]
        public bool allowPowerShot = false;
        public float powerShotDamageMult = 3f;

        [Header("Ближний бой (kind = Melee)")]
        [Tooltip("Радиус поражения взмаха.")]
        public float meleeRange = 2.6f;
        [Tooltip("Взмах может парировать подсвеченные снаряды.")]
        public bool meleeCanParry = true;
        [Tooltip("Импульс, добавляемый мячику при отбивании.")]
        public float meleeBallImpulse = 14f;

        [Header("Боезапас (0 = бесконечный)")]
        public int maxAmmo = 0;
        public int ammoPerShot = 1;
        [Tooltip("Восстановление патронов в секунду (само по себе).")]
        public float ammoRegenPerSecond = 0f;

        [Header("Модификатор урона по дистанции")]
        [Tooltip("Множитель урона в упор (дистанция ≤ closeRange).")]
        public float closeBonusMult = 1f;
        public float closeRange = 3f;
        [Tooltip("С этой дистанции урон начинает спадать…")]
        public float falloffStart = 12f;
        [Tooltip("…и к этой дистанции достигает минимума.")]
        public float falloffEnd = 28f;
        [Range(0f, 1f)] public float farMinMult = 0.45f;

        [Header("Вампиризм")]
        [Tooltip("Убийство этим оружием в пределах lifestealRange выбивает частицы здоровья.")]
        public bool lifestealOnKill = false;
        public float lifestealRange = 8f;

        [Header("Альтернативный огонь (ПКМ)")]
        public AltFireKind altFire = AltFireKind.None;
        [Tooltip("Префаб для Coin/Magnet/Ball.")]
        public GameObject altFirePrefab;
        public float altFireCooldown = 0.8f;
        public float altFireLaunchSpeed = 14f;
        [Tooltip("Pump: безопасное число накачек; следующая — взрыв.")]
        public int pumpMaxSafe = 3;
        public float pumpExplosionDamage = 45f;
        public float pumpExplosionRadius = 5f;
        public GameObject explosionEffectPrefab;

        [Header("Отдача камеры / эффекты / звук")]
        public float cameraShake = 0.1f;
        public GameObject muzzleEffectPrefab;
        public SoundId fireSound = SoundId.None;
        public SoundId altFireSound = SoundId.None;

        public float FireInterval => fireRate > 0f ? 1f / fireRate : 0.1f;

        /// <summary>
        /// Дистанционный модификатор урона: бонус в упор, плато, линейный спад до минимума.
        /// Дробовик: closeBonus > 1 и ранний спад; револьвер: ровный; гвоздомёт: лёгкий спад.
        /// </summary>
        public float DistanceDamageMult(float distance)
        {
            if (distance <= closeRange) return closeBonusMult;
            if (distance <= falloffStart)
            {
                // Плавный переход от бонуса в упор к номиналу.
                float t = Mathf.InverseLerp(closeRange, falloffStart, distance);
                return Mathf.Lerp(closeBonusMult, 1f, t);
            }
            if (distance >= falloffEnd) return farMinMult;
            return Mathf.Lerp(1f, farMinMult, Mathf.InverseLerp(falloffStart, falloffEnd, distance));
        }
    }
}
