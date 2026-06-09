using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cybershi
{
    /// <summary>
    /// Контроллер главного меню. Строит UI из кода: заголовок, «Играть», «Настройки», «Выход».
    /// Сцену игры грузит по имени <see cref="gameSceneName"/> (должна быть в Build Settings).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Имя игровой сцены (добавьте её в Build Settings).")]
        public string gameSceneName = "SampleArena";

        private SettingsPanel _settings;

        private void Start()
        {
            GameSettings.EnsureLoaded();
            Time.timeScale = 1f;
            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = UIBuilder.CreateCanvas("MainMenuCanvas", 0);

            // Фон.
            var bg = UIBuilder.Image(canvas.transform, new Color(0.05f, 0.06f, 0.09f, 1f));
            UIBuilder.Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one);

            // Заголовок.
            var title = UIBuilder.Label(canvas.transform, "CYBERSHI", 96, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.SetRect(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(900f, 120f));

            var subtitle = UIBuilder.Label(canvas.transform, "динамичный шутер · буллет-хелл · смена перспективы",
                26, TextAnchor.MiddleCenter, new Color(0.6f, 0.7f, 0.8f));
            UIBuilder.SetRect(subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1000f, 40f));

            // Кнопки.
            float y = -40f;
            MakeMenuButton(canvas.transform, "Играть", ref y, StartGame);
            MakeMenuButton(canvas.transform, "Настройки", ref y, OpenSettings);
            MakeMenuButton(canvas.transform, "Выход", ref y, QuitGame);

            var hint = UIBuilder.Label(canvas.transform,
                "WASD — движение · Мышь — прицел · ЛКМ — огонь · Shift — рывок · Ctrl — подкат/слэм · Space — прыжок",
                20, TextAnchor.MiddleCenter, new Color(0.5f, 0.55f, 0.62f));
            UIBuilder.SetRect(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 30f));

            _settings = SettingsPanel.Create(canvas.transform, null);
            _settings.SetVisible(false);
        }

        private void MakeMenuButton(Transform parent, string text, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIBuilder.Button(parent, text, onClick);
            UIBuilder.SetRect(btn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(360f, 72f));
            y -= 90f;
        }

        private void StartGame()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
                SceneManager.LoadScene(gameSceneName);
            else
                Debug.LogError($"Cybershi: сцена '{gameSceneName}' не найдена в Build Settings. " +
                               "Добавьте её (File → Build Settings) или запустите Cybershi → Build Game.");
        }

        private void OpenSettings() => _settings.SetVisible(true);

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
