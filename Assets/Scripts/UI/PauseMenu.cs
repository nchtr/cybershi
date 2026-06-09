using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cybershi
{
    /// <summary>
    /// Игровая пауза по Esc/Start. Останавливает время (Time.timeScale = 0), показывает меню:
    /// «Продолжить», «Настройки», «В главное меню», «Выход». UI строится из кода.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("Имя сцены главного меню (должна быть в Build Settings).")]
        public string mainMenuSceneName = "MainMenu";

        private Canvas _canvas;
        private GameObject _menuRoot;
        private SettingsPanel _settings;
        private bool _paused;

        public bool IsPaused => _paused;

        private void Start()
        {
            BuildUI();
            SetPaused(false);
        }

        private void Update()
        {
            if (InputReader.Instance.PausePressed)
            {
                // Esc внутри открытых настроек — просто закрыть их, оставшись на паузе.
                if (_paused && _settings != null && _settings.IsVisible)
                {
                    _settings.SetVisible(false);
                    return;
                }
                SetPaused(!_paused);
            }
        }

        private void BuildUI()
        {
            _canvas = UIBuilder.CreateCanvas("PauseCanvas", 50);

            _menuRoot = new GameObject("Menu", typeof(RectTransform));
            _menuRoot.transform.SetParent(_canvas.transform, false);
            UIBuilder.Stretch((RectTransform)_menuRoot.transform, Vector2.zero, Vector2.one);

            var dim = UIBuilder.Image(_menuRoot.transform, new Color(0f, 0f, 0f, 0.7f));
            UIBuilder.Stretch((RectTransform)dim.transform, Vector2.zero, Vector2.one);

            var title = UIBuilder.Label(_menuRoot.transform, "ПАУЗА", 72, TextAnchor.MiddleCenter, UIBuilder.Accent);
            UIBuilder.SetRect(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(600f, 100f));

            float y = 80f;
            MakeButton("Продолжить", ref y, () => SetPaused(false));
            MakeButton("Настройки", ref y, () => _settings.SetVisible(true));
            MakeButton("В главное меню", ref y, ToMainMenu);
            MakeButton("Выход", ref y, Quit);

            _settings = SettingsPanel.Create(_canvas.transform, null);
            _settings.SetVisible(false);
        }

        private void MakeButton(string text, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIBuilder.Button(_menuRoot.transform, text, onClick);
            UIBuilder.SetRect(btn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(380f, 68f));
            y -= 86f;
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_menuRoot != null) _menuRoot.SetActive(paused);
            if (!paused && _settings != null) _settings.SetVisible(false);
        }

        private void ToMainMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
            else
                Debug.LogError($"Cybershi: сцена '{mainMenuSceneName}' не в Build Settings.");
        }

        private void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            // Чтобы пауза не «застряла» при перезагрузке сцены.
            Time.timeScale = 1f;
        }
    }
}
