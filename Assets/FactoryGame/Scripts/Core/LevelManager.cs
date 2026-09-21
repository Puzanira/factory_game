using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Machines;
using LastShift.Objectives;
using LastShift.UI;
using LastShift.Utilities;

namespace LastShift.Core
{
    /// <summary>
    /// Per-room orchestrator: builds the room and UI, spawns the engineer, tracks the
    /// objective sequence, routes the shared logical input (Up/Down/Enter + Esc), and
    /// handles room completion / failure / scene flow.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Tooltip("Room configuration. When empty, a default is derived from the scene name.")]
        public LevelData levelData;
        [Tooltip("Prefab registry. Missing entries fall back to code factories.")]
        public PrefabLibrary prefabLibrary;

        public RoomPressureController Pressure { get; private set; }
        public RoomEscalationController Escalation { get; private set; }
        public LastShift.Engineer.EngineerController Engineer { get; private set; }
        public FactoryCommandTerminal Terminal { get; private set; }
        public ExitDoor Exit { get; private set; }

        RoomLayout layout;
        RoomRefs refs;
        bool roomComplete;
        bool roomFailed;

        EndRoomPanel endPanel;
        HUDController hud;
        VictorySequenceController victory;

        public string RoomName => layout != null ? layout.roomName : "";
        public bool Escalated => Escalation != null && Escalation.Escalated;
        public bool RoomEnded => roomComplete || roomFailed;
        public bool InputLocked => roomComplete || roomFailed || TutorialOverlayLock;

        /// <summary>True while this scene runs the interactive tutorial room.</summary>
        public bool TutorialMode { get; private set; }
        /// <summary>The arcade cabinet has no pause; kept always-false so tutorial
        /// helpers that guarded against pausing keep compiling and behave normally.</summary>
        public bool IsPaused => false;
        /// <summary>Set by the tutorial while its completion panel owns the input.</summary>
        public bool TutorialOverlayLock { get; set; }

        /// <summary>
        /// An informational tutorial step owns the clock (the room is frozen while
        /// the player reads). LevelManager must not touch Time.timeScale then.
        /// </summary>
        public bool TutorialTimeFreeze { get; set; }

        /// <summary>True while the industrial victory animation is still running.</summary>
        public bool VictoryPlaying => victory != null && victory.Playing;

        public RepairObjective CurrentObjectiveForHud
        {
            get
            {
                if (Engineer != null && Engineer.CurrentObjective != null) return Engineer.CurrentObjective;
                return FirstUncompletedObjective();
            }
        }

        RepairObjective FirstUncompletedObjective()
        {
            if (refs == null) return null;
            foreach (var o in refs.objectives)
                if (o != null && !o.Completed) return o;
            return null;
        }

        /// <summary>
        /// Nearest unfinished repair point other than the given one — used by the
        /// engineer to switch targets after a stun or panic instead of stubbornly
        /// walking back into the same trap.
        /// </summary>
        public RepairObjective AlternativeObjectiveFor(RepairObjective current)
        {
            if (refs == null) return null;
            RepairObjective best = null;
            float bestDist = float.MaxValue;
            foreach (var o in refs.objectives)
            {
                if (o == null || o.Completed || o == current) continue;
                float d = Engineer != null ? Vector2.Distance(Engineer.Pos, o.Pos) : 0f;
                if (d < bestDist) { bestDist = d; best = o; }
            }
            return best;
        }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            GameManager.Ensure();
            ResolveData();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void ResolveData()
        {
            if (levelData == null)
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                levelData = ScriptableObject.CreateInstance<LevelData>();
                levelData.sceneName = scene;
                if (scene == GameManager.Level2Scene) { levelData.levelIndex = 1; levelData.nextSceneName = GameManager.Level3Scene; }
                else if (scene == GameManager.Level3Scene) { levelData.levelIndex = 2; levelData.finalLevel = true; }
                else { levelData.levelIndex = 0; levelData.nextSceneName = GameManager.Level2Scene; }
            }
            if (levelData.engineerData == null)
                levelData.engineerData = ScriptableObject.CreateInstance<EngineerData>();
            if (levelData.escalationData == null)
                levelData.escalationData = ScriptableObject.CreateInstance<EscalationData>();

