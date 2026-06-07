using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Вешается на дочерний объект "Visual". Гарантирует наличие SpriteRenderer и
    /// заполняет его спрайтом-плейсхолдером нужной формы/цвета/размера.
    ///
    /// Как заменить плейсхолдер на настоящую графику:
    ///   1) Присвоить свой спрайт в поле "Sprite Override", ИЛИ
    ///   2) Просто задать SpriteRenderer.sprite в инспекторе и удалить этот компонент.
    /// Анимация: добавьте рядом <see cref="SpriteAnimator"/> — он будет менять кадры на этом же SpriteRenderer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class PlaceholderVisual : MonoBehaviour
    {
        public enum Shape { Square, Circle }

        [Tooltip("Если присвоить настоящий спрайт — плейсхолдер не используется.")]
        public Sprite spriteOverride;
        public Shape shape = Shape.Square;
        public Color color = Color.white;
        [Tooltip("Размер в юнитах. Применяется к localScale этого объекта.")]
        public Vector2 size = Vector2.one;
        public int sortingOrder = 0;

        private SpriteRenderer _sr;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;

            _sr.sprite = spriteOverride != null
                ? spriteOverride
                : (shape == Shape.Circle ? PlaceholderFactory.Circle : PlaceholderFactory.Square);

            _sr.color = color;
            _sr.sortingOrder = sortingOrder;
            transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        public void SetColor(Color c)
        {
            color = c;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _sr.color = c;
        }
    }
}
