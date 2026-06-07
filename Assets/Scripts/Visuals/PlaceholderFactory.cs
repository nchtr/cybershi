using UnityEngine;

namespace Cybershi
{
    /// <summary>
    /// Генерирует простые спрайты-плейсхолдеры в рантайме (белый квадрат и белый круг),
    /// чтобы ничего не рисовать вручную до появления настоящих ассетов.
    /// Спрайты белые — реальный цвет задаётся через <see cref="SpriteRenderer.color"/>,
    /// поэтому их легко переиспользовать и тонировать.
    ///
    /// Замена на настоящую графику: просто присвойте свой Sprite в SpriteRenderer
    /// (или в поле PlaceholderVisual.spriteOverride) — фабрика больше не используется.
    /// </summary>
    public static class PlaceholderFactory
    {
        private static Sprite _square;
        private static Sprite _circle;

        /// <summary>Белый квадрат 1x1. PixelsPerUnit = 1, значит 1 спрайт = 1 юнит до масштаба.</summary>
        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        name = "PH_Square",
                        filterMode = FilterMode.Point
                    };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _square.name = "PH_Square";
                }
                return _square;
            }
        }

        /// <summary>Белый круг (для снарядов буллет-хелла). Диаметр = 1 юнит до масштаба.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    const int size = 32;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    {
                        name = "PH_Circle",
                        filterMode = FilterMode.Bilinear
                    };
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        // Мягкий край в 1px для сглаживания.
                        float a = Mathf.Clamp01(r - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                    tex.Apply();
                    _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    _circle.name = "PH_Circle";
                }
                return _circle;
            }
        }
    }
}
