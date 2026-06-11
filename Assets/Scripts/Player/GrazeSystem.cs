using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Механика «Graze»: награда за пролёт в миллиметрах от вражеских пуль.
    ///
    /// Два вида начисления:
    ///  • МГНОВЕННО — когда пуля впервые входит в грейз-радиус: +стиль, +порция энергии;
    ///  • СО ВРЕМЕНЕМ — пока пули остаются в грейз-радиусе, энергия капает каждую секунду
    ///    (чем больше пуль вокруг — тем быстрее, с потолком).
    ///
    /// Накопленная энергия (0..1) — заряд «мощного выстрела» револьвера: при полном заряде
    /// следующий выстрел пробивает все цели насквозь с множителем урона (см. WeaponController).
    /// Вешается на игрока.
    /// </summary>
    public class GrazeSystem : MonoBehaviour
    {
        public static GrazeSystem Instance { get; private set; }

        [Header("Радиус")]
        [Tooltip("Радиус грейза вокруг игрока (больше его хитбокса).")]
        public float grazeRadius = 1.8f;

        [Header("Награды")]
        [Tooltip("Энергия за первое касание пулей грейз-зоны (мгновенно).")]
        public float instantEnergy = 0.06f;
        [Tooltip("Энергия в секунду за каждую пулю, остающуюся в зоне (со временем).")]
        public float energyPerSecondPerBullet = 0.10f;
        [Tooltip("Сколько пуль одновременно может «капать» энергию.")]
        public int maxSimultaneous = 4;

        [Header("Эффект")]
        public GameObject grazeEffectPrefab; // вспышка на пуле при первом грейзе (опционально)

        /// <summary>Заряд мощного выстрела, 0..1.</summary>
        public float Energy { get; private set; }
        public bool IsFull => Energy >= 1f;
        /// <summary>Пуль в грейз-зоне прямо сейчас (для HUD).</summary>
        public int NearbyBullets { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Потратить полный заряд. true, если заряд был полон.</summary>
        public bool ConsumeFull()
        {
            if (!IsFull) return false;
            Energy = 0f;
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            float r2 = grazeRadius * grazeRadius;
            int nearby = 0;

            var list = Projectile.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || p.OwnerFaction != Faction.Enemy) continue;
                if ((p.transform.position - pos).sqrMagnitude > r2) continue;

                nearby++;

                // Мгновенная часть: каждая пуля засчитывается один раз.
                if (!p.Grazed)
                {
                    p.Grazed = true;
                    Energy = Mathf.Min(1f, Energy + instantEnergy);
                    if (StyleSystem.Instance != null) StyleSystem.Instance.RegisterGraze();
                    if (AudioManager.Instance != null) AudioManager.Instance.Play(SoundId.Graze, p.transform.position);
                    if (grazeEffectPrefab != null)
                        PoolManager.Spawn(grazeEffectPrefab, p.transform.position, Quaternion.identity);
                }
            }

            NearbyBullets = nearby;

            // Накопление со временем, пока пули рядом.
            if (nearby > 0)
            {
                int counted = Mathf.Min(nearby, maxSimultaneous);
                Energy = Mathf.Min(1f, Energy + energyPerSecondPerBullet * counted * dt);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, grazeRadius);
        }
    }
}
