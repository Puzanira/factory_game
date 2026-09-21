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

        /// <summary>
        /// Leaves the current session. A browser tab cannot be closed by the page, so
        /// Application.Quit() is a silent no-op there — the web build returns to the
        /// title screen instead, which is the only "leave" that means anything in a
        /// tab. The menu label follows this (see Loc.MenuLeave).
        /// </summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            Load(GameManager.BootScene);
#else
            Application.Quit();
#endif
        }
    }
}
