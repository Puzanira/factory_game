using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Machines;
using LastShift.UI;
using LastShift.Utilities;

namespace LastShift.Core
{
    /// <summary>
    /// The lesson in «УЧЕБНЫЙ ЦЕХ»: THREE things done by hand, on the real machines,
    /// the real engineer AI and the real interface.
    ///   1. pick a system in the terminal list  (joystick)
    ///   2. switch it on and see what it does   (red button)
    ///   3. wait for «ПОДХОДЯЩИЙ МОМЕНТ», then fire
    ///
    /// It used to be nine steps, most of them cards of text at a frozen room, and it
    /// sat behind a «пройти урок / начать смену» choice. At the live cabinet
    /// (founder, 2026-09) nobody took it and nobody understood the game: «игрок не
    /// успевает вообще понимать, что происходит». So the choice is gone — every
    /// player walks this — and everything that was explained rather than done was
    /// cut: the control resource, the resolve gauge and combinations are now learnt
    /// by playing the first room, which announces them as they happen.
    ///
    /// The lesson can never hard-fail — a mistake replays the current situation,
    /// nothing more. Input is the shared Up/Down/Enter funnel; no tutorial-only
    /// input path.
    /// </summary>
    public class TutorialFlowController : MonoBehaviour
    {
        const int TotalSteps = 3;

        /// <summary>How long «УПРАВЛЕНИЕ ОСВОЕНО» stays up before the shift starts.</summary>
        const float DoneAutoStartSeconds = 3.2f;

        LevelManager lm;
        RoomRefs refs;

        DoorMachine door;
        ConveyorMachine conveyor;
        RoboticArmMachine arm;

        Canvas canvas;
        TutorialStepController steps;

        int step;
        bool advancing;
        Vector2 spawn;

        // Practical-step bookkeeping.
        bool step1Armed;
        bool armCycleSeen;
        float situationResetAt;
        float repairWatch;
        const float StepRetrySeconds = 26f;

        // Completion panel.
        GameObject donePanel;
        bool doneStarting;

        public void Init(LevelManager levelManager, RoomRefs roomRefs, HUDController hudController)
        {
            lm = levelManager;
            refs = roomRefs;
            spawn = new Vector2(0f, -3.2f);

            foreach (var m in refs.machines)
            {
                if (m is DoorMachine d) door = d;
                else if (m is ConveyorMachine c) conveyor = c;
                else if (m is RoboticArmMachine a) arm = a;
                if (m != null) m.Judged += OnJudged;
            }

            var e = lm.Engineer;
            e.StunnedBy += OnStunned;

            canvas = UIBuilder.CreateCanvas("TutorialCanvas", 12);
            steps = gameObject.AddComponent<TutorialStepController>();
            steps.Init(lm, canvas);
            BuildDonePanel(canvas.transform);

            StartStep(1);
        }

        // ================= target providers =================

        static Func<Rect> ZoneTarget(InteractableMachine m) => () =>
        {
            if (m == null) return new Rect();
            Rect r = m.EffectiveZoneRect;
            return TutorialUiSpace.ScreenRectOfWorld(r.center, r.size);
        };

        static Func<Rect> UiTarget(RectTransform rt) =>
            () => rt == null ? new Rect() : TutorialUiSpace.ScreenRectOf(rt);

        CommandTerminalUI TerminalUi => lm != null ? lm.TerminalUi : null;

        // ================= step flow =================

