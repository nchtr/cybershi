using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Носит набор оружий (<see cref="WeaponDefinition"/>), переключает их и стреляет.
    /// Направление берёт из <see cref="PlayerAiming"/>. Снаряды спавнятся через пул,
    /// им проставляется фракция владельца (Player) — так свои не получают урон.
    ///
    /// Управление: ЛКМ — огонь (зажать для авто-оружия), 1..N или колесо — смена оружия.
    /// Боезапас бесконечный (как у V1) — при желании легко добавить патроны.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Tooltip("Список доступных оружий. Заполняется ассетами WeaponDefinition.")]
        public List<WeaponDefinition> weapons = new();

        [Tooltip("Точка вылета снарядов (дуло).")]
        public Transform firePoint;

        [Tooltip("Фракция владельца. Для игрока — Player.")]
        public Faction ownerFaction = Faction.Player;

        public KeyCode fireKey = KeyCode.Mouse0;
        public bool allowScrollSwitch = true;

        private PlayerAiming _aiming;
        private int _index;
        private float _cooldown;

        public WeaponDefinition Current => (weapons.Count > 0 && _index < weapons.Count) ? weapons[_index] : null;
        public int CurrentIndex => _index;

        private void Awake()
        {
            _aiming = GetComponent<PlayerAiming>();
        }

        private void Update()
        {
            HandleSwitching();

            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            var weapon = Current;
            if (weapon == null) return;

            bool firing = weapon.automatic ? Input.GetKey(fireKey) : Input.GetKeyDown(fireKey);
            if (firing && _cooldown <= 0f)
            {
                Fire(weapon);
                _cooldown = weapon.FireInterval;
            }
        }

        private void HandleSwitching()
        {
            // Цифры 1..9
            for (int i = 0; i < weapons.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Select(i);
                    return;
                }
            }

            if (allowScrollSwitch)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll > 0.01f) Select((_index + 1) % Mathf.Max(1, weapons.Count));
                else if (scroll < -0.01f) Select((_index - 1 + weapons.Count) % Mathf.Max(1, weapons.Count));
            }
        }

        public void Select(int index)
        {
            if (weapons.Count == 0) return;
            index = Mathf.Clamp(index, 0, weapons.Count - 1);
            if (index == _index) return;
            _index = index;
            _cooldown = 0f;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.WeaponSwitch, transform.position);
        }

        private void Fire(WeaponDefinition weapon)
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 baseDir = _aiming != null ? _aiming.AimDirection : transform.right;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.right;

            if (weapon.projectilePrefab != null)
            {
                int pellets = Mathf.Max(1, weapon.pelletsPerShot);
                for (int i = 0; i < pellets; i++)
                {
                    Vector3 dir = ApplySpread(baseDir, weapon.spreadAngle, pellets, i);
                    var go = PoolManager.Spawn(weapon.projectilePrefab, origin, Quaternion.identity);
                    var proj = go != null ? go.GetComponent<Projectile>() : null;
                    if (proj != null)
                    {
                        proj.knockback = weapon.knockback;
                        proj.Launch(dir, ownerFaction, gameObject, weapon.projectileSpeed, weapon.damage);
                    }
                }
            }

            if (weapon.muzzleEffectPrefab != null)
                PoolManager.Spawn(weapon.muzzleEffectPrefab, origin, Quaternion.LookRotation(Vector3.forward, baseDir));

            if (AudioManager.Instance != null && weapon.fireSound != SoundId.None)
                AudioManager.Instance.Play(weapon.fireSound, origin);

            CameraController.Shake(weapon.cameraShake);
        }

        /// <summary>Раскидывает дробь равномерно по дуге + лёгкий случайный джиттер.</summary>
        private Vector3 ApplySpread(Vector3 dir, float spreadAngle, int pellets, int index)
        {
            if (spreadAngle <= 0f) return dir;

            float t = pellets > 1 ? (index / (float)(pellets - 1)) - 0.5f : 0f; // -0.5..0.5
            float jitter = Random.Range(-0.5f, 0.5f) * (spreadAngle / Mathf.Max(1, pellets));
            float angle = t * spreadAngle + jitter;

            // Поворот вокруг оси Z (плоскость игры). В 3D — вокруг мировой вертикали.
            bool is3D = PerspectiveManager.Instance != null &&
                        PerspectiveManager.Instance.CurrentMode == PerspectiveMode.ThreeD;
            Vector3 axis = is3D ? Vector3.up : Vector3.forward;
            return Quaternion.AngleAxis(angle, axis) * dir;
        }
    }
}
