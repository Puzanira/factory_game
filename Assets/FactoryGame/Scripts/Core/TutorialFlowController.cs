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
    /// The interactive lesson in «УЧЕБНЫЙ ЦЕХ»: ten steps on the real machines, the
    /// real engineer AI and the real interface. Each step darkens everything else,
    /// marks exactly one target and waits for Submit; the practical steps hand the
    /// room back and only allow the system the step is about. The lesson can never
    /// hard-fail — a mistake resets the current small task, nothing more.
    /// Input is the shared Up/Down/Enter funnel; no tutorial-only input path.
    /// </summary>
    public class TutorialFlowController : MonoBehaviour
    {
        const int TotalSteps = 10;

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
        bool armCycleSeen;
        float situationResetAt;
        float repairWatch;
        const float StepRetrySeconds = 26f;

        // Step 9 sub-sequence: gate, then conveyor. The lesson ends there — the arm
        // was already taught (and practised) in steps 7-8.
        enum ComboPhase { None, GateInfo, GateAct, BeltInfo, BeltAct }
        ComboPhase combo = ComboPhase.None;
        float gateClosedAt = -999f;

        // Completion panel.
        GameObject donePanel;

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

        static Func<Rect> WorldTarget(Transform t, Vector2 size, Vector2 offset) =>
            () => t == null ? new Rect() : TutorialUiSpace.ScreenRectOfWorld((Vector2)t.position + offset, size);

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
            combo = ComboPhase.None;
            armCycleSeen = false;
            gateClosedAt = -999f;
            situationResetAt = Time.time;
            repairWatch = 0f;

            var terminalUi = TerminalUi;
            switch (step)
            {
                case 1: // the engineer himself — irregular shape, corner brackets only
                    SetGate(m => false);
                    steps.ShowInfo(1, TotalSteps, Loc.TutStep1Header, Loc.TutStep1Body,
                        lm.Engineer != null ? WorldTarget(lm.Engineer.transform, new Vector2(1.3f, 1.9f), new Vector2(0f, 0.25f)) : null,
                        TutorialHighlightShape.Brackets, () => StartStep(2),
                        // He spawns at the bottom of the room: the card belongs above
                        // him, not squeezed into the bottom-right corner.
                        TutorialCalloutSide.Above);
                    break;

                case 2: // repair console — rectangular object, rectangular frame
                    SetGate(m => false);
                    steps.ShowInfo(2, TotalSteps, Loc.TutStep2Header, Loc.TutStep2Body,
                        refs.objectives.Count > 0
                            ? WorldTarget(refs.objectives[0].transform, new Vector2(1.9f, 2.6f), new Vector2(0f, 0.15f))
                            : null,
                        TutorialHighlightShape.Rect, () => StartStep(3));
                    break;

                case 3: // command list — rectangular UI panel
                    SetGate(m => false);
                    steps.ShowInfo(3, TotalSteps, Loc.TutStep3Header, Loc.TutStep3Body,
                        terminalUi != null ? UiTarget(terminalUi.CommandListRect) : null,
                        TutorialHighlightShape.Rect, () => StartStep(4));
                    break;

                case 4: // lower-left detail panel
                    SetGate(m => false);
                    steps.ShowInfo(4, TotalSteps, Loc.TutStep4Header, Loc.TutStep4Body,
                        terminalUi != null ? UiTarget(terminalUi.Detail.PanelRect) : null,
                        TutorialHighlightShape.Rect, () => StartStep(5));
                    break;

                case 5: // control resource row inside the detail panel
                    SetGate(m => false);
                    steps.ShowInfo(5, TotalSteps, Loc.TutStep5Header, Loc.TutStep5Body,
                        terminalUi != null ? UiTarget(terminalUi.Detail.ResourceRect) : null,
                        TutorialHighlightShape.Rect, () => StartStep(6));
                    break;

                case 6: // engineer resolve row inside the detail panel
                    SetGate(m => false);
                    steps.ShowInfo(6, TotalSteps, Loc.TutStep6Header, Loc.TutStep6Body,
                        terminalUi != null ? UiTarget(terminalUi.Detail.ResolveRect) : null,
                        TutorialHighlightShape.Rect, () => StartStep(7));
                    break;

                case 7: // the arm's real effective zone in world space
                    SetGate(m => false);
                    OpenRouteThroughArm();
                    steps.ShowInfo(7, TotalSteps, Loc.TutStep7Header, Loc.TutStep7Body,
                        arm != null ? ZoneTarget(arm) : null,
                        TutorialHighlightShape.Rect, () => StartStep(8));
                    break;

                case 8: // first activation: only the arm, only at the right moment
                    OpenRouteThroughArm();
                    ResetSituation(null);
                    SetGate(m => m == arm);
                    steps.ShowPractical(8, TotalSteps, Loc.TutStep8Header, Loc.TutStep8Body,
                        arm != null ? ZoneTarget(arm) : null, TutorialHighlightShape.Rect);
                    steps.SetStatus(Loc.TutWaitOutOfZone, false);
                    break;

                case 9: // combination: gate → conveyor → arm, one target at a time
                    OpenRouteThroughArm();
                    ResetSituation(null);
                    EnterComboPhase(ComboPhase.GateInfo);
                    break;

                case 10:
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

        // ================= step 9: the combination sub-sequence =================

        void EnterComboPhase(ComboPhase phase)
        {
            combo = phase;
            situationResetAt = Time.time;
            switch (phase)
            {
                case ComboPhase.GateInfo:
                    SetGate(m => false);
                    steps.ShowInfo(9, TotalSteps, Loc.TutStep9Header, Loc.TutStep9Body,
                        door != null ? ZoneTarget(door) : null, TutorialHighlightShape.Rect,
                        () => EnterComboPhase(ComboPhase.GateAct));
                    break;

                case ComboPhase.GateAct:
                    SetGate(m => m == door);
                    steps.ShowPractical(9, TotalSteps, Loc.TutComboGateHeader, Loc.TutComboGateBody,
                        door != null ? ZoneTarget(door) : null, TutorialHighlightShape.Rect);
                    steps.SetStatus(Loc.TutorialFooterAction, false);
                    break;

                case ComboPhase.BeltInfo:
                    SetGate(m => false);
                    steps.ShowInfo(9, TotalSteps, Loc.TutComboConveyorHeader, Loc.TutComboConveyorBody,
                        conveyor != null ? ZoneTarget(conveyor) : null, TutorialHighlightShape.Rect,
                        () => EnterComboPhase(ComboPhase.BeltAct));
                    break;

                case ComboPhase.BeltAct:
                    SetGate(m => m == conveyor);
                    steps.ShowPractical(9, TotalSteps, Loc.TutComboConveyorHeader, Loc.TutComboConveyorBody,
                        conveyor != null ? ZoneTarget(conveyor) : null, TutorialHighlightShape.Rect);
                    break;
            }
        }

        /// <summary>A mistake resets this small sequence only — never the whole lesson.</summary>
        void ResetCombo()
        {
            if (advancing) return;
            lm.ShowToast(Loc.TutSequenceReset, 2.8f, warning: true);
            gateClosedAt = -999f;
            ResetSituation(null); // re-opens the route as well
            EnterComboPhase(ComboPhase.GateAct);
        }

        // ================= machine / engineer signals =================

        void OnJudged(InteractableMachine machine, bool effective)
        {
            if (advancing) return;

            if (step == 8 && machine == arm && !effective)
            {
                // Fired too early: a warning and a fresh approach, never a failure.
                steps.SetStatus(Loc.TutEarlyActivation, true);
                lm.ShowToast(Loc.TutEarlyActivation, 2.8f, warning: true);
                steps.LockInput(0.8f);
                StartCoroutine(RetryAfter(1.6f));
                return;
            }

            if (step != 9) return;

            switch (combo)
            {
                case ComboPhase.GateAct:
                    // Judged fires before the slab toggles: an open gate means this
                    // activation is the closing move the step asks for.
                    if (machine == door && !door.IsClosed)
                    {
                        gateClosedAt = Time.time;
                        EnterComboPhase(ComboPhase.BeltInfo);
                    }
                    break;

                case ComboPhase.BeltAct:
                    if (machine == conveyor)
                    {
                        if (gateClosedAt > 0f)
                        {
                            // Gate then belt: that is the combination. «ПЕРЕНАПРАВЛЕНИЕ»
                            // is announced by the real combination tracker when it
                            // genuinely lands, so no toast is duplicated here.
                            steps.SetHighlightColor(TutorialHighlightTarget.Green);
                            steps.SetStatus(effective ? Loc.ComboRedirect : Loc.EffectiveActivation, false);
                            Advance(10, 2.4f);
                        }
                        else ResetCombo();
                    }
                    break;
            }
        }

        IEnumerator RetryAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (advancing) yield break;
            ResetSituation(null);
            steps.SetStatus(Loc.TutWaitOutOfZone, false);
        }

        void OnStunned(string source)
        {
            if (advancing) return;
            if (step == 8)
            {
                // «ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» and «РЕШИМОСТЬ −10» already arrive through
                // the shared feedback path; the step just confirms and moves on.
                steps.SetHighlightColor(TutorialHighlightTarget.Green);
                steps.SetStatus(Loc.EffectiveActivation, false);
                Advance(9, 2.4f);
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

            // TutorialStepController applies the clock freeze and the input lock
            // itself (execution order -60, before the terminal polls Submit).
            bool doneVisible = donePanel != null && donePanel.activeSelf;
            steps.ExternalBlock = doneVisible;

            // The pause menu lives on the game canvas: never cover it.
            if (canvas != null) canvas.enabled = !lm.IsPaused;
            if (lm.IsPaused) return;

            if (doneVisible) { HandleDoneMenu(); return; }
            if (advancing) return;

            // Safety net: the engineer must never finish the training repair.
            if (refs.objectives.Count > 0 && refs.objectives[0].Progress01 > 0.3f)
                OnObjectiveRepaired();

            if (step == 8) UpdateFirstActivation();
            else if (step == 9) UpdateCombination();
        }

        void UpdateFirstActivation()
        {
            if (arm == null) return;
            bool inZone = arm.EngineerInEffectiveZone;
            steps.SetStatus(inZone ? Loc.TutGoodMoment : Loc.TutWaitOutOfZone, false);
            steps.SetHighlightColor(inZone
                ? TutorialHighlightTarget.Green
                : TutorialHighlightTarget.Amber);

            // An arm cycle that ended without a stun was a miss: walk him back in.
            if (arm.State == MachineState.Active) armCycleSeen = true;
            else if (armCycleSeen) ResetSituation(Loc.TutorialRetry);

            WatchStall();
        }

        void UpdateCombination()
        {
            if (combo == ComboPhase.BeltAct && conveyor != null)
            {
                bool onBelt = conveyor.EngineerInEffectiveZone;
                steps.SetStatus(onBelt ? Loc.TutGoodMoment : Loc.TutWaitOutOfZone, false);
                steps.SetHighlightColor(onBelt ? TutorialHighlightTarget.Green : TutorialHighlightTarget.Amber);
            }

            // Slipping through to the panel means the trap failed: reset this
            // sequence (and only it) after he visibly repairs for a moment.
            if (combo == ComboPhase.GateAct || combo == ComboPhase.BeltAct)
            {
                var e = lm.Engineer;
                bool repairing = e != null && e.Fsm != null &&
                    e.Fsm.CurrentId == LastShift.Engineer.EngineerStateId.RepairObjective;
                if (repairing)
                {
                    repairWatch += Time.deltaTime;
                    if (repairWatch >= 2f) { repairWatch = 0f; ResetCombo(); }
                }
                else repairWatch = 0f;
            }

            WatchStall();
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
            SetRect(title.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 0.86f));
            UIBuilder.Panel(card, "TitleLine", new Vector2(0.2f, 0.665f), new Vector2(0.8f, 0.6685f),
                new Color(0.34f, 0.7f, 0.45f, 0.55f));

            var body = UIBuilder.Label(card, "Body", Loc.TutorialDoneBody, 24,
                new Color(0.7f, 0.95f, 0.76f), TextAnchor.UpperCenter);
            SetRect(body.rectTransform, new Vector2(0.06f, 0.4f), new Vector2(0.94f, 0.64f));

            RectTransform button = UIBuilder.Panel(card, "StartButton", new Vector2(0.28f, 0.2f), new Vector2(0.72f, 0.31f),
                new Color(0.1f, 0.3f, 0.16f, 0.95f));
            Frame(button, new Color(0.95f, 0.85f, 0.45f, 0.85f), 2f);
            var doneButton = UIBuilder.Label(button, "Label", "> " + Loc.TutorialStartShift + " <", 28,
                new Color(0.95f, 1f, 0.7f), TextAnchor.MiddleCenter);
            doneButton.fontStyle = FontStyle.Bold;

            var hint = UIBuilder.Label(card, "Hint", Loc.FooterContinue, 16,
                new Color(0.45f, 0.62f, 0.5f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, 0.1f), new Vector2(1f, 0.17f));

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
        }

        void HandleDoneMenu()
        {
            if (steps.BlocksGameInput) return;
            if (!GameInput.ConfirmPressed) return;
            Audio.UiSfx.Confirm();
            // «НАЧАТЬ СМЕНУ»: straight into the first real room — the instruction
            // pages have already been read, so they are never repeated.
            GameManager.TutorialRequested = false;
            SceneLoader.Load(GameManager.Level1Scene);
        }

        /// <summary>Smoke-test introspection: current lesson step (1..10).</summary>
        public int DevStep => step;

        /// <summary>
        /// Smoke-test introspection: the system the current practical step needs
        /// (null while a text step is up).
        /// </summary>
        public InteractableMachine DevRequiredMachine
        {
            get
            {
                if (step == 8) return arm;
                if (step != 9) return null;
                switch (combo)
                {
                    case ComboPhase.GateAct: return door;
                    case ComboPhase.BeltAct: return conveyor;
                    default: return null;
                }
            }
        }

        /// <summary>
        /// Smoke-test guard: a practical step must never require a system the player
        /// is not allowed to activate. Must always be false.
        /// </summary>
        public bool DevRouteBlocked =>
            step == 8 && door != null && door.IsClosed;
        /// <summary>Smoke-test introspection: the step presenter (layout checks).</summary>
        public TutorialStepController DevSteps => steps;
    }
}