        void StartStep(int index)
        {
            step = index;
            advancing = false;
            armCycleSeen = false;
            situationResetAt = Time.time;
            repairWatch = 0f;

            var terminalUi = TerminalUi;
            switch (step)
            {
                case 1: // pick a system — the joystick, and nothing else
                    SetGate(m => false);
                    // The cursor starts on the belt so that «ДЖОЙСТИК ВВЕРХ» is a
                    // real move to a different row, not a press that changes nothing.
                    step1Armed = false;
                    if (lm.Terminal != null) lm.Terminal.TutorialPreselect(conveyor);
                    OpenRouteThroughArm();
                    steps.ShowPractical(1, TotalSteps, Loc.TutStep1Header, Loc.TutStep1Body, Loc.TutStep1Footer,
                        terminalUi != null ? UiTarget(terminalUi.CommandListRect) : null,
                        TutorialHighlightShape.Rect, TutorialCalloutSide.Right, dimAround: true);
                    steps.SetStatus("", false);
                    break;

                case 2: // switch it on: the gate cuts the route in front of him
                    OpenRouteThroughArm();
                    SetGate(m => m == door);
                    steps.ShowPractical(2, TotalSteps, Loc.TutStep2Header, Loc.TutStep2Body, Loc.TutActFooter,
                        door != null ? ZoneTarget(door) : null, TutorialHighlightShape.Rect);
                    steps.SetStatus("", false);
                    break;

                case 3: // the moment: only the arm, and only while he is in its zone
                    OpenRouteThroughArm();
                    ResetSituation(null);
                    SetGate(m => m == arm);
                    steps.ShowPractical(3, TotalSteps, Loc.TutStep3Header, Loc.TutStep3Body, Loc.TutStep3Footer,
                        arm != null ? ZoneTarget(arm) : null, TutorialHighlightShape.Rect);
                    // No status line on this step: «ПОДХОДЯЩИЙ МОМЕНТ» is already the
                    // card's title AND the mark in the manipulator's row, and a third
                    // copy of it (plus «ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» after the hit) only
                    // got in the way. The row is the thing to watch.
                    steps.SetStatus("", false);
                    break;

                case 4:
                    ShowDonePanel();
                    break;
            }
        }

        void Advance(int nextStep, float delay)
        {
            if (advancing) return;
            advancing = true;
            SetGate(m => false);
            StartCoroutine(AdvanceRoutine(nextStep, delay));
        }

        IEnumerator AdvanceRoutine(int nextStep, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            steps.HideAll();
            StartStep(nextStep);
        }

        void SetGate(Func<InteractableMachine, bool> gate)
        {
            if (lm != null && lm.Terminal != null) lm.Terminal.TutorialGate = gate;
        }

        /// <summary>The lesson route must run past the arm: the training gate stays open.</summary>
        void OpenRouteThroughArm()
        {
            if (door != null && door.IsClosed) door.ForceActivate();
        }

        // ================= machine / engineer signals =================

        void OnJudged(InteractableMachine machine, bool effective)
        {
            if (advancing) return;

            if (step == 2 && machine == door)
            {
                // Judged fires before the slab toggles: an open gate means this
                // activation is the closing move the step asks for.
                if (!door.IsClosed)
                {
                    steps.SetHighlightColor(TutorialHighlightTarget.Green);
                    steps.SetStatus(Loc.TutStep2Done, false);
                    Advance(3, 2.6f);
                }
                return;
            }

            if (step == 3 && machine == arm && !effective)
            {
                // Fired too early: a warning and a fresh approach, never a failure.
                // The warning is a toast, not a permanent line on the card.
                lm.ShowToast(Loc.TutEarlyActivation, 2.8f, warning: true);
                steps.LockInput(0.8f);
                StartCoroutine(RetryAfter(1.6f));
            }
        }

        IEnumerator RetryAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (advancing) yield break;
            ResetSituation(null);
        }

        void OnStunned(string source)
        {
            if (advancing) return;
            if (step == 3)
            {
                // «ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» and «РЕШИМОСТЬ −10» already arrive through
                // the shared feedback path; the step just goes green and moves on.
                steps.SetHighlightColor(TutorialHighlightTarget.Green);
                Advance(4, 2.4f);
            }
        }

        /// <summary>The lesson objective can never actually be finished.</summary>
        public void OnObjectiveRepaired() => ResetSituation(null);

