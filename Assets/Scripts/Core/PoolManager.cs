using System.Collections.Generic;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Объект, который хочет получать уведомления при выдаче/возврате из пула.
    /// Снаряды и эффекты реализуют это, чтобы сбрасывать своё состояние.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Маркер, который PoolManager добавляет на каждый созданный экземпляр,
    /// чтобы помнить, из какого префаба он сделан, и куда его вернуть.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledInstance : MonoBehaviour
    {
        public GameObject SourcePrefab;
    }

    /// <summary>
    /// Простой пул объектов. В буллет-хелле на экране бывают сотни снарядов —
    /// Instantiate/Destroy каждого кадра убил бы производительность и насорил бы в GC.
    /// Поэтому объекты переиспользуются: <see cref="Spawn"/> достаёт из пула или создаёт,
    /// <see cref="Despawn"/> прячет и возвращает обратно.
    ///
    /// Использование (всегда через эти статические методы, не Instantiate напрямую):
    ///   var go = PoolManager.Spawn(prefab, pos, rot);
    ///   PoolManager.Despawn(go);
    /// </summary>
    public static class PoolManager
    {
        private static readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("~PoolRoot");
                    Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
            }

            GameObject instance = null;
            // Пропускаем уже уничтоженные ссылки (например, при смене сцены).
            while (queue.Count > 0 && instance == null)
                instance = queue.Dequeue();

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                var marker = instance.GetComponent<PooledInstance>();
                if (marker == null) marker = instance.AddComponent<PooledInstance>();
                marker.SourcePrefab = prefab;
            }

            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawned();

            return instance;
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawned();

            instance.SetActive(false);

            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null || marker.SourcePrefab == null)
            {
                // Объект не из пула — просто уничтожаем.
                Object.Destroy(instance);
                return;
            }

            instance.transform.SetParent(Root, false);
            if (!_pools.TryGetValue(marker.SourcePrefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[marker.SourcePrefab] = queue;
            }
            queue.Enqueue(instance);
        }

        /// <summary>Очистить все пулы (например, перед загрузкой нового уровня).</summary>
        public static void Clear()
        {
            _pools.Clear();
        }
    }
}
