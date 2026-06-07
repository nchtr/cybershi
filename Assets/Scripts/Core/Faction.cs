using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Кто кому враг. Снаряды бьют только по чужой фракции.
    /// </summary>
    public enum Faction
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2
    }

    /// <summary>
    /// Пакет информации об одном попадании. Передаётся в <see cref="IDamageable.TakeDamage"/>.
    /// Структура (value type) — не порождает мусор в GC, что важно для буллет-хелла.
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;        // точка попадания в мире
        public Vector3 Normal;       // нормаль поверхности в точке попадания
        public Vector3 Knockback;    // импульс отбрасывания (мир)
        public GameObject Source;    // кто нанёс урон (оружие/враг)
        public Faction SourceFaction;

        public DamageInfo(float amount, Vector3 point, Vector3 normal, Faction sourceFaction, GameObject source = null, Vector3 knockback = default)
        {
            Amount = amount;
            Point = point;
            Normal = normal;
            SourceFaction = sourceFaction;
            Source = source;
            Knockback = knockback;
        }
    }

    /// <summary>
    /// Всё, что можно ранить (игрок, враги, разрушаемые объекты), реализует этот интерфейс.
    /// </summary>
    public interface IDamageable
    {
        Faction Faction { get; }
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
