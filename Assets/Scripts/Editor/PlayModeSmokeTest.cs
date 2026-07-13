using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LastShift.Core;

namespace LastShift.EditorTools
{
    /// <summary>
    /// Headless smoke test: plays the Boot scene, lets Level 1 load and run for a
    /// while, periodically activates the selected terminal command, and exits with a
    /// non-zero code if any runtime error/exception is logged.
    /// Entering play mode causes a domain reload that wipes static state, so the
    /// update hook re-arms itself via [InitializeOnLoadMethod] + SessionState.
    /// Run: Unity -batchmode -executeMethod LastShift.EditorTools.PlayModeSmokeTest.Run
    /// </summary>
    public static class PlayModeSmokeTest
    {
        const string RunningKey = "LastShift.SmokeRunning";
        const double PlaySeconds = 35.0;

        static double startTime;
        static double lastActivation;
        static int errorCount;

        const string FinalKey = "LastShift.SmokeFinal";

        public static void Run()
        {
            // Optional: -smokeScene <SceneName> selects the scene (default Boot);
            // -smokeFinal forces room completion to validate the final victory screen.
            string scene = "Boot";
            bool final = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-smokeScene" && i + 1 < args.Length) scene = args[i + 1];
                if (args[i] == "-smokeFinal") final = true;
            }
            SessionState.SetBool(FinalKey, final);

            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            Arm();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void ReArmAfterDomainReload()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            Arm();
        }

        static void Arm()
        {
            startTime = EditorApplication.timeSinceStartup;
            lastActivation = 0;
            errorCount = 0;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            Debug.Log("SMOKE_ARMED (isPlaying=" + EditorApplication.isPlaying + ")");
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (condition.Contains("Licensing")) return;
            errorCount++;
            Debug.Log("SMOKE_ERR[" + type + "]: " + condition + "\n" + stackTrace);
        }

        static void Tick()
        {
            // Only measure while actually playing (the pre-play arm just waits).
            if (!EditorApplication.isPlaying)
            {
                startTime = EditorApplication.timeSinceStartup;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - startTime;

            // Drive the keyboard-only intro flow (title menu + 4 briefing pages)
            // exactly like Enter presses, until Level 1 loads.
            var intro = LastShift.UI.IntroFlowUI.Instance;
            if (intro != null && elapsed > 2.0 && elapsed - lastActivation > 1.2)
            {
                lastActivation = elapsed;
                intro.DevAdvance();
                Debug.Log("SMOKE_INTRO_ADVANCE t=" + elapsed.ToString("0.0"));
                return;
            }

            // Exercise the terminal: activate the selected command every 4 seconds.
            if (elapsed > 8.0 && elapsed - lastActivation > 4.0)
            {
                lastActivation = elapsed;
                var lm = LevelManager.Instance;
                if (lm != null && lm.Terminal != null && lm.Terminal.Selected != null)
                {
                    bool ok = lm.Terminal.Selected.TryActivate();
                    Debug.Log("SMOKE_ACTIVATE: " + lm.Terminal.Selected.displayName + " ok=" + ok
                        + " pressure=" + (lm.Pressure != null ? lm.Pressure.Value.ToString("0") : "-")
                        + " engineerState=" + (lm.Engineer != null && lm.Engineer.Fsm != null ? lm.Engineer.Fsm.CurrentLabel : "-"));
                }
            }

            // Final-screen validation: force room completion, then check the title
            // text and menu navigation of «ЗАВОД ПОБЕДИЛ».
            if (SessionState.GetBool(FinalKey, false))
            {
                var flm = LevelManager.Instance;
                if (elapsed > 16.0 && flm != null && !flm.RoomEnded)
                {
                    Debug.Log("SMOKE_FORCE_ESCAPE");
                    flm.OnEngineerEscaped();
                }
                if (elapsed > 18.0)
                {
                    bool foundTitle = false;
                    foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                        if (t.text == LastShift.Data.Loc.FactoryWon && t.gameObject.activeInHierarchy) foundTitle = true;
                    Debug.Log("SMOKE_FINAL_OK=" + foundTitle);
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(RunningKey, false);
                    Debug.Log(errorCount == 0 && foundTitle ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
                    EditorApplication.Exit(errorCount == 0 && foundTitle ? 0 : 1);
                    return;
                }
            }

            if (elapsed < PlaySeconds) return;

            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            var level = LevelManager.Instance;
            bool sceneOk = level != null && level.Engineer != null && level.Terminal != null
                           && level.Terminal.Items.Count > 0;
            bool russianOk = level != null && ContainsCyrillic(level.RoomName)
                             && level.Terminal != null && level.Terminal.Selected != null
                             && ContainsCyrillic(level.Terminal.Selected.CommandLabel);
            Debug.Log("SMOKE_SCENE_OK=" + sceneOk +
                (level != null && level.Terminal != null ? " commands=" + level.Terminal.Items.Count : ""));
            Debug.Log("SMOKE_RUSSIAN_OK=" + russianOk +
                (level != null ? " room=" + level.RoomName : ""));
            bool pass = errorCount == 0 && sceneOk && russianOk;
            Debug.Log(pass ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        static bool ContainsCyrillic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (c >= 0x0400 && c <= 0x04FF) return true;
            return false;
        }
    }
}
