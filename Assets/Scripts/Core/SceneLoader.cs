using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastShift.Core
{
    /// <summary>Scene transitions. Always restores time scale so pause can't leak across scenes.</summary>
    public static class SceneLoader
    {
        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        public static void Reload()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
