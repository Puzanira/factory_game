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
        const string DefeatKey = "LastShift.SmokeDefeat";
        const string TutorialKey = "LastShift.SmokeTutorial";
        const string PhaseKey = "LastShift.SmokePhase";
        const string FailKey = "LastShift.SmokeFails";

        public static void Run()
        {
            // Optional: -smokeScene <SceneName> selects the scene (default Boot);
            // -smokeFinal forces room completion to validate the final victory screen;
            // -smokeDefeat forces «ЦЕХ СТАБИЛИЗИРОВАН» and drives the restart/quit
            // menu through UnifiedGameInput (wrap, restart, clean reload, quit).
            string scene = "Boot";
            bool final = false;
            bool defeat = false;
            bool tutorial = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-smokeScene" && i + 1 < args.Length) scene = args[i + 1];
                if (args[i] == "-smokeFinal") final = true;
                if (args[i] == "-smokeDefeat") defeat = true;
                if (args[i] == "-smokeTutorial") tutorial = true;
            }
            SessionState.SetBool(FinalKey, final);
            SessionState.SetBool(DefeatKey, defeat);
            SessionState.SetBool(TutorialKey, tutorial);
            SessionState.SetString(PhaseKey, "boot");
            SessionState.SetInt(FailKey, 0);

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
            // The defeat scenario survives domain reloads, so its error count
            // must live in SessionState rather than a static.
            if (SessionState.GetBool(DefeatKey, false))
                SessionState.SetInt(FailKey, SessionState.GetInt(FailKey, 0) + 1);
            Debug.Log("SMOKE_ERR[" + type + "]: " + condition + "\n" + stackTrace);
        }

        static void Tick()
        {
            // The defeat-menu scenario ends by quitting play mode on purpose;
            // detect that phase before the not-playing early-out below.
            if (SessionState.GetBool(DefeatKey, false) &&
                SessionState.GetString(PhaseKey, "") == "quitSent" &&
                !EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunningKey, false);
                int fails = SessionState.GetInt(FailKey, 0);
                Debug.Log("SMOKE_QUIT_STOPPED_PLAYMODE=True");
                Debug.Log(fails == 0 ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL fails=" + fails);
                EditorApplication.Exit(fails == 0 ? 0 : 1);
                return;
            }

            // Only measure while actually playing (the pre-play arm just waits).
            if (!EditorApplication.isPlaying)
            {
                startTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (SessionState.GetBool(DefeatKey, false))
            {
                DefeatTick(EditorApplication.timeSinceStartup - startTime);
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - startTime;

            // Drive the keyboard-only intro flow (title menu, tutorial choice,
            // 4 briefing pages) exactly like key presses, until gameplay loads.
            // Default: pick «СРАЗУ К СМЕНЕ» so Level 1 keeps its coverage;
            // -smokeTutorial keeps the default «ПРОЙТИ УРОК» and tests the lesson.
            var intro = LastShift.UI.IntroFlowUI.Instance;
            if (intro != null && elapsed > 2.0 && elapsed - lastActivation > 1.2)
            {
                lastActivation = elapsed;
                if (intro.DevAtTutorialChoice && !SessionState.GetBool(TutorialKey, false) &&
                    intro.DevTutorialChoiceIndex == 0)
                {
                    intro.DevNavigateNext();
                    Debug.Log("SMOKE_CHOICE_SKIP_TUTORIAL t=" + elapsed.ToString("0.0"));
                    return;
                }
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
                    bool foundRepeat = false;
                    bool foundQuit = false;
                    foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                    {
                        if (!t.gameObject.activeInHierarchy) continue;
                        if (t.text == LastShift.Data.Loc.FactoryWon) foundTitle = true;
                        if (t.text.Contains(LastShift.Data.Loc.MenuRepeatRoom)) foundRepeat = true;
                        if (t.text.Contains(LastShift.Data.Loc.MenuQuitGame)) foundQuit = true;
                    }
                    Debug.Log("SMOKE_FINAL_OK=" + foundTitle + " menuRepeat=" + foundRepeat + " menuQuit=" + foundQuit);
                    bool pass2 = errorCount == 0 && foundTitle && foundRepeat && foundQuit;
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(RunningKey, false);
                    Debug.Log(pass2 ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
                    EditorApplication.Exit(pass2 ? 0 : 1);
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
            bool tutorialOk = true;
            if (SessionState.GetBool(TutorialKey, false))
            {
                tutorialOk = level != null && level.TutorialMode &&
                             level.RoomName == LastShift.Data.Loc.TutorialRoomName;
                Debug.Log("SMOKE_TUTORIAL_OK=" + tutorialOk);
            }
            bool pass = errorCount == 0 && sceneOk && russianOk && tutorialOk;
            Debug.Log(pass ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        // ---------------- defeat-menu scenario ----------------

        static double phaseTime;
        static LevelManager cachedManager;

        static void Fail(string what)
        {
            SessionState.SetInt(FailKey, SessionState.GetInt(FailKey, 0) + 1);
            Debug.Log("SMOKE_FAIL: " + what);
        }

        static void SetPhase(string phase, double elapsed)
        {
            SessionState.SetString(PhaseKey, phase);
            phaseTime = elapsed;
            Debug.Log("SMOKE_PHASE: " + phase + " t=" + elapsed.ToString("0.0"));
        }

        static LastShift.UI.EndRoomPanel PanelOf(LevelManager lm) =>
            (LastShift.UI.EndRoomPanel)typeof(LevelManager)
                .GetField("endPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(lm);

        /// <summary>Forces the «ЦЕХ СТАБИЛИЗИРОВАН» state exactly like the last repair completing.</summary>
        static void ForceStabilized(LevelManager lm)
        {
            typeof(LevelManager)
                .GetField("roomFailed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(lm, true);
            if (lm.Engineer != null) lm.Engineer.AbandonObjective();
            PanelOf(lm).ShowRoomStabilized();
        }

        static void DefeatTick(double elapsed)
        {
            string phase = SessionState.GetString(PhaseKey, "boot");
            var lm = LevelManager.Instance;

            if (elapsed > 90.0)
            {
                Fail("timeout in phase " + phase);
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunningKey, false);
                Debug.Log("SMOKE_RESULT: FAIL fails=" + SessionState.GetInt(FailKey, 0));
                EditorApplication.Exit(1);
                return;
            }

            switch (phase)
            {
                case "boot":
                    if (elapsed > 5.0 && lm != null && lm.Terminal != null && !lm.RoomEnded)
                    {
                        ForceStabilized(lm);
                        SetPhase("menuShown", elapsed);
                    }
                    break;

                case "menuShown":
                    if (elapsed - phaseTime < 1.0 || lm == null) break;
                    {
                        var panel = PanelOf(lm);
                        if (!panel.HasMenu) Fail("menu not visible on defeat screen");
                        if (panel.SelectedIndex != LastShift.UI.EndRoomPanel.OptionRepeatRoom)
                            Fail("initial selection is " + panel.SelectedIndex + ", expected ПОВТОРИТЬ ЦЕХ");
                        bool titleOk = false, selRepeat = false, quitLabel = false;
                        foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                        {
                            if (!t.gameObject.activeInHierarchy) continue;
                            if (t.text == LastShift.Data.Loc.RoomStabilized) titleOk = true;
                            if (t.text == "> " + LastShift.Data.Loc.MenuRepeatRoom + " <") selRepeat = true;
                            if (t.text == LastShift.Data.Loc.MenuQuitGame) quitLabel = true;
                        }
                        if (!titleOk) Fail("result text «ЦЕХ СТАБИЛИЗИРОВАН» missing");
                        if (!selRepeat) Fail("selected row «> ПОВТОРИТЬ ЦЕХ <» missing");
                        if (!quitLabel) Fail("dimmed row «ВЫЙТИ ИЗ ИГРЫ» missing");
                        Debug.Log("SMOKE_MENU_SHOWN title=" + titleOk + " selRepeat=" + selRepeat + " quit=" + quitLabel);
                        SmokeInputDriver.Get().QueueNavigate(previous: true);
                        SetPhase("wrapUp", elapsed);
                    }
                    break;

                case "wrapUp":
                    if (elapsed - phaseTime < 0.6 || lm == null) break;
                    {
                        int idx = PanelOf(lm).SelectedIndex;
                        if (idx != LastShift.UI.EndRoomPanel.OptionQuit)
                            Fail("Up on first item wrapped to " + idx + ", expected ВЫЙТИ ИЗ ИГРЫ");
                        else Debug.Log("SMOKE_WRAP_UP_OK");
                        SmokeInputDriver.Get().QueueNavigate(previous: false);
                        SetPhase("wrapDown", elapsed);
                    }
                    break;

                case "wrapDown":
                    if (elapsed - phaseTime < 0.6 || lm == null) break;
                    {
                        int idx = PanelOf(lm).SelectedIndex;
                        if (idx != LastShift.UI.EndRoomPanel.OptionRepeatRoom)
                            Fail("Down on last item wrapped to " + idx + ", expected ПОВТОРИТЬ ЦЕХ");
                        else Debug.Log("SMOKE_WRAP_DOWN_OK");
                        cachedManager = lm;
                        SmokeInputDriver.Get().QueueSubmit();
                        SetPhase("restartSent", elapsed);
                    }
                    break;

                case "restartSent":
                    if (elapsed - phaseTime > 8.0 && lm == cachedManager)
                    {
                        Fail("Submit on ПОВТОРИТЬ ЦЕХ did not reload the room");
                        SetPhase("quitMenu", elapsed); // still try the quit branch
                        break;
                    }
                    if (lm == null || lm == cachedManager || lm.Terminal == null) break;
                    if (elapsed - phaseTime < 2.0) break;
                    {
                        if (lm.RoomEnded) Fail("room state not reset after restart");
                        if (Mathf.Abs(Time.timeScale - 1f) > 0.01f) Fail("timeScale is " + Time.timeScale + " after restart");
                        int gm = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length;
                        int bridges = Object.FindObjectsByType<LastShift.Input.ArduinoInputBridge>(FindObjectsSortMode.None).Length;
                        int audio = Object.FindObjectsByType<LastShift.Audio.AudioManager>(FindObjectsSortMode.None).Length;
                        int panels = Object.FindObjectsByType<LastShift.UI.EndRoomPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                        if (gm > 1) Fail("duplicate GameManager after restart: " + gm);
                        if (bridges > 1) Fail("duplicate ArduinoInputBridge after restart: " + bridges);
                        if (audio > 1) Fail("duplicate AudioManager after restart: " + audio);
                        if (panels > 1) Fail("duplicate EndRoomPanel after restart: " + panels);
                        Debug.Log("SMOKE_RESTART_OK room=" + lm.RoomName + " gm=" + gm + " bridges=" + bridges
                            + " audio=" + audio + " panels=" + panels);
                        ForceStabilized(lm);
                        SetPhase("quitMenu", elapsed);
                    }
                    break;

                case "quitMenu":
                    if (elapsed - phaseTime < 1.0 || lm == null) break;
                    SmokeInputDriver.Get().QueueNavigate(previous: false);
                    SetPhase("quitSelected", elapsed);
                    break;

                case "quitSelected":
                    if (elapsed - phaseTime < 0.6 || lm == null) break;
                    {
                        if (PanelOf(lm).SelectedIndex != LastShift.UI.EndRoomPanel.OptionQuit)
                            Fail("quit option not selected before quit test");
                        SmokeInputDriver.Get().QueueSubmit();
                        SetPhase("quitSent", elapsed);
                    }
                    break;

                case "quitSent":
                    // Waiting for SceneLoader.Quit() to stop play mode; the exit
                    // branch at the top of Tick() finishes the run after the
                    // domain reload. Nothing to do while still playing.
                    if (elapsed - phaseTime > 8.0)
                    {
                        Fail("Submit on ВЫЙТИ ИЗ ИГРЫ did not stop play mode");
                        EditorApplication.update -= Tick;
                        SessionState.SetBool(RunningKey, false);
                        Debug.Log("SMOKE_RESULT: FAIL fails=" + SessionState.GetInt(FailKey, 0));
                        EditorApplication.Exit(1);
                    }
                    break;
            }
        }

        /// <summary>
        /// Publishes queued logical actions from inside the player loop (execution
        /// order -200, before ArduinoInputBridge and every consumer), so the
        /// frame-stamped UnifiedGameInput flags are seen the same frame — exactly
        /// the path the Arduino controller uses.
        /// </summary>
        [DefaultExecutionOrder(-200)]
        class SmokeInputDriver : MonoBehaviour
        {
            static SmokeInputDriver instance;
            bool navQueued;
            bool navPrevious;
            bool submitQueued;

            public static SmokeInputDriver Get()
            {
                if (instance == null)
                {
                    var go = new GameObject("SmokeInputDriver");
                    Object.DontDestroyOnLoad(go);
                    instance = go.AddComponent<SmokeInputDriver>();
                }
                return instance;
            }

            public void QueueNavigate(bool previous) { navQueued = true; navPrevious = previous; }
            public void QueueSubmit() { submitQueued = true; }

            void Update()
            {
                if (navQueued)
                {
                    navQueued = false;
                    LastShift.Input.UnifiedGameInput.PublishNavigation(navPrevious);
                }
                if (submitQueued)
                {
                    submitQueued = false;
                    LastShift.Input.UnifiedGameInput.PublishSubmit();
                }
            }
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