        /// <summary>
        /// Puts the engineer back on his approach so the same situation plays out
        /// again, with fresh repair progress. Never a failure.
        /// </summary>
        void ResetSituation(string message)
        {
            if (advancing) return;
            situationResetAt = Time.time;
            armCycleSeen = false;
            repairWatch = 0f;
            // A replayed situation must always be solvable: never hand the player a
            // room whose route is still blocked by an earlier step.
            OpenRouteThroughArm();
            foreach (var obj in refs.objectives)
                if (obj != null) obj.ResetProgress();
            if (!string.IsNullOrEmpty(message)) lm.ShowToast(message, 2.6f, warning: true);
            var e = lm.Engineer;
            if (e != null && !e.IsEscaped) e.TutorialReset(spawn);
        }

        // ================= per-frame =================

        void Update()
        {
            if (lm == null || steps == null) return;

            // TutorialStepController applies the input lock itself (execution order
            // -60, before the terminal polls Submit).
            bool doneVisible = donePanel != null && donePanel.activeSelf;
            steps.ExternalBlock = doneVisible;

            // The pause menu lives on the game canvas: never cover it.
            if (canvas != null) canvas.enabled = !lm.IsPaused;
            if (lm.IsPaused) return;

            if (doneVisible) { HandleDonePanel(); return; }
            if (advancing) return;

            // Safety net: the engineer must never finish the training repair.
            if (refs.objectives.Count > 0 && refs.objectives[0].Progress01 > 0.3f)
                OnObjectiveRepaired();

            if (step == 1) UpdateSelection();
            else if (step == 2) UpdateGateStep();
            else if (step == 3) UpdateTheMoment();
        }

        /// <summary>
        /// Step 1 ends when the player has actually steered to the gate — and not
        /// before. The terminal parks its own cursor on the first ready command as
        /// soon as it has one, and that is the gate; if the step started before the
        /// list existed, our preselect did nothing and the terminal would have
        /// "chosen" for the player, lifting the dimming a moment after it appeared.
        /// So the step arms only once the cursor is demonstrably off the gate.
        /// </summary>
        void UpdateSelection()
        {
            var terminal = lm.Terminal;
            if (terminal == null || door == null) return;

            if (!step1Armed)
            {
                if (conveyor == null) { step1Armed = true; return; }     // nothing to park on
                if (terminal.Selected != null && terminal.Selected != door) step1Armed = true;
                else terminal.TutorialPreselect(conveyor);
                return;
            }

            if (terminal.Selected == door)
            {
                // Chosen: give the room back to the eye before the next step starts.
                steps.Undim();
                steps.SetHighlightColor(TutorialHighlightTarget.Green);
                Advance(2, 0.7f);
            }
        }

        void UpdateGateStep()
        {
            WatchRepairStall();
            WatchStall();
        }

        void UpdateTheMoment()
        {
            if (arm == null) return;
            bool inZone = arm.EngineerInEffectiveZone;
            steps.SetHighlightColor(inZone
                ? TutorialHighlightTarget.Green
                : TutorialHighlightTarget.Amber);

            // An arm cycle that ended without a stun was a miss: walk him back in.
            if (arm.State == MachineState.Active) armCycleSeen = true;
            else if (armCycleSeen) ResetSituation(Loc.TutorialRetry);

            WatchStall();
        }

        /// <summary>
        /// Slipping through to the panel means the step's trap failed: replay the
        /// approach once he has visibly been repairing for a moment.
        /// </summary>
        void WatchRepairStall()
        {
            var e = lm.Engineer;
            bool repairing = e != null && e.Fsm != null &&
                e.Fsm.CurrentId == LastShift.Engineer.EngineerStateId.RepairObjective;
            if (repairing)
            {
                repairWatch += Time.deltaTime;
                if (repairWatch >= 2f) { repairWatch = 0f; ResetSituation(null); }
            }
            else repairWatch = 0f;
        }

        /// <summary>Nothing happened for a long while: replay the situation silently.</summary>
        void WatchStall()
        {
            if (arm != null && arm.State == MachineState.Active) return; // never mid-strike
            if (Time.time - situationResetAt > StepRetrySeconds) ResetSituation(null);
        }

        // ================= completion =================

        void BuildDonePanel(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "TutorialDone", Vector2.zero, Vector2.one,
                new Color(0.008f, 0.018f, 0.014f, 1f));
            donePanel = rt.gameObject;

