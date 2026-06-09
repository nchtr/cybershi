using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Cybershi
{
    /// <summary>
    /// Утилиты для сборки интерфейса прямо из кода (uGUI). Меню, пауза и HUD строят себя сами,
    /// чтобы ничего не нужно было собирать руками в редакторе. Всё на легаси-Text (без TextMeshPro)
    /// и на белом спрайте-плейсхолдере — легко заменить на свои префабы/спрайты.
    ///
    /// EnsureEventSystem ставит EventSystem с правильным модулем ввода под новую Input System.
    /// </summary>
    public static class UIBuilder
    {
        private static Font _font;
        public static Font UIFont =>
            _font != null ? _font : (_font = Font.CreateDynamicFontFromOSFont(
                new[] { "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans" }, 16));

        public static readonly Color Accent = new Color(0.30f, 0.85f, 1.00f);
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.10f, 0.92f);

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;

            // Создаём выключенным, настраиваем модуль ввода, и только потом включаем —
            // чтобы у InputSystemUIInputModule к моменту OnEnable уже были назначены действия,
            // иначе клики по кнопкам не регистрируются.
            var go = new GameObject("EventSystem");
            go.SetActive(false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // привязка стандартных действий UI (point, click, navigate…)
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            go.SetActive(true);
        }

        public static RectTransform SetRect(Component c, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image Image(Transform parent, Color color)
        {
            var go = NewUI("Image", parent);
            var img = go.AddComponent<Image>();
            img.sprite = PlaceholderFactory.Square; // гарантирует сплошной прямоугольник
            img.type = Image.Type.Simple;
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var go = NewUI("Text", parent);
            var t = go.AddComponent<Text>();
            t.font = UIFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string text, UnityAction onClick)
        {
            var go = NewUI("Button", parent);
            var img = go.AddComponent<Image>();
            img.sprite = PlaceholderFactory.Square;
            img.type = Image.Type.Simple;
            img.color = new Color(0.15f, 0.17f, 0.22f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.17f, 0.22f, 1f);
            colors.highlightedColor = new Color(0.22f, 0.45f, 0.6f, 1f);
            colors.pressedColor = Accent;
            colors.selectedColor = new Color(0.22f, 0.45f, 0.6f, 1f);
            btn.colors = colors;

            var label = Label(go.transform, text, 30, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Slider Slider(Transform parent, float min, float max, float value, UnityAction<float> onChanged)
        {
            var go = NewUI("Slider", parent);
            var slider = go.AddComponent<Slider>();

            var bg = Image(go.transform, new Color(0f, 0f, 0f, 0.5f));
            Stretch((RectTransform)bg.transform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));

            var fillArea = NewUI("Fill Area", go.transform);
            var faRt = (RectTransform)fillArea.transform;
            Stretch(faRt, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
            faRt.offsetMin = new Vector2(5f, faRt.offsetMin.y);
            faRt.offsetMax = new Vector2(-5f, faRt.offsetMax.y);

            var fill = Image(fillArea.transform, Accent);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;

            var hsa = NewUI("Handle Slide Area", go.transform);
            var hsaRt = (RectTransform)hsa.transform;
            Stretch(hsaRt, Vector2.zero, Vector2.one);
            hsaRt.offsetMin = new Vector2(8f, 0f);
            hsaRt.offsetMax = new Vector2(-8f, 0f);

            var handle = Image(hsa.transform, Color.white);
            var hRt = (RectTransform)handle.transform;
            hRt.anchorMin = new Vector2(0f, 0f); hRt.anchorMax = new Vector2(0f, 1f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(16f, 0f);

            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static Toggle Toggle(Transform parent, string text, bool value, UnityAction<bool> onChanged)
        {
            var go = NewUI("Toggle", parent);
            var toggle = go.AddComponent<Toggle>();

            var box = Image(go.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = new Vector2(0f, 0.5f); boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.pivot = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = new Vector2(0f, 0f);
            boxRt.sizeDelta = new Vector2(36f, 36f);

            var check = Image(box.transform, Accent);
            Stretch((RectTransform)check.transform, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));

            var label = Label(go.transform, text, 26, TextAnchor.MiddleLeft, Color.white);
            var lblRt = (RectTransform)label.transform;
            lblRt.anchorMin = new Vector2(0f, 0f); lblRt.anchorMax = new Vector2(1f, 1f);
            lblRt.offsetMin = new Vector2(48f, 0f); lblRt.offsetMax = Vector2.zero;

            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = value;
            if (onChanged != null) toggle.onValueChanged.AddListener(onChanged);
            return toggle;
        }

        /// <summary>Полоска (для здоровья/боссов). Возвращает Image-заполнение (меняйте fillAmount 0..1).</summary>
        public static Image Bar(RectTransform container, Color bgColor, Color fillColor)
        {
            var bg = Image(container, bgColor);
            Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one);

            var fill = Image(bg.transform, fillColor);
            Stretch((RectTransform)fill.transform, Vector2.zero, Vector2.one);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            return fill;
        }
    }
}
