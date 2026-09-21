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

        // There is deliberately no Quit(). On the cabinet this game runs *inside* the
        // launcher process, so Application.Quit() would take the whole machine down
        // with it. Leaving is the cabinet's «меню» touch button, which the hub polls
        // (ArcadeInput.MenuButton) — the game never learns about it and never owns
        // the process. See ARCADE_INTEGRATION_CONTRACT §5; pinned by
        // Tests/EditMode/ArcadeContractTests.cs.
    }
}
