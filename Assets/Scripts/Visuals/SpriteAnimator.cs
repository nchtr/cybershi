using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Простейший покадровый аниматор: проигрывает <see cref="SpriteAnimation"/> на SpriteRenderer.
    /// Не зависит от Unity Animator/Mecanim — достаточно списка спрайтов.
    ///
    /// Использование из кода:
    ///   animator.Play(runAnim);     // переключить состояние
    ///   animator.SetFlipX(true);    // развернуть по направлению движения
    /// Если кадров нет (плейсхолдер) — компонент ничего не трогает.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [Tooltip("Анимация, играющая по умолчанию при старте.")]
        public SpriteAnimation defaultAnimation;

        private SpriteRenderer _sr;
        private SpriteAnimation _current;
        private float _timer;
        private int _frame;
        private bool _finished;

        public bool IsFinished => _finished;
        public SpriteAnimation Current => _current;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void Start()
        {
            if (defaultAnimation != null) Play(defaultAnimation);
        }

        /// <summary>Запустить анимацию. Повторный вызов той же анимации игнорируется (не сбрасывает).</summary>
        public void Play(SpriteAnimation anim, bool restartIfSame = false)
        {
            if (anim == null || !anim.HasFrames) return;
            if (_current == anim && !restartIfSame) return;

            _current = anim;
            _timer = 0f;
            _frame = 0;
            _finished = false;
            _sr.sprite = anim.frames[0];
        }

        public void SetFlipX(bool flip)
        {
            if (_sr != null) _sr.flipX = flip;
        }

        private void Update()
        {
            if (_current == null || !_current.HasFrames || _finished) return;

            _timer += Time.deltaTime;
            while (_timer >= _current.FrameDuration)
            {
                _timer -= _current.FrameDuration;
                _frame++;

                if (_frame >= _current.frames.Length)
                {
                    if (_current.loop)
                    {
                        _frame = 0;
                    }
                    else
                    {
                        _frame = _current.frames.Length - 1;
                        _finished = true;
                        break;
                    }
                }
            }
            _sr.sprite = _current.frames[_frame];
        }
    }
}
