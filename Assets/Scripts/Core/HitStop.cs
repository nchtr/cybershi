using System.Collections;
using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Хит-стоп: короткое «замирание» времени (Time.timeScale ≈ 0) на сотые доли секунды
    /// при убийстве крупных врагов или успешном парировании — даёт ощущение веса удара.
    ///
    /// Вызов: HitStop.Do(0.1f). Безопасно: не конфликтует с паузой (если timeScale уже 0 —
    /// ничего не делает; восстанавливает только своё значение).
    /// </summary>
    public static class HitStop
    {
        private static HitStopRunner _runner;
        private static float _remaining;

        public static void Do(float duration, float scale = 0.03f)
        {
            if (duration <= 0f) return;
            if (Time.timeScale <= 0f && _remaining <= 0f) return; // игра на паузе — не трогаем

            if (_runner == null)
            {
                var go = new GameObject("~HitStop");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<HitStopRunner>();
            }
            _remaining = Mathf.Max(_remaining, duration);
            _runner.Run(scale);
        }

        private class HitStopRunner : MonoBehaviour
        {
            private Coroutine _co;
            private float _scale;

            public void Run(float scale)
            {
                _scale = scale;
                if (_co == null) _co = StartCoroutine(Freeze());
            }

            private IEnumerator Freeze()
            {
                float prev = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = _scale;
                while (_remaining > 0f)
                {
                    _remaining -= Time.unscaledDeltaTime;
                    yield return null;
                    // Если кто-то поставил паузу во время хит-стопа — уступаем и выходим.
                    if (Time.timeScale == 0f) { _co = null; _remaining = 0f; yield break; }
                }
                if (Mathf.Approximately(Time.timeScale, _scale)) Time.timeScale = prev;
                _co = null;
            }
        }
    }
}
