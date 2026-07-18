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
    /// objective sequence, routes global keyboard input (Esc/R/Enter/Q), and handles
    /// room completion / failure / scene flow.
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
        int objectiveIndex;
        bool paused;
        bool roomComplete;
        bool roomFailed;

        PauseMenuUI pauseMenu;
        EndRoomPanel endPanel;
        HUDController hud;

        public string RoomName => layout != null ? layout.roomName : "";
        public bool Escalated => Escalation != null && Escalation.Escalated;
        public bool RoomEnded => roomComplete || roomFailed;
        public bool InputLocked => paused || roomComplete || roomFailed;

        public RepairObjective CurrentObjectiveForHud
        {
            get
            {
                if (Engineer != null && Engineer.CurrentObjective != null) return Engineer.CurrentObjective;
                if (refs != null && objectiveIndex < refs.objectives.Count) return refs.objectives[objectiveIndex];
                return null;
            }
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
            layout = LevelLayouts.Get(levelData.levelIndex);
            levelData.roomName = layout.roomName;

            PathGrid.Create(Vector2.zero, layout.roomSize + new Vector2(1.5f, 1.5f), 0.5f);
            refs = RoomBuilder.Build(layout, prefabLibrary, transform);
            Exit = refs.exit;
            Exit.CompletedEvent += _ => OnEngineerEscaped();
            foreach (var obj in refs.objectives)
                obj.CompletedEvent += OnObjectiveCompleted;

            Pressure = gameObject.AddComponent<RoomPressureController>();
            Pressure.Init(levelData.maxPressure);
            Escalation = gameObject.AddComponent<RoomEscalationController>();
            Escalation.Init(this, levelData.escalationData, refs.machines, refs.hazards);

            SpawnEngineer();
            BuildUI();
            SetupCamera();
            gameObject.AddComponent<RoomAudioDirector>().Init(this, levelData.levelIndex);

            objectiveIndex = 0;
            if (refs.objectives.Count > 0) Engineer.SetObjective(refs.objectives[0]);

            WireToasts();
            if (levelData.levelIndex == 0) StartCoroutine(TutorialHints());
        }

        void WireToasts()
        {
            if (hud == null) return;
            foreach (var m in refs.machines)
            {
                if (m == null) continue;
                var machine = m;
                machine.Activated += _ => hud.ShowToast(machine.ActivationMessage);
            }
            Pressure.ComboBonus += amount =>
                hud.ShowToast(string.Format(Data.Loc.ComboToast, Mathf.RoundToInt(amount)));
            Pressure.WarningReached += () => hud.ShowToast(Data.Loc.PressureWarning, 3.5f, warning: true);
            Pressure.MaxReached += () => hud.ShowToast(Data.Loc.EscalationToast, 3.5f, warning: true);
            Engineer.Stats.ResolveEmpty += () => hud.ShowToast(Data.Loc.ExitUnlockedToast, 3.5f);
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
            pauseMenu = uiRoot.GetComponent<PauseMenuUI>();
            pauseMenu.Build(canvas);
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

            // Fit the whole room into the area right of the terminal (28%) and
            // below the HUD strip (~13.5%), then shift so the room centres there.
            const float panelFrac = 0.28f;
            const float topFrac = 0.135f;
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
            objectiveIndex++;
            if (objectiveIndex < refs.objectives.Count)
            {
                Engineer.SetObjective(refs.objectives[objectiveIndex]);
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
            roomComplete = true;
            if (levelData.finalLevel) endPanel.ShowSliceComplete();
            else endPanel.ShowRoomComplete();
        }

        // ---------------- input routing ----------------

        void Update()
        {
            if (roomComplete)
            {
                if (levelData.finalLevel)
                {
                    // Final «ЗАВОД ПОБЕДИЛ» menu: Up/Down select, Enter confirms, Esc jumps to quit.
                    if (GameInput.UpPressed) endPanel.MoveSelection(-1);
                    if (GameInput.DownPressed) endPanel.MoveSelection(+1);
                    if (GameInput.EscapePressed) endPanel.SelectQuit();
                    if (GameInput.ConfirmPressed)
                    {
                        UiSfx.Confirm();
                        switch (endPanel.SelectedIndex)
                        {
                            case 1: SceneLoader.Load(GameManager.Level1Scene); break;
                            case 2: SceneLoader.Quit(); break;
                            default: SceneLoader.Load(GameManager.BootScene); break;
                        }
                    }
                }
                else
                {
                    if (GameInput.ConfirmPressed) { UiSfx.Confirm(); SceneLoader.Load(levelData.nextSceneName); }
                }
                return;
            }

            if (roomFailed)
            {
                // Defeat screen: Enter retries the room (quit stays available
                // through the pause menu after the restart).
                if (GameInput.ConfirmPressed) { UiSfx.Confirm(); SceneLoader.Reload(); }
                return;
            }

            if (paused)
            {
                // Keyboard menu navigation (incl. Russian sound settings) lives in the menu.
                var action = pauseMenu != null ? pauseMenu.HandleInput() : PauseMenuUI.PauseAction.Resume;
                switch (action)
                {
                    case PauseMenuUI.PauseAction.Resume: SetPaused(false); break;
                    case PauseMenuUI.PauseAction.Restart: SceneLoader.Reload(); break;
                    case PauseMenuUI.PauseAction.Quit: SceneLoader.Quit(); break;
                }
                return;
            }

            if (GameInput.EscapePressed) { SetPaused(true); return; }

            // Passive pressure while the engineer is significantly delayed:
            // stunned, panicking, dodging hazards or hunting for a route.
            if (Engineer != null && Engineer.Fsm != null && Pressure != null)
            {
                var id = Engineer.Fsm.CurrentId;
                if (id == LastShift.Engineer.EngineerStateId.Stunned ||
                    id == LastShift.Engineer.EngineerStateId.Panic ||
                    id == LastShift.Engineer.EngineerStateId.AvoidHazard ||
                    id == LastShift.Engineer.EngineerStateId.Repath)
                {
                    Pressure.AddPassive(0.5f * Time.deltaTime);
                }
            }

            // Editor-only fast-forward; does nothing in released builds.
            Time.timeScale = GameInput.SpeedHeld ? 3f : 1f;
        }

        void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            AudioManager.SetGamePaused(value);
            UiSfx.PauseConfirm();
            if (pauseMenu != null)
            {
                if (value) pauseMenu.Show();
                else pauseMenu.Hide();
            }
        }
    }
}
