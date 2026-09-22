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
        static double lastTutorialSubmit;
        static double lastTutorialAct;
        static int maxTutorialStep;
        static double victoryForcedAt;
        static bool victoryAnimChecked;
        static int errorCount;

        const string FinalKey = "LastShift.SmokeFinal";
        const string DefeatKey = "LastShift.SmokeDefeat";
        const string TutorialKey = "LastShift.SmokeTutorial";
        const string SecondsKey = "LastShift.SmokeSeconds";
        const string PhaseKey = "LastShift.SmokePhase";
        const string FailKey = "LastShift.SmokeFails";

        public static void Run()
        {
            // Optional: -smokeScene <SceneName> selects the scene (default Boot);
            // -smokeFinal forces room completion to validate the final victory screen;
            // -smokeDefeat forces «ЦЕХ СТАБИЛИЗИРОВАН» and drives the end menu through
            // UnifiedGameInput (single-option navigation, restart, clean reload, and
            // the proof that confirming can never end the session).
            string scene = "Boot";
            bool final = false;
            bool defeat = false;
            bool tutorial = false;
            float playSeconds = (float)PlaySeconds;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-smokeScene" && i + 1 < args.Length) scene = args[i + 1];
                if (args[i] == "-smokeFinal") final = true;
                if (args[i] == "-smokeDefeat") defeat = true;
                if (args[i] == "-smokeTutorial") tutorial = true;
                // Opt-in longer window: the ten-step lesson needs ~110 s to be walked
                // end to end (practical steps wait for the engineer to come to them).
                if (args[i] == "-smokeSeconds" && i + 1 < args.Length &&
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float secs))
                    playSeconds = secs;
            }
            SessionState.SetBool(FinalKey, final);
            SessionState.SetBool(DefeatKey, defeat);
            SessionState.SetBool(TutorialKey, tutorial);
            SessionState.SetFloat(SecondsKey, playSeconds);
            SessionState.SetString(PhaseKey, "boot");
            SessionState.SetInt(FailKey, 0);

            SessionState.SetBool(RunningKey, true);
            EditorSceneManager.OpenScene("Assets/FactoryGame/Scenes/" + scene + ".unity");
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
            lastTutorialSubmit = 0;
            lastTutorialAct = 0;
            maxTutorialStep = 0;
            victoryForcedAt = 0;
            victoryAnimChecked = false;
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
            // The game must never be able to end its own session: inside the cabinet
            // it lives in the launcher's process, so stopping play mode from game code
            // means Application.Quit() is back. Catch it before the not-playing
            // early-out below, while the phase still says the run was under way.
            if (SessionState.GetBool(DefeatKey, false) &&
                IsDefeatPlayPhase(SessionState.GetString(PhaseKey, "")) &&
                !EditorApplication.isPlaying)
            {
                Fail("the game stopped play mode on its own — game code must not own the "
                     + "process (contract §5: leaving is the cabinet's «меню» button)");
                EditorApplication.update -= Tick;
                SessionState.SetBool(RunningKey, false);
                Debug.Log("SMOKE_SELF_QUIT_DETECTED=True");
                Debug.Log("SMOKE_RESULT: FAIL fails=" + SessionState.GetInt(FailKey, 0));
                EditorApplication.Exit(1);
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

            // Drive the intro flow (title card, briefing page, tutorial choice)
            // exactly like key presses, until gameplay loads.
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

            // Tutorial scenario: walk the whole lesson. Text steps are acknowledged
            // the way a player does — a real Submit through UnifiedGameInput (the
            // Arduino path); practical steps activate the system the step asks for,
            // and only at a moment when it can actually work.
            bool tutorialRun = SessionState.GetBool(TutorialKey, false);
            if (tutorialRun && LevelManager.Instance != null && elapsed > 6.0)
            {
                var tf = LevelManager.Instance.GetComponent<TutorialFlowController>();
                if (tf != null) maxTutorialStep = Mathf.Max(maxTutorialStep, tf.DevStep);
                if (tf != null && tf.DevRouteBlocked)
                {
                    errorCount++;
                    Debug.Log("SMOKE_FAIL: lesson dead-end — the gate blocks the route while "
                              + "only the arm may be activated");
                }

                var need = tf != null ? tf.DevRequiredMachine : null;
                if (need != null)
                {
                    bool worthIt = need.ActivationAlwaysEffective || need.EngineerInEffectiveZone;
                    if (need.IsReady && worthIt && elapsed - lastTutorialAct > 1.0)
                    {
                        lastTutorialAct = elapsed;
                        need.TryActivate();
                        Debug.Log("SMOKE_TUTORIAL_ACT " + need.displayName + " step=" + tf.DevStep
                            + " t=" + elapsed.ToString("0.0"));
                    }
                }
                else if (elapsed - lastTutorialSubmit > 2.0)
                {
                    lastTutorialSubmit = elapsed;
                    CheckTutorialLayout(tf);
                    SmokeInputDriver.Get().QueueSubmit();
                    Debug.Log("SMOKE_TUTORIAL_SUBMIT step=" + (tf != null ? tf.DevStep : -1)
                        + " t=" + elapsed.ToString("0.0"));
                }
                // No early return here: the run must still reach its end-of-window
                // check below, otherwise this scenario would never finish.
            }

            // Exercise the terminal: activate the selected command every 4 seconds.
            // Skipped in the tutorial scenario — the lesson driver above owns input there.
            if (!tutorialRun && elapsed > 8.0 && elapsed - lastActivation > 4.0)
            {
                lastActivation = elapsed;
                var lm = LevelManager.Instance;
                var view = lm != null ? lm.TerminalUi : null;
                if (view != null && !view.DevSelectedRowVisible)
                {
                    errorCount++;
                    Debug.Log("SMOKE_FAIL: selected command is outside the list viewport");
                }
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
                    victoryForcedAt = elapsed;
                }
                // The industrial victory animation must run first and must hold the
                // result menu back until it finishes.
                if (victoryForcedAt > 0.0 && !victoryAnimChecked && elapsed - victoryForcedAt > 0.6)
                {
                    victoryAnimChecked = true;
                    bool playing = flm != null && flm.VictoryPlaying;
                    bool menuLive = false;
                    if (flm != null && PanelOf(flm) != null) menuLive = PanelOf(flm).HasMenu;
                    if (!playing) Debug.Log("SMOKE_FAIL: victory animation did not start");
                    if (menuLive) Debug.Log("SMOKE_FAIL: result menu live during the victory animation");
                    if (!playing || menuLive) errorCount++;
                    Debug.Log("SMOKE_VICTORY_ANIM playing=" + playing + " menuLive=" + menuLive);
                }
                if (victoryForcedAt > 0.0 && elapsed - victoryForcedAt > 6.0)
                {
                    bool foundTitle = false;
                    bool foundRepeat = false;
                    string leaveRow = null;
                    foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                    {
                        if (!t.gameObject.activeInHierarchy) continue;
                        if (t.text == LastShift.Data.Loc.FactoryWon) foundTitle = true;
                        if (t.text.Contains(LastShift.Data.Loc.MenuRepeatRoom)) foundRepeat = true;
                        // The final room must leave the player a way on («ПОВТОРИТЬ ЦЕХ»)
                        // and no way to end the session: that is the cabinet's «меню»
                        // button, owned by the launcher (contract §5).
                        if (t.text.Contains("ВЫЙТИ") || t.text.Contains("В ГЛАВНОЕ МЕНЮ")) leaveRow = t.text;
                    }
                    if (leaveRow != null)
                        Debug.Log("SMOKE_FAIL: final screen offers «" + leaveRow + "» — the game must not "
                                  + "offer a way out of itself (contract §5)");
                    Debug.Log("SMOKE_FINAL_OK=" + foundTitle + " menuRepeat=" + foundRepeat
                        + " leaveRow=" + (leaveRow ?? "none"));
                    bool pass2 = errorCount == 0 && foundTitle && foundRepeat && leaveRow == null;
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(RunningKey, false);
                    Debug.Log(pass2 ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
                    EditorApplication.Exit(pass2 ? 0 : 1);
                    return;
                }
            }

            if (elapsed < SessionState.GetFloat(SecondsKey, (float)PlaySeconds)) return;

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
                var flow = level != null ? level.GetComponent<TutorialFlowController>() : null;
                if (flow != null) maxTutorialStep = Mathf.Max(maxTutorialStep, flow.DevStep);
                // Finishing the lesson loads Level 1, so at the end of the run the room
                // is either the training hall (still learning) or the first real room
                // (lesson completed) — both are fine. What must hold is that the lesson
                // really walked its steps up to the combination.
                bool finished = level != null && !level.TutorialMode &&
                                level.RoomName != LastShift.Data.Loc.TutorialRoomName;
                bool stillLearning = level != null && level.TutorialMode &&
                                     level.RoomName == LastShift.Data.Loc.TutorialRoomName;
                tutorialOk = (stillLearning || finished) && maxTutorialStep >= 9;
                Debug.Log("SMOKE_TUTORIAL_OK=" + tutorialOk + " maxStep=" + maxTutorialStep
                    + " finishedIntoRoom=" + (finished && level != null ? level.RoomName : "-"));
            }
            bool pass = errorCount == 0 && sceneOk && russianOk && tutorialOk;
            Debug.Log(pass ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL errors=" + errorCount);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        /// <summary>
        /// Layout rules the redesigned lesson must keep on every step: exactly one
        /// marked target, a callout that stays on screen and never covers it.
        /// </summary>
        static void CheckTutorialLayout(TutorialFlowController flow)
        {
            var steps = flow != null ? flow.DevSteps : null;
            if (steps == null || !steps.DevCalloutVisible) return;
            Rect card = steps.DevCalloutRect;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            bool onScreen = card.xMin >= -1f && card.yMin >= -1f && card.xMax <= w + 1f && card.yMax <= h + 1f;
            if (!onScreen)
            {
                errorCount++;
                Debug.Log("SMOKE_FAIL: callout off screen " + card + " screen=" + w + "x" + h);
            }
            if (steps.DevHasTarget)
            {
                Rect target = steps.DevTargetRect;
                bool overlaps = card.xMin < target.xMax && card.xMax > target.xMin &&
                                card.yMin < target.yMax && card.yMax > target.yMin;
                if (overlaps)
                {
                    errorCount++;
                    Debug.Log("SMOKE_FAIL: callout covers its target card=" + card + " target=" + target);
                }
                Debug.Log("SMOKE_TUTORIAL_LAYOUT step=" + flow.DevStep + " onScreen=" + onScreen +
                          " coversTarget=" + overlaps);
            }
            else Debug.Log("SMOKE_TUTORIAL_LAYOUT step=" + flow.DevStep + " onScreen=" + onScreen + " (no target)");
        }

        // ---------------- defeat-menu scenario ----------------

        static double phaseTime;
        static LevelManager cachedManager;

        static void Fail(string what)
        {
            SessionState.SetInt(FailKey, SessionState.GetInt(FailKey, 0) + 1);
            Debug.Log("SMOKE_FAIL: " + what);
        }

        /// <summary>True once the defeat run is actually playing: from then on, play
        /// mode ending without the test asking for it means the game quit itself.</summary>
        static bool IsDefeatPlayPhase(string phase) =>
            !string.IsNullOrEmpty(phase) && phase != "boot";

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
                        bool titleOk = false, selRepeat = false;
                        string leaveRow = null;
                        foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                        {
                            if (!t.gameObject.activeInHierarchy) continue;
                            if (t.text == LastShift.Data.Loc.RoomStabilized) titleOk = true;
                            if (t.text == "> " + LastShift.Data.Loc.MenuRepeatRoom + " <") selRepeat = true;
                            // The cabinet has no «выйти»: the launcher owns the
                            // process, so the game must not show that option at all.
                            if (t.text.Contains("ВЫЙТИ") || t.text.Contains("В ГЛАВНОЕ МЕНЮ")) leaveRow = t.text;
                        }
                        if (!titleOk) Fail("result text «ЦЕХ СТАБИЛИЗИРОВАН» missing");
                        if (!selRepeat) Fail("selected row «> ПОВТОРИТЬ ЦЕХ <» missing");
                        if (leaveRow != null)
                            Fail("defeat screen offers a way out of the game: «" + leaveRow + "» — "
                                 + "only the cabinet's «меню» button may leave (contract §5)");
                        if (MenuRowCount(panel) != 1)
                            Fail("defeat menu has " + MenuRowCount(panel) + " rows, expected exactly «ПОВТОРИТЬ ЦЕХ»");
                        Debug.Log("SMOKE_MENU_SHOWN title=" + titleOk + " selRepeat=" + selRepeat
                            + " rows=" + MenuRowCount(panel) + " leaveRow=" + (leaveRow ?? "none"));
                        SmokeInputDriver.Get().QueueNavigate(previous: true);
                        SetPhase("navUp", elapsed);
                    }
                    break;

                // A one-row menu must stay put under the joystick: no wrapping onto a
                // phantom row, no selection that points at nothing.
                case "navUp":
                    if (elapsed - phaseTime < 0.6 || lm == null) break;
                    {
                        int idx = PanelOf(lm).SelectedIndex;
                        if (idx != LastShift.UI.EndRoomPanel.OptionRepeatRoom)
                            Fail("Up moved the single-option menu to " + idx + ", expected ПОВТОРИТЬ ЦЕХ");
                        else Debug.Log("SMOKE_NAV_UP_OK");
                        SmokeInputDriver.Get().QueueNavigate(previous: false);
                        SetPhase("navDown", elapsed);
                    }
                    break;

                case "navDown":
                    if (elapsed - phaseTime < 0.6 || lm == null) break;
                    {
                        int idx = PanelOf(lm).SelectedIndex;
                        if (idx != LastShift.UI.EndRoomPanel.OptionRepeatRoom)
                            Fail("Down moved the single-option menu to " + idx + ", expected ПОВТОРИТЬ ЦЕХ");
                        else Debug.Log("SMOKE_NAV_DOWN_OK");
                        cachedManager = lm;
                        SmokeInputDriver.Get().QueueSubmit();
                        SetPhase("restartSent", elapsed);
                    }
                    break;

                case "restartSent":
                    if (elapsed - phaseTime > 8.0 && lm == cachedManager)
                    {
                        Fail("Submit on ПОВТОРИТЬ ЦЕХ did not reload the room");
                        SetPhase("noExitMenu", elapsed); // still check the no-exit branch
                        break;
                    }
                    if (lm == null || lm == cachedManager || lm.Terminal == null) break;
                    if (elapsed - phaseTime < 2.0) break;
                    {
                        if (lm.RoomEnded) Fail("room state not reset after restart");
                        if (Mathf.Abs(Time.timeScale - 1f) > 0.01f) Fail("timeScale is " + Time.timeScale + " after restart");
                        int gm = Object.FindObjectsByType<GameManager>(FindObjectsInactive.Exclude).Length;
                        int bridges = Object.FindObjectsByType<LastShift.Input.ArcadeInputBridge>(FindObjectsInactive.Exclude).Length;
                        int audio = Object.FindObjectsByType<LastShift.Audio.AudioManager>(FindObjectsInactive.Exclude).Length;
                        int panels = Object.FindObjectsByType<LastShift.UI.EndRoomPanel>(FindObjectsInactive.Include).Length;
                        if (gm > 1) Fail("duplicate GameManager after restart: " + gm);
                        if (bridges > 1) Fail("duplicate ArcadeInputBridge after restart: " + bridges);
                        if (audio > 1) Fail("duplicate AudioManager after restart: " + audio);
                        if (panels > 1) Fail("duplicate EndRoomPanel after restart: " + panels);
                        Debug.Log("SMOKE_RESTART_OK room=" + lm.RoomName + " gm=" + gm + " bridges=" + bridges
                            + " audio=" + audio + " panels=" + panels);
                        ForceStabilized(lm);
                        SetPhase("noExitMenu", elapsed);
                    }
                    break;

                // Second time on the defeat screen, the interesting question is the
                // opposite of the old one: confirming the menu must NOT be able to end
                // the session. Submit again and require the room to reload with play
                // mode still running — on the cabinet the game shares the launcher's
                // process, so quitting it would take the whole machine down.
                case "noExitMenu":
                    if (elapsed - phaseTime < 1.0 || lm == null) break;
                    {
                        var panel = PanelOf(lm);
                        if (!panel.HasMenu) Fail("menu not visible on the second defeat screen");
                        string leaveRow = null;
                        foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                        {
                            if (!t.gameObject.activeInHierarchy) continue;
                            if (t.text.Contains("ВЫЙТИ") || t.text.Contains("В ГЛАВНОЕ МЕНЮ")) leaveRow = t.text;
                        }
                        if (leaveRow != null) Fail("a leave-the-game row is back: «" + leaveRow + "»");
                        cachedManager = lm;
                        SmokeInputDriver.Get().QueueSubmit();
                        SetPhase("noExitSent", elapsed);
                    }
                    break;

                case "noExitSent":
                    // If the game were still able to quit, play mode would already be
                    // gone and the guard at the top of Tick() would have failed the run.
                    if (elapsed - phaseTime < 4.0) break;
                    {
                        bool stillPlaying = EditorApplication.isPlaying;
                        bool reloaded = lm != null && lm != cachedManager && lm.Terminal != null;
                        if (!stillPlaying) Fail("play mode ended after confirming the end menu");
                        if (!reloaded) Fail("Submit on ПОВТОРИТЬ ЦЕХ did not reload the room the second time");
                        Debug.Log("SMOKE_NO_EXIT_OK playing=" + stillPlaying + " reloaded=" + reloaded);
                        EditorApplication.update -= Tick;
                        SessionState.SetBool(RunningKey, false);
                        int fails = SessionState.GetInt(FailKey, 0);
                        Debug.Log(fails == 0 ? "SMOKE_RESULT: PASS" : "SMOKE_RESULT: FAIL fails=" + fails);
                        EditorApplication.Exit(fails == 0 ? 0 : 1);
                    }
                    break;
            }
        }

        /// <summary>Number of rows the end menu actually renders.</summary>
        static int MenuRowCount(LastShift.UI.EndRoomPanel panel)
        {
            var list = typeof(LastShift.UI.EndRoomPanel)
                .GetField("menuTexts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(panel) as System.Collections.Generic.List<UnityEngine.UI.Text>;
            return list != null ? list.Count : -1;
        }

        /// <summary>
        /// Publishes queued logical actions from inside the player loop (execution
        /// order -200, before ArcadeInputBridge and every consumer), so the
        /// frame-stamped UnifiedGameInput flags are seen the same frame — exactly
        /// the path the arcade input bridge uses.
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
