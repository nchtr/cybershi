using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Трассер хитскан-выстрела: растянутая полоска от дула до точки попадания, быстро гаснет.
    /// Спавнится через пул; вызвать <see cref="Show"/> сразу после Spawn.
    /// Плейсхолдер — замените спрайт у дочернего Visual на свой.
    /// </summary>
    public class TracerEffect : MonoBehaviour, IPoolable
    {
        public float lifetime = 0.08f;
        public float thickness = 0.08f;

        private float _timer;
        private SpriteRenderer _sr;
        private Color _baseColor;

        private void Awake()
        {
            _sr = GetComponentInChildren<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        public void Show(Vector3 from, Vector3 to)
        {
            Vector3 mid = (from + to) * 0.5f;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            transform.position = mid;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            transform.localScale = new Vector3(Mathf.Max(0.01f, len), thickness, 1f);
        }

        public void OnSpawned()
        {
            _timer = 0f;
            if (_sr != null) _sr.color = _baseColor;
        }

        public void OnDespawned() { }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_sr != null)
            {
                var c = _baseColor;
                c.a = _baseColor.a * (1f - _timer / lifetime);
                _sr.color = c;
            }
            if (_timer >= lifetime) PoolManager.Despawn(gameObject);
        }
    }
}
