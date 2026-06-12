using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cybershi
{
    /// <summary>
    /// Переходы между уровнями. Уровни — это сцены в Build Settings (по порядку).
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>Загрузить следующую сцену по индексу Build Settings (после последней — в первую/меню).</summary>
        public static void LoadNext()
        {
            Time.timeScale = 1f;
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
            SceneManager.LoadScene(next);
        }

        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                SceneManager.LoadScene(sceneName);
            else
                Debug.LogError($"Cybershi: сцена '{sceneName}' не найдена в Build Settings.");
        }

        public static void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
