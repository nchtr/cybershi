using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Описание одного оружия — чистые данные (ScriptableObject), без логики.
    /// Создать новое оружие: Create → Cybershi → Weapon, заполнить поля, добавить в список
    /// <see cref="WeaponController.weapons"/>. Логику стрельбы выполняет контроллер.
    /// </summary>
    [CreateAssetMenu(menuName = "Cybershi/Weapon", fileName = "Weapon")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        public string displayName = "Weapon";

        [Header("Снаряд")]
        public GameObject projectilePrefab;
        public float damage = 12f;
        public float projectileSpeed = 28f;

        [Header("Стрельба")]
        [Tooltip("Выстрелов в секунду.")]
        public float fireRate = 5f;
        [Tooltip("Снарядов за один выстрел (дробовик > 1).")]
        public int pelletsPerShot = 1;
        [Tooltip("Полный угол разброса в градусах.")]
        public float spreadAngle = 0f;
        [Tooltip("Авто (зажать ЛКМ) или полуавтомат (клик).")]
        public bool automatic = false;
        public float knockback = 0f;

        [Header("Отдача камеры / эффекты / звук")]
        public float cameraShake = 0.1f;
        public GameObject muzzleEffectPrefab;
        public SoundId fireSound = SoundId.None;

        public float FireInterval => fireRate > 0f ? 1f / fireRate : 0.1f;
    }
}