            if (GameManager.Instance != null)
                GameManager.Instance.CurrentLevelIndex = levelData.levelIndex;
        }

        void Start()
        {
            TutorialMode = GameManager.TutorialRequested && levelData.levelIndex == 0;
            if (TutorialMode)
            {
                // Work on copies so shared assets never absorb tutorial tuning.
                levelData = Instantiate(levelData);
                levelData.engineerData = levelData.engineerData != null
                    ? Instantiate(levelData.engineerData)
                    : ScriptableObject.CreateInstance<EngineerData>();
                levelData.engineerData.resolveMax = 400f;   // failure impossible in the lesson
                levelData.engineerData.lossArmGrab = 10f;   // step 2 shows «РЕШИМОСТЬ −10»
                // The lesson target does not dodge telegraphs: otherwise step 2
                // («дождитесь цель в зоне») is nearly impossible to land.
                levelData.engineerData.avoidDangerThreshold = 999f;
                levelData.finalLevel = false;
            }

            layout = TutorialMode ? LevelLayouts.Tutorial() : LevelLayouts.Get(levelData.levelIndex);
            levelData.roomName = layout.roomName;

            // Before anything is built: world-space canvases size their font rasterisation
            // from the camera (UIBuilder.ApplyWorldCanvasDensity), and the room's repair
            // plates and the engineer's label are both created below.
            SetupCamera();

            PathGrid.Create(Vector2.zero, layout.roomSize + new Vector2(1.5f, 1.5f), 0.5f);
            refs = RoomBuilder.Build(layout, prefabLibrary, transform);
            Exit = refs.exit;
            Exit.CompletedEvent += _ => OnEngineerEscaped();
            foreach (var obj in refs.objectives)
                obj.CompletedEvent += OnObjectiveCompleted;

            // Room Pressure and its escalation were removed by design: the room is
            // decided purely by Resolve vs repairs, and machinery never fires on
            // its own. Pressure/Escalation stay null; every consumer null-checks.
            gameObject.AddComponent<FactoryControlResource>();

            SpawnEngineer();
            BuildUI();
            gameObject.AddComponent<RoomAudioDirector>().Init(this, levelData.levelIndex);

            if (refs.objectives.Count > 0) Engineer.SetObjective(refs.objectives[0]);

            var combos = gameObject.AddComponent<MachineCombinationTracker>();
            combos.Init(this, refs);
            combos.ComboTriggered += OnComboTriggered;

            WireToasts();

            // The room announces itself once and fades: no permanent top header.
            if (hud != null) hud.ShowRoomTitle(layout.roomName, layout.goalText);

            if (TutorialMode)
            {
                gameObject.AddComponent<TutorialFlowController>().Init(this, refs, hud);
            }
            else if (levelData.levelIndex == 0)
            {
                StartCoroutine(TutorialHints());
            }
        }

        void OnComboTriggered(string title, float resolveBonus)
        {
            if (hud == null) return;
            hud.ShowToast(title, 3.2f);
            if (resolveBonus > 0f)
            {
                // Own resolve toast; suppress the generic one for this hit.
                lastResolveToastAt = Time.unscaledTime;
                hud.ShowToast(string.Format(Data.Loc.ResolveLossToast, Mathf.RoundToInt(resolveBonus)), 2.6f);
            }
        }

        /// <summary>Shared toast entry point for the tactical systems.</summary>
        public void ShowToast(string message, float duration = 2.6f, bool warning = false)
        {
            if (hud != null) hud.ShowToast(message, duration, warning);
        }

        float lastResolveToastAt = -99f;
        float lastOutcomeToastAt = -99f;

        void WireToasts()
        {
            if (hud == null) return;
            foreach (var m in refs.machines)
            {
                if (m == null) continue;
                var machine = m;
                machine.Activated += _ => hud.ShowToast(machine.ActivationMessage);
                machine.Judged += OnMachineJudged;
            }
            Engineer.Stats.ResolveEmpty += () => hud.ShowToast(Data.Loc.ExitUnlockedToast, 3.5f);
            Engineer.Stats.ResolveLost += OnResolveLost;

            var res = FactoryControlResource.Instance;
            var terminalUi = GetTerminalUi();
            if (res != null && terminalUi != null)
                res.InsufficientFlash += terminalUi.FlashResource;
        }

        CommandTerminalUI GetTerminalUi() =>
            Terminal != null ? Terminal.GetComponent<CommandTerminalUI>() : null;

        /// <summary>Terminal view (command list + lower-left detail panel rects).</summary>
        public CommandTerminalUI TerminalUi => GetTerminalUi();

        /// <summary>
        /// Timing feedback: effective activations get a positive line and a soft
        /// blip, wasted ones a muted note and a longer cooldown (already applied by
        /// the machine). Recovery/always-effective machines stay silent.
        /// </summary>
        void OnMachineJudged(Machines.InteractableMachine machine, bool effective)
        {
            if (hud == null || RoomEnded) return;
            if (effective && machine.ActivationAlwaysEffective) return;
            if (Time.unscaledTime - lastOutcomeToastAt < 0.4f) return;
            lastOutcomeToastAt = Time.unscaledTime;
            if (effective)
            {
                hud.ShowToast(Data.Loc.EffectiveActivation, 2.2f);
                AudioManager.Play("terminal_ready", SfxBus.UI, 0.5f);
            }
            else
            {
                hud.ShowToast(Data.Loc.WastedActivation, 2.4f, warning: true);
                AudioManager.Play("terminal_denied", SfxBus.UI, 0.35f);
            }
        }

        /// <summary>Rate-limited «РЕШИМОСТЬ −N» so the player links actions to outcomes.</summary>
        void OnResolveLost(float amount, string reason)
        {
            if (hud == null || RoomEnded) return;
            if (Time.unscaledTime - lastResolveToastAt < 3f) return;
            lastResolveToastAt = Time.unscaledTime;
            hud.ShowToast(string.Format(Data.Loc.ResolveLossToast, Mathf.RoundToInt(amount)), 2.2f);
        }

        System.Collections.IEnumerator TutorialHints()
        {
            yield return new WaitForSeconds(2f);
            if (hud != null && !RoomEnded) hud.ShowToast(Data.Loc.HintGoal, 4.5f);
            yield return new WaitForSeconds(4.5f);
            if (hud != null && !RoomEnded) hud.ShowToast(Data.Loc.HintSelect, 4.5f);
            yield return new WaitForSeconds(4.5f);
            if (hud != null && !RoomEnded) hud.ShowToast(Data.Loc.HintActivate, 4.5f);
            yield return new WaitForSeconds(4.5f);
            if (hud != null && !RoomEnded) hud.ShowToast(Data.Loc.HintHighlight, 4.5f);
        }

        void SpawnEngineer()
        {
            GameObject go = prefabLibrary != null && prefabLibrary.engineer != null
                ? Instantiate(prefabLibrary.engineer)
                : PrefabFactories.CreateEngineer();
            Vector2 spawn = layout.engineerSpawn;
            var grid = PathGrid.Instance;
            if (grid != null) spawn = grid.NearestFree(spawn);
            go.transform.position = new Vector3(spawn.x, spawn.y, 0f);

            Engineer = go.GetComponent<LastShift.Engineer.EngineerController>();
            Engineer.Init(levelData.engineerData, layout.exitPos);
            Engineer.Escaped += OnEngineerEscaped;
            Engineer.Stats.ResolveEmpty += () => Exit.Unlock();
        }

        void BuildUI()
        {
            GameObject uiRoot = prefabLibrary != null && prefabLibrary.uiRoot != null
                ? Instantiate(prefabLibrary.uiRoot)
                : PrefabFactories.CreateUIRoot();
            uiRoot.name = "UIRoot";

            Terminal = uiRoot.GetComponent<FactoryCommandTerminal>();
            Terminal.Init(refs.machines);

            Canvas canvas = UIBuilder.CreateCanvas("GameCanvas", 10);

            uiRoot.GetComponent<CommandTerminalUI>().Bind(Terminal, this, canvas);
            hud = uiRoot.GetComponent<HUDController>();
            hud.Bind(this, canvas);
            endPanel = uiRoot.GetComponent<EndRoomPanel>();
            endPanel.Build(canvas);
        }

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.07f);

            // Fit the whole room into the area right of the terminal (28%). The top
            // HUD strip is gone, so only a thin margin is reserved above.
            const float panelFrac = 0.28f;
            const float topFrac = 0.02f;
            float aspect = cam.aspect;
            float margin = 1.0f;
            float orthoForH = (layout.roomSize.y + margin) / (2f * (1f - topFrac));
            float orthoForW = (layout.roomSize.x + margin) / (2f * aspect * (1f - panelFrac));
            float ortho = Mathf.Max(orthoForH, orthoForW);
            cam.orthographicSize = ortho;
            float worldW = 2f * ortho * aspect;
            float worldH = 2f * ortho;
            cam.transform.position = new Vector3(-worldW * panelFrac * 0.5f, worldH * topFrac * 0.5f, -10f);
        }

        // ---------------- objective / completion flow ----------------

        void OnObjectiveCompleted(ObjectiveNode node)
        {
            if (RoomEnded) return;
            if (TutorialMode)
            {
                // The lesson has no defeat: the tutorial controller restarts the step.
                var tf = GetComponent<TutorialFlowController>();
                if (tf != null) tf.OnObjectiveRepaired();
                return;
            }
            // Objectives can now be repaired out of order (the engineer may switch
            // targets after a stun/panic), so always pick the next unfinished one.
            var next = FirstUncompletedObjective();
            if (next != null)
            {
                Engineer.SetObjective(next);
            }
            else
            {
                // The engineer stabilized the room: the factory loses this room.
                roomFailed = true;
                Engineer.AbandonObjective();
                endPanel.ShowRoomStabilized();
            }
        }

        public void OnEngineerEscaped()
        {
            if (roomComplete) return;
            if (TutorialMode) return; // cannot happen in the lesson; never end the room
            roomComplete = true;
            // The factory won this room: play the short industrial victory animation
            // first, and only then show the (opaque) result screen with its menu.
            victory = gameObject.AddComponent<VictorySequenceController>();
            victory.Play(this, ShowVictoryResult);
        }

        void ShowVictoryResult()
        {
            if (endPanel == null) return;
            if (levelData.finalLevel) endPanel.ShowSliceComplete();
            else endPanel.ShowRoomComplete();
        }

        // ---------------- input routing ----------------

        void Update()
        {
            if (roomComplete)
            {
                // Result input only wakes up once the victory animation is done.
                if (VictoryPlaying || endPanel == null || !endPanel.Visible) return;
                if (levelData.finalLevel) HandleEndMenu();
                else if (GameInput.ConfirmPressed) { UiSfx.Confirm(); SceneLoader.Load(levelData.nextSceneName); }
                return;
            }

            if (roomFailed)
            {
                HandleEndMenu();
                return;
            }

            // The arcade cabinet has no pause: play runs at normal speed. While an
            // informational tutorial step is up, the lesson owns the clock instead.
            if (!TutorialTimeFreeze) Time.timeScale = 1f;
        }

        /// <summary>
        /// Shared «ПОВТОРИТЬ ЦЕХ» / «ВЫЙТИ ИЗ ИГРЫ» menu on the defeat and final
        /// victory screens: Up/Down select (wrapping), Red confirms. Restart always
        /// reloads the *current* room.
        /// </summary>
        void HandleEndMenu()
        {
            if (endPanel == null || !endPanel.HasMenu) return;
            if (GameInput.UpPressed) endPanel.MoveSelection(-1);
            if (GameInput.DownPressed) endPanel.MoveSelection(+1);
            if (GameInput.ConfirmPressed)
            {
                UiSfx.Confirm();
                if (endPanel.SelectedIndex == EndRoomPanel.OptionQuit) SceneLoader.Quit();
                else SceneLoader.Reload();
            }
        }
    }
}
