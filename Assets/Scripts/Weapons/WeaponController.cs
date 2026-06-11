using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Носит оружия, переключает их, исполняет основной (ЛКМ) и альтернативный (ПКМ) огонь.
    ///
    /// Виды основного огня (WeaponDefinition.kind):
    ///  • Hitscan — револьвер: мгновенный луч, трассер; попадание в МОНЕТКУ даёт рикошет с
    ///    автонаводкой на приоритетную цель и доп. уроном; при полном заряде грейза следующий
    ///    выстрел — МОЩНЫЙ (пробивает все цели, множитель урона);
    ///  • Projectile — дробовик (случайный разброс дробинок в конусе) и гвоздомёт
    ///    (ограниченный самовосстанавливающийся боезапас);
    ///  • Melee — ракетка: удар по радиусу, парирует подсвеченные снаряды и отбивает мячик.
    ///
    /// Альт-огонь (ПКМ): Coin (монетка), Pump (накачка дробовика; перекачка = взрыв на игроке),
    ///  Magnet (магнит для гвоздей), Ball (мячик).
    ///
    /// Урон умножается на модификатор свежести (StyleSystem) и дистанционный модификатор оружия.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Tooltip("Список доступных оружий (ассеты WeaponDefinition).")]
        public List<WeaponDefinition> weapons = new();

        [Tooltip("Точка вылета снарядов/луча (дуло).")]
        public Transform firePoint;

        public Faction ownerFaction = Faction.Player;
        public bool allowScrollSwitch = true;

        private PlayerAiming _aiming;
        private int _index;
        private float _cooldown;
        private float _altCooldown;
        private float[] _ammo;          // текущий боезапас по слотам (для maxAmmo > 0)
        private int _pump;              // накачка дробовика
        private readonly RaycastHit[] _scanHits = new RaycastHit[24];

        public WeaponDefinition Current => (weapons.Count > 0 && _index < weapons.Count) ? weapons[_index] : null;
        public int CurrentIndex => _index;
        public int PumpCount => _pump;

        /// <summary>Текущий боезапас оружия (или -1, если бесконечный) — для HUD.</summary>
        public int GetAmmo(int slot)
        {
            if (slot < 0 || slot >= weapons.Count || weapons[slot] == null) return -1;
            if (weapons[slot].maxAmmo <= 0) return -1;
            EnsureAmmo();
            return Mathf.FloorToInt(_ammo[slot]);
        }

        private void Awake()
        {
            _aiming = GetComponent<PlayerAiming>();
            EnsureAmmo();
        }

        private void EnsureAmmo()
        {
            if (_ammo != null && _ammo.Length == weapons.Count) return;
            _ammo = new float[Mathf.Max(1, weapons.Count)];
            for (int i = 0; i < weapons.Count; i++)
                _ammo[i] = weapons[i] != null ? weapons[i].maxAmmo : 0;
        }

        private void Update()
        {
            EnsureAmmo();
            RegenAmmo(Time.deltaTime);
            HandleSwitching();

            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (_altCooldown > 0f) _altCooldown -= Time.deltaTime;

            var weapon = Current;
            if (weapon == null) return;

            var input = InputReader.Instance;

            bool firing = weapon.automatic ? input.FireHeld : input.FirePressed;
            if (firing && _cooldown <= 0f && HasAmmo(weapon))
            {
                FirePrimary(weapon);
                ConsumeAmmo(weapon);
                _cooldown = weapon.FireInterval;
            }

            if (input.AltFirePressed && _altCooldown <= 0f && weapon.altFire != AltFireKind.None)
            {
                FireAlt(weapon);
                _altCooldown = weapon.altFireCooldown;
            }
        }

        // ------------------------------------------------------------- боезапас

        private void RegenAmmo(float dt)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w == null || w.maxAmmo <= 0 || w.ammoRegenPerSecond <= 0f) continue;
                _ammo[i] = Mathf.Min(w.maxAmmo, _ammo[i] + w.ammoRegenPerSecond * dt);
            }
        }

        private bool HasAmmo(WeaponDefinition w) =>
            w.maxAmmo <= 0 || _ammo[_index] >= w.ammoPerShot;

        private void ConsumeAmmo(WeaponDefinition w)
        {
            if (w.maxAmmo > 0) _ammo[_index] -= w.ammoPerShot;
        }

        // ------------------------------------------------------------- переключение

        private void HandleSwitching()
        {
            var input = InputReader.Instance;
            for (int i = 0; i < weapons.Count && i < 9; i++)
            {
                if (input.WeaponSlotPressed(i)) { Select(i); return; }
            }
            if (allowScrollSwitch)
            {
                int cycle = input.WeaponCycle;
                if (cycle > 0) Select((_index + 1) % Mathf.Max(1, weapons.Count));
                else if (cycle < 0) Select((_index - 1 + weapons.Count) % Mathf.Max(1, weapons.Count));
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

        // ------------------------------------------------------------- основной огонь

        private Vector3 AimDir()
        {
            Vector3 d = _aiming != null ? _aiming.AimDirection : transform.right;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
        }

        private Vector3 Origin() => firePoint != null ? firePoint.position : transform.position;

        private void FirePrimary(WeaponDefinition w)
        {
            switch (w.kind)
            {
                case WeaponKind.Hitscan: FireHitscan(w); break;
                case WeaponKind.Melee: MeleeSwing(w); break;
                default: FireProjectiles(w); break;
            }

            if (w.muzzleEffectPrefab != null)
                PoolManager.Spawn(w.muzzleEffectPrefab, Origin(), Quaternion.LookRotation(Vector3.forward, AimDir()));
            if (AudioManager.Instance != null && w.fireSound != SoundId.None)
                AudioManager.Instance.Play(w.fireSound, Origin());
            CameraController.Shake(w.cameraShake);

            if (StyleSystem.Instance != null) StyleSystem.Instance.RegisterFire(w);
        }

        // --- снаряды (дробовик / гвоздомёт) ---

        private void FireProjectiles(WeaponDefinition w)
        {
            if (w.projectilePrefab == null) return;

            float styleMult = StyleSystem.Instance != null ? StyleSystem.Instance.DamageMult(w) : 1f;

            // Накачка дробовика: больше дробинок на выстрел, затем сбрасывается.
            int pellets = Mathf.Max(1, w.pelletsPerShot);
            if (w.altFire == AltFireKind.Pump && _pump > 0)
            {
                pellets += w.pelletsPerShot * _pump;
                _pump = 0;
            }

            Vector3 baseDir = AimDir();
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = ApplySpread(baseDir, w, pellets, i);
                var go = PoolManager.Spawn(w.projectilePrefab, Origin(), Quaternion.identity);
                var proj = go != null ? go.GetComponent<Projectile>() : null;
                if (proj != null)
                {
                    proj.knockback = w.knockback;
                    proj.sourceWeapon = w;
                    proj.Launch(dir, ownerFaction, gameObject, w.projectileSpeed, w.damage * styleMult);
                }
            }
        }

        private Vector3 ApplySpread(Vector3 dir, WeaponDefinition w, int pellets, int index)
        {
            if (w.spreadAngle <= 0f) return dir;

            bool is3D = PerspectiveManager.Instance != null &&
                        PerspectiveManager.Instance.CurrentMode == PerspectiveMode.ThreeD;
            Vector3 axis = is3D ? Vector3.up : Vector3.forward;

            float angle;
            if (w.randomSpread)
            {
                // Дробинки летят под СЛУЧАЙНЫМ углом внутри конуса.
                angle = Random.Range(-0.5f, 0.5f) * w.spreadAngle;
            }
            else
            {
                float t = pellets > 1 ? (index / (float)(pellets - 1)) - 0.5f : 0f;
                float jitter = Random.Range(-0.5f, 0.5f) * (w.spreadAngle / Mathf.Max(1, pellets));
                angle = t * w.spreadAngle + jitter;
            }
            return Quaternion.AngleAxis(angle, axis) * dir;
        }

        // --- хитскан (револьвер) ---

        private void FireHitscan(WeaponDefinition w)
        {
            Vector3 origin = Origin();
            Vector3 dir = AimDir();

            // Мощный выстрел от полного заряда грейза: пробивает все цели.
            bool power = w.allowPowerShot && GrazeSystem.Instance != null && GrazeSystem.Instance.ConsumeFull();
            float styleMult = StyleSystem.Instance != null ? StyleSystem.Instance.DamageMult(w) : 1f;
            float baseDamage = w.damage * styleMult * (power ? w.powerShotDamageMult : 1f);

            if (power)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.PowerShot, origin);
                CameraController.Shake(0.45f);
            }

            HitscanRay(w, origin, dir, w.hitscanRange, baseDamage, power, allowRicochet: true);
        }

        /// <summary>
        /// Луч хитскана: триггеры ищутся специально (монетки), стены/цели — обычные коллайдеры.
        /// pierce — не останавливаться на целях (мощный выстрел).
        /// </summary>
        private void HitscanRay(WeaponDefinition w, Vector3 origin, Vector3 dir, float range,
            float damage, bool pierce, bool allowRicochet)
        {
            int n = Physics.RaycastNonAlloc(new Ray(origin, dir), _scanHits, range, ~0, QueryTriggerInteraction.Collide);
            SortHits(n);

            Vector3 end = origin + dir * range;
            for (int i = 0; i < n; i++)
            {
                var col = _scanHits[i].collider;
                if (col.transform.IsChildOf(transform)) continue;

                // Монетка: запуск цепочки рикошетов (монетка → монетка → … → цель/сплит).
                if (allowRicochet)
                {
                    var coin = col.GetComponent<Coin>();
                    if (coin != null)
                    {
                        end = _scanHits[i].point;
                        SpawnTracer(w, origin, end);
                        RicochetChain(w, coin, damage, pierce);
                        return;
                    }
                    if (col.isTrigger) continue; // прочие триггеры игнорируем
                }
                else if (col.isTrigger) continue;

                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg != null)
                {
                    if (dmg.Faction == ownerFaction) continue;

                    float dist = Vector3.Distance(origin, _scanHits[i].point);
                    var info = new DamageInfo(damage * w.DistanceDamageMult(dist), _scanHits[i].point,
                        _scanHits[i].normal, ownerFaction, gameObject, dir * w.knockback)
                    { Weapon = w };
                    dmg.TakeDamage(info);

                    if (w.hitEffectPrefab != null)
                        PoolManager.Spawn(w.hitEffectPrefab, _scanHits[i].point, Quaternion.identity);

                    if (!pierce)
                    {
                        SpawnTracer(w, origin, _scanHits[i].point);
                        return;
                    }
                    continue; // мощный выстрел летит дальше
                }

                // Стена.
                end = _scanHits[i].point;
                if (w.hitEffectPrefab != null)
                    PoolManager.Spawn(w.hitEffectPrefab, end, Quaternion.identity);
                break;
            }

            SpawnTracer(w, origin, end);
        }

        /// <summary>
        /// Цепочка рикошетов через монетки. Луч прыгает с монетки на ближайшую другую в радиусе
        /// её chainRadius, ПЕРЕМНОЖАЯ множители урона за каждое звено. С последней монетки:
        ///  • если в цепи была хотя бы одна СОЗРЕВШАЯ (провисевшая ≥ splitAge) — выстрел
        ///    РАЗДЕЛЯЕТСЯ на лучи по нескольким приоритетным целям (доля урона за луч);
        ///  • иначе — одиночный луч в приоритетную цель.
        /// </summary>
        private void RicochetChain(WeaponDefinition w, Coin first, float damage, bool pierce)
        {
            // Параметры сплита берём с первой монетки (все из одного префаба).
            int splitTargets = Mathf.Max(1, first.splitTargets);
            float splitFactor = first.splitDamageFactor;
            float searchRadius = first.ricochetSearchRadius;

            float dmg = damage;
            bool anyRipe = false;
            Coin current = first;
            Vector3 lastPos = first.transform.position;

            int safety = 16; // страховка от вырожденных циклов
            while (current != null && safety-- > 0)
            {
                dmg *= current.ricochetDamageMult;      // суммируем прибавку за звено
                anyRipe |= current.IsRipe;
                lastPos = current.transform.position;
                float chainR = current.chainRadius;
                current.Consume();                       // выводит монетку из Active

                if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.Ricochet, lastPos);

                var next = Coin.FindNearest(lastPos, chainR);
                if (next != null)
                    SpawnTracer(w, lastPos, next.transform.position);
                current = next;
            }

            // Терминал: огонь с последней монетки.
            if (anyRipe && splitTargets > 1)
            {
                var targets = ParryUtility.FindPriorityEnemies(lastPos, searchRadius, splitTargets);
                if (targets.Count > 0)
                {
                    foreach (var t in targets)
                    {
                        Vector3 dir = (t.transform.position - lastPos).normalized;
                        HitscanRay(w, lastPos, dir, 60f, dmg * splitFactor, pierce, allowRicochet: false);
                    }
                    CameraController.Shake(0.2f);
                    if (StyleSystem.Instance != null)
                        StyleSystem.Instance.AddStyle(20f * targets.Count, "РАЗДЕЛЕНИЕ!");
                    return;
                }
            }

            var single = ParryUtility.FindPriorityEnemy(lastPos, searchRadius);
            if (single != null)
            {
                Vector3 dir = (single.transform.position - lastPos).normalized;
                HitscanRay(w, lastPos, dir, 60f, dmg, pierce, allowRicochet: false);
            }
        }

        private void SpawnTracer(WeaponDefinition w, Vector3 from, Vector3 to)
        {
            if (w.tracerPrefab == null) return;
            var go = PoolManager.Spawn(w.tracerPrefab, (from + to) * 0.5f, Quaternion.identity);
            var tracer = go != null ? go.GetComponent<TracerEffect>() : null;
            if (tracer != null) tracer.Show(from, to);
        }

        private void SortHits(int n)
        {
            for (int i = 1; i < n; i++)
            {
                var key = _scanHits[i];
                int j = i - 1;
                while (j >= 0 && _scanHits[j].distance > key.distance)
                {
                    _scanHits[j + 1] = _scanHits[j];
                    j--;
                }
                _scanHits[j + 1] = key;
            }
        }

        // --- ближний бой (ракетка) ---

        private static readonly Collider[] _meleeOverlap = new Collider[16];

        private void MeleeSwing(WeaponDefinition w)
        {
            Vector3 aim = AimDir();
            Vector3 center = transform.position + aim * (w.meleeRange * 0.6f);
            float radius = w.meleeRange * 0.75f;
            float styleMult = StyleSystem.Instance != null ? StyleSystem.Instance.DamageMult(w) : 1f;

            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.MeleeSwing, center);

            // Урон врагам в зоне взмаха.
            int n = Physics.OverlapSphereNonAlloc(center, radius, _meleeOverlap, ~0, QueryTriggerInteraction.Ignore);
            bool hitSomething = false;
            for (int i = 0; i < n; i++)
            {
                var dmg = _meleeOverlap[i] != null ? _meleeOverlap[i].GetComponentInParent<IDamageable>() : null;
                if (dmg == null || dmg.Faction == ownerFaction) continue;
                Vector3 point = _meleeOverlap[i].transform.position;
                var info = new DamageInfo(w.damage * styleMult, point, -aim, ownerFaction, gameObject, aim * w.knockback)
                { Weapon = w };
                dmg.TakeDamage(info);
                hitSomething = true;
            }
            if (hitSomething && AudioManager.Instance != null)
                AudioManager.Instance.Play(SoundId.MeleeHit, center);

            // Парирование подсвеченных снарядов.
            if (w.meleeCanParry)
                ParryUtility.TryParry(center, radius + 0.4f, aim, gameObject);

            // Отбить мячик (только ближняя атака умеет это).
            for (int i = PongBall.Active.Count - 1; i >= 0; i--)
            {
                var ball = PongBall.Active[i];
                if (ball == null) continue;
                if ((ball.transform.position - center).sqrMagnitude <= radius * radius)
                {
                    ball.Smash(aim, w.meleeBallImpulse);
                    HitStop.Do(0.05f);
                }
            }
        }

        // ------------------------------------------------------------- альт-огонь (ПКМ)

        private void FireAlt(WeaponDefinition w)
        {
            Vector3 origin = Origin();
            Vector3 aim = AimDir();

            switch (w.altFire)
            {
                case AltFireKind.Coin:
                {
                    if (w.altFirePrefab == null) return;
                    var go = PoolManager.Spawn(w.altFirePrefab, origin, Quaternion.identity);
                    var coin = go != null ? go.GetComponent<Coin>() : null;
                    if (coin != null)
                        coin.Toss(aim * w.altFireLaunchSpeed + Vector3.up * 6f);
                    PlayAlt(w, SoundId.CoinToss, origin);
                    break;
                }

                case AltFireKind.Pump:
                {
                    if (_pump < w.pumpMaxSafe)
                    {
                        _pump++;
                        PlayAlt(w, SoundId.PumpReload, origin);
                    }
                    else
                    {
                        // Перекачка: взрыв на месте игрока. Бьёт всех, включая самого игрока.
                        PumpExplosion(w);
                        _pump = 0;
                    }
                    break;
                }

                case AltFireKind.Magnet:
                {
                    if (w.altFirePrefab == null) return;
                    var go = PoolManager.Spawn(w.altFirePrefab, origin, Quaternion.identity);
                    var magnet = go != null ? go.GetComponent<NailMagnet>() : null;
                    if (magnet != null)
                        magnet.Launch(aim * w.altFireLaunchSpeed + Vector3.up * 3f);
                    PlayAlt(w, SoundId.MagnetDeploy, origin);
                    break;
                }

                case AltFireKind.Ball:
                {
                    if (w.altFirePrefab == null) return;
                    var go = PoolManager.Spawn(w.altFirePrefab, origin + aim * 0.8f, Quaternion.identity);
                    var ball = go != null ? go.GetComponent<PongBall>() : null;
                    if (ball != null)
                        ball.Launch(aim * w.altFireLaunchSpeed);
                    PlayAlt(w, SoundId.BallLaunch, origin);
                    break;
                }
            }
        }

        private void PlayAlt(WeaponDefinition w, SoundId fallback, Vector3 pos)
        {
            var id = w.altFireSound != SoundId.None ? w.altFireSound : fallback;
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id, pos);
        }

        private void PumpExplosion(WeaponDefinition w)
        {
            Vector3 pos = transform.position;
            if (w.explosionEffectPrefab != null)
                PoolManager.Spawn(w.explosionEffectPrefab, pos, Quaternion.identity);
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.Explosion, pos);
            CameraController.Shake(0.8f);
            HitStop.Do(0.07f);

            int n = Physics.OverlapSphereNonAlloc(pos, w.pumpExplosionRadius, _meleeOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var dmg = _meleeOverlap[i] != null ? _meleeOverlap[i].GetComponentInParent<IDamageable>() : null;
                if (dmg == null) continue;
                Vector3 point = _meleeOverlap[i].transform.position;
                Vector3 push = (point - pos).normalized * 14f;
                // Faction.Neutral — взрыв ранит и игрока, и врагов.
                dmg.TakeDamage(new DamageInfo(w.pumpExplosionDamage, point, Vector3.up, Faction.Neutral, gameObject, push)
                { Weapon = w });
            }
        }

        // ------------------------------------------------------------- статус для HUD

        /// <summary>Строка состояния текущего оружия для HUD (патроны/накачка/заряд).</summary>
        public string GetStatusText()
        {
            var w = Current;
            if (w == null) return "";
            var sb = new StringBuilder();

            if (w.maxAmmo > 0)
                sb.Append(' ').Append(GetAmmo(_index)).Append('/').Append(w.maxAmmo);

            if (w.altFire == AltFireKind.Pump && _pump > 0)
            {
                sb.Append("  ");
                for (int i = 0; i < _pump; i++) sb.Append('▲');
                if (_pump >= w.pumpMaxSafe) sb.Append(" ОПАСНО!");
            }

            if (w.allowPowerShot && GrazeSystem.Instance != null && GrazeSystem.Instance.IsFull)
                sb.Append("  ★ЗАРЯЖЕН");

            return sb.ToString();
        }
    }
}