            RectTransform card = UIBuilder.Panel(rt, "Card", new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f),
                new Color(0.012f, 0.032f, 0.024f, 1f));
            Frame(card, new Color(0.34f, 0.7f, 0.45f, 0.85f), 2.5f);
            Brackets(card, new Color(0.95f, 0.85f, 0.45f));

            var scan = UIBuilder.Panel(card, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.3f);
            scanImg.raycastTarget = false;

            var title = UIBuilder.Label(card, "Title", Loc.TutorialDoneHeader, 52,
                new Color(0.78f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.82f));
            UIBuilder.Panel(card, "TitleLine", new Vector2(0.2f, 0.605f), new Vector2(0.8f, 0.6085f),
                new Color(0.34f, 0.7f, 0.45f, 0.55f));

            var body = UIBuilder.Label(card, "Body", Loc.TutorialDoneBody, 28,
                new Color(0.7f, 0.95f, 0.76f), TextAnchor.MiddleCenter);
            SetRect(body.rectTransform, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.58f));

            // No menu here on purpose: the shift starts by itself. One press fewer
            // between a person at the cabinet and the game.
            var starts = UIBuilder.Label(card, "ShiftStarts", Loc.TutorialDoneShiftStarts, 26,
                new Color(0.95f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(starts.rectTransform, new Vector2(0f, 0.18f), new Vector2(1f, 0.27f));

            donePanel.SetActive(false);
        }

        static void Frame(Transform parent, Color color, float thickness)
        {
            Edge(parent, "EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), color);
            Edge(parent, "EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), color);
        }

        static void Brackets(Transform parent, Color color)
        {
            const float len = 30f, t = 3.5f;
            Edge(parent, "BrBL_h", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBL_v", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrBR_h", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBR_v", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrTL_h", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTL_v", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(t, len), color);
            Edge(parent, "BrTR_h", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTR_v", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(t, len), color);
        }

        static void Edge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void ShowDonePanel()
        {
            steps.HideAll();
            steps.LockInput(0.5f);
            steps.ExternalBlock = true;
            SetGate(m => false);
            lm.TutorialOverlayLock = true;
            lm.TutorialTimeFreeze = false;
            Time.timeScale = 1f;
            donePanel.SetActive(true);
            Audio.AudioManager.Play("room_won", Audio.SfxBus.UI, 0.5f);
            StartCoroutine(AutoStartShift());
        }

        IEnumerator AutoStartShift()
        {
            yield return new WaitForSecondsRealtime(DoneAutoStartSeconds);
            StartShift();
        }

        /// <summary>The red button only makes the waiting shorter; it is not required.</summary>
        void HandleDonePanel()
        {
            if (steps.BlocksGameInput) return;
            if (!GameInput.ConfirmPressed) return;
            Audio.UiSfx.Confirm();
            StartShift();
        }

        void StartShift()
        {
            if (doneStarting) return;
            doneStarting = true;
            GameManager.TutorialRequested = false;
            SceneLoader.Load(GameManager.Level1Scene);
        }

        /// <summary>Smoke-test introspection: current lesson step (1..3).</summary>
        public int DevStep => step;

        /// <summary>
        /// Smoke-test introspection: the system the current step needs SELECTED
        /// (null unless the step is the selection one).
        /// </summary>
        public InteractableMachine DevRequiredSelection => step == 1 ? (InteractableMachine)door : null;

        /// <summary>
        /// Smoke-test introspection: the system the current step needs ACTIVATED
        /// (null while no activation is asked for).
        /// </summary>
        public InteractableMachine DevRequiredMachine
        {
            get
            {
                if (step == 2) return door;
                if (step == 3) return arm;
                return null;
            }
        }

        /// <summary>
        /// Smoke-test guard: a step must never require a system the player is not
        /// allowed to activate, or a route the previous step left blocked.
        /// Must always be false.
        /// </summary>
        public bool DevRouteBlocked =>
            step == 3 && door != null && door.IsClosed;

        /// <summary>Smoke-test introspection: the step presenter (layout checks).</summary>
        public TutorialStepController DevSteps => steps;
    }
}
