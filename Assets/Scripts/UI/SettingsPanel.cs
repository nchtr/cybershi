using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Cybershi
{
    /// <summary>
    /// Переиспользуемая панель настроек (громкости + полноэкранный режим). Строится из кода,
    /// читает/пишет <see cref="GameSettings"/>. Используется и главным меню, и паузой.
    /// Создание: <see cref="Create"/>; показ/скрытие: <see cref="SetVisible"/>.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        private GameObject _root;
        private UnityAction _onClose;

        public static SettingsPanel Create(Transform canvas, UnityAction onClose)
        {
            GameSettings.EnsureLoaded();

            var host = new GameObject("SettingsPanel", typeof(RectTransform));
            host.transform.SetParent(canvas, false);
            var panel = host.AddComponent<SettingsPanel>();
            panel._onClose = onClose;
            panel.Build(host.transform);
            return panel;
        }

        private void Build(Transform parent)
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            UIBuilder.Stretch((RectTransform)_root.transform, Vector2.zero, Vector2.one);

            // Затемнение фона.
            var dim = UIBuilder.Image(_root.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBuilder.Stretch((RectTransform)dim.transform, Vector2.zero, Vector2.one);

            // Центральная панель.
            var box = UIBuilder.Image(_root.transform, UIBuilder.PanelColor);
            UIBuilder.SetRect(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 620f));

            var title = UIBuilder.Label(box.transform, "НАСТРОЙКИ", 44, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.SetRect(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 60f));

            float y = -140f;
            MakeSliderRow(box.transform, "Общая громкость", GameSettings.Master, ref y, GameSettings.SetMaster);
            MakeSliderRow(box.transform, "Музыка", GameSettings.Music, ref y, GameSettings.SetMusic);
            MakeSliderRow(box.transform, "Эффекты", GameSettings.Sfx, ref y, GameSettings.SetSfx);

            // Полноэкранный режим.
            var toggle = UIBuilder.Toggle(box.transform, "Полный экран", GameSettings.Fullscreen, GameSettings.SetFullscreen);
            UIBuilder.SetRect(toggle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-150f, y), new Vector2(320f, 40f));
            y -= 90f;

            // Кнопка «Назад».
            var back = UIBuilder.Button(box.transform, "Назад", () => { SetVisible(false); _onClose?.Invoke(); });
            UIBuilder.SetRect(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(280f, 64f));
        }

        private void MakeSliderRow(Transform parent, string label, float value, ref float y, UnityAction<float> onChanged)
        {
            var text = UIBuilder.Label(parent, label, 26, TextAnchor.MiddleLeft, Color.white);
            UIBuilder.SetRect(text, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-310f, y), new Vector2(320f, 36f));

            var valueText = UIBuilder.Label(parent, Mathf.RoundToInt(value * 100f) + "%", 24, TextAnchor.MiddleRight, new Color(0.7f, 0.8f, 0.9f));
            UIBuilder.SetRect(valueText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(300f, y), new Vector2(120f, 36f));

            var slider = UIBuilder.Slider(parent, 0f, 1f, value, v =>
            {
                onChanged(v);
                valueText.text = Mathf.RoundToInt(v * 100f) + "%";
            });
            UIBuilder.SetRect(slider, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y - 38f), new Vector2(560f, 30f));

            y -= 100f;
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}
