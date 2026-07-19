using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Machines;
using LastShift.UI;
using LastShift.Utilities;

namespace LastShift.Core
{
    /// <summary>
    /// Interactive tutorial: four short steps in the «УЧЕБНЫЙ ЦЕХ» room, using the
    /// real machines, real engineer AI and the shared Up/Down/Enter input funnel.
    /// Steps gate which system may be activated (selection stays free), reset the
    /// engineer between steps, and can never hard-fail. Ends with «УПРАВЛЕНИЕ
    /// ОСВОЕНО» and a «НАЧАТЬ СМЕНУ» / «ПОВТОРИТЬ УРОК» menu.
    /// </summary>
    public class TutorialFlowController : MonoBehaviour
    {
        LevelManager lm;
        RoomRefs refs;
        HUDController hud;

        DoorMachine door;
        ConveyorMachine conveyor;
        RoboticArmMachine arm;

        int step;               // 1..4, 5 = completion panel
        bool stepAdvancing;

        // Retry-until-success: if the target action was not completed, the
        // situation repeats — the engineer walks back in and tries again.
        const float StepRetrySeconds = 22f;
        float situationResetAt;
        bool armCycleSeen; // step 2: an arm activation is in flight

        // Step 3 sequencing.
        float doorClosedAt = -999f;
        float conveyorActivatedAt = -999f;

        // Instruction card UI.
        Text cardHeader;
        Text cardBody;
        Text cardStep;

        // Completion panel.
        GameObject donePanel;
        readonly List<Text> doneMenu = new List<Text>();
        readonly List<Image> doneMenuBgs = new List<Image>();
        int doneIndex;

        GameObject targetRing;
        Vector2 spawn;

        public void Init(LevelManager levelManager, RoomRefs roomRefs, HUDController hudController)
        {
            lm = levelManager;
            refs = roomRefs;
            hud = hudController;
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
            e.ConveyorCarried += OnConveyorCarried;

            BuildUI();
            StartStep(1);
        }

        // ================= UI =================

        void BuildUI()
        {
            Canvas canvas = UIBuilder.CreateCanvas("TutorialCanvas", 12);

            // Instruction card along the bottom of the play area.
            RectTransform card = UIBuilder.Panel(canvas.transform, "TutorialCard",
                new Vector2(0.31f, 0.055f), new Vector2(0.97f, 0.215f), new Color(0.02f, 0.06f, 0.04f, 0.94f));
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            cardHeader = UIBuilder.Label(card, "Header", "", 24, new Color(0.95f, 0.85f, 0.45f), TextAnchor.MiddleLeft);
            SetRect(cardHeader.rectTransform, new Vector2(0.025f, 0.62f), new Vector2(0.8f, 0.96f));
            cardStep = UIBuilder.Label(card, "Step", "", 16, new Color(0.45f, 0.68f, 0.52f), TextAnchor.MiddleRight);
            SetRect(cardStep.rectTransform, new Vector2(0.8f, 0.62f), new Vector2(0.975f, 0.96f));
            cardBody = UIBuilder.Label(card, "Body", "", 18, new Color(0.7f, 1f, 0.78f), TextAnchor.UpperLeft);
            SetRect(cardBody.rectTransform, new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.6f));

            BuildDonePanel(canvas.transform);
        }

        void BuildDonePanel(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "TutorialDone",
                Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.015f, 0.94f));
            donePanel = rt.gameObject;

            var scan = UIBuilder.Panel(rt, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.35f);
            scanImg.raycastTarget = false;

            var title = UIBuilder.Label(rt, "Title", Loc.TutorialDoneHeader, 58, new Color(0.75f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.8f));
            var body = UIBuilder.Label(rt, "Body", Loc.TutorialDoneBody, 26, new Color(0.65f, 0.85f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(body.rectTransform, new Vector2(0f, 0.44f), new Vector2(1f, 0.62f));

            string[] options = { Loc.TutorialStartShift, Loc.TutorialRepeat };
            for (int i = 0; i < options.Length; i++)
            {
                RectTransform row = UIBuilder.Panel(rt, "Option" + i,
                    new Vector2(0.36f, 0.3f - i * 0.075f), new Vector2(0.64f, 0.365f - i * 0.075f),
                    new Color(0f, 0f, 0f, 0f));
                doneMenuBgs.Add(row.GetComponent<Image>());
                doneMenu.Add(UIBuilder.Label(row, "Label", options[i], 26, new Color(0.6f, 0.85f, 0.65f), TextAnchor.MiddleCenter));
            }
            var hint = UIBuilder.Label(rt, "Hint", Loc.MenuHint, 15, new Color(0.4f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.16f));

            donePanel.SetActive(false);
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================= step flow =================

        void StartStep(int index)
        {
            step = index;
            stepAdvancing = false;
            armCycleSeen = false;
            situationResetAt = Time.time;
            doorClosedAt = -999f;
            conveyorActivatedAt = -999f;

            foreach (var obj in refs.objectives)
                if (obj != null) obj.ResetProgress();
            var e = lm.Engineer;
            if (e != null && !e.IsEscaped) e.TutorialReset(spawn);

            switch (step)
            {
                case 1:
                    // Door must start open so closing it is the activation to learn.
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep1Header, Loc.TutorialStep1Body);
                    SetGate(m => m == door);
                    Highlight(door);
                    break;

                case 2:
                    // The only route now leads through the arm radius: door open.
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep2Header, Loc.TutorialStep2Body);
                    SetGate(m => m == arm);
                    Highlight(arm);
                    break;

                case 3:
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep3Header, Loc.TutorialStep3Body + "\n" + Loc.TutorialStep3Hint);
                    SetGate(m => m == door || m == conveyor);
                    Highlight(door);
                    break;

                case 4:
                    SetCard(Loc.TutorialStep4Header, Loc.TutorialStep4Body);
                    SetGate(null);
                    Highlight(null);
                    break;
            }
        }

        void SetCard(string header, string body)
        {
            if (cardHeader != null) cardHeader.text = header;
            if (cardBody != null) cardBody.text = body;
            if (cardStep != null) cardStep.text = string.Format(Loc.TutorialStepLabel, step);
        }

        void SetGate(System.Func<InteractableMachine, bool> gate)
        {
            if (lm.Terminal != null) lm.Terminal.TutorialGate = gate;
        }

        /// <summary>Pulsing ring around the machine the current step is about.</summary>
        void Highlight(InteractableMachine machine)
        {
            if (targetRing != null) Destroy(targetRing);
            targetRing = null;
            if (machine == null) return;
            targetRing = new GameObject("TutorialTargetRing");
            targetRing.transform.position = machine.transform.position;
            var sr = Viz.Make("Ring", targetRing.transform, PlaceholderShape.Ring,
                new Color(0.95f, 0.85f, 0.45f, 0.85f), Vector2.zero, new Vector2(2.6f, 2.6f), 12, unlit: true);
            targetRing.AddComponent<SelectionPulse>().target = sr.transform;
        }

        void Advance(string feedback, float delay = 1.6f)
        {
            if (stepAdvancing) return;
            stepAdvancing = true;
            if (!string.IsNullOrEmpty(feedback)) lm.ShowToast(feedback, 2.6f);
            Audio.UiSfx.Confirm();
            StartCoroutine(AdvanceRoutine(delay));
        }

        IEnumerator AdvanceRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (step >= 4) ShowDonePanel();
            else StartStep(step + 1);
        }

        // ================= step completion signals =================

        void OnJudged(InteractableMachine machine, bool effective)
        {
            if (stepAdvancing) return;
            switch (step)
            {
                case 1:
                    if (machine == door) Advance(Loc.TutorialStep1Done);
                    break;

                case 2:
                    // Too early: gentle retry, never a fail (arm cooldown gives the pause).
                    if (machine == arm && !effective)
                        lm.ShowToast(Loc.TutorialStep2Early, 2.6f, warning: true);
                    break;

                case 3:
                    // Judged fires before the slab toggles: an open door means this
                    // activation is the closing move the step asks for.
                    if (machine == door && !door.IsClosed)
                        doorClosedAt = Time.time;
                    if (machine == conveyor)
                    {
                        if (doorClosedAt > 0f)
                        {
                            conveyorActivatedAt = Time.time;
                            // Reversing the belt while the engineer is ON it is the
                            // whole lesson — count it immediately, no second flip.
                            if (effective) Advance(Loc.ComboRedirect, 2.2f);
                        }
                        else ResetStep3();
                    }
                    break;

                case 4:
                    if (effective) Advance(Loc.ResourceRestored);
                    break;
            }
        }

        void OnStunned(string source)
        {
            // Step 2 completes on a real arm hit («ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» and
            // «РЕШИМОСТЬ −10» arrive through the shared feedback path).
            if (step == 2 && !stepAdvancing) Advance(null);
        }

        void OnConveyorCarried()
        {
            // Step 3: door first, then the conveyor, then a meaningful displacement.
            if (step != 3 || stepAdvancing) return;
            if (conveyorActivatedAt > doorClosedAt && doorClosedAt > 0f &&
                Time.time - conveyorActivatedAt <= TacticsData.Get().comboWindowSeconds + 4f)
            {
                Advance(null, 2.2f); // combo toasts already fired via the tracker
            }
        }

        void ResetStep3()
        {
            lm.ShowToast(Loc.TutorialStep3Reset, 2.8f, warning: true);
            doorClosedAt = -999f;
            conveyorActivatedAt = -999f;
            RetrySituation(null);
        }

        /// <summary>
        /// The target action was missed or the step stalled: put the engineer back
        /// on his approach so the same situation plays out again. Never a fail.
        /// </summary>
        void RetrySituation(string message)
        {
            if (stepAdvancing) return;
            situationResetAt = Time.time;
            armCycleSeen = false;
            // A fresh situation includes fresh repair progress — otherwise it
            // accumulates across retries and permanently trips the safety net.
            foreach (var obj in refs.objectives)
                if (obj != null) obj.ResetProgress();
            if (!string.IsNullOrEmpty(message)) lm.ShowToast(message, 2.6f, warning: true);
            var e = lm.Engineer;
            if (e != null && !e.IsEscaped) e.TutorialReset(spawn);
        }

        /// <summary>The lesson objective can never actually be finished.</summary>
        public void OnObjectiveRepaired() => RetrySituation(null);

        // ================= per-frame =================

        float pauseGraceUntil;

        void Update()
        {
            if (lm == null) return;
            // Grace after unpausing so one Enter can't close the pause menu AND
            // confirm the completion menu in the same frame.
            if (lm.IsPaused) { pauseGraceUntil = Time.unscaledTime + 0.2f; return; }
            if (Time.unscaledTime < pauseGraceUntil) return;

            // Safety net: the engineer must never finish the training repair.
            if (refs.objectives.Count > 0 && refs.objectives[0].Progress01 > 0.3f)
                OnObjectiveRepaired();

            if (donePanel != null && donePanel.activeSelf)
            {
                HandleDoneMenu();
                return;
            }

            // Step 2: an arm cycle that ended without a stun was a miss — bring the
            // engineer back so he walks into the range again.
            if (step == 2 && arm != null && !stepAdvancing)
            {
                if (arm.State == MachineState.Active) armCycleSeen = true;
                else if (armCycleSeen) RetrySituation(Loc.TutorialRetry);
            }

            // Watchdog for every step: no completion for a while (engineer wandered
            // off, stood aside, got the situation into a dead end) — repeat it.
            // Never teleport mid-strike: a landing hit must be allowed to connect.
            if (step >= 1 && step <= 4 && !stepAdvancing &&
                !(arm != null && arm.State == MachineState.Active) &&
                Time.time - situationResetAt > StepRetrySeconds)
            {
                RetrySituation(null);
            }
        }

        void ShowDonePanel()
        {
            lm.TutorialOverlayLock = true;
            if (targetRing != null) Destroy(targetRing);
            SetGate(m => false);
            doneIndex = 0;
            RefreshDoneMenu();
            donePanel.SetActive(true);
            Audio.AudioManager.Play("room_won", Audio.SfxBus.UI, 0.5f);
        }

        void HandleDoneMenu()
        {
            if (GameInput.UpPressed || GameInput.DownPressed)
            {
                doneIndex = 1 - doneIndex;
                Audio.UiSfx.PauseMove();
                RefreshDoneMenu();
            }
            if (GameInput.ConfirmPressed)
            {
                Audio.UiSfx.Confirm();
                if (doneIndex == 0)
                {
                    // «НАЧАТЬ СМЕНУ»: back to Boot, straight into the normal briefing.
                    GameManager.TutorialRequested = false;
                    GameManager.ResumeAtBriefing = true;
                    SceneLoader.Load(GameManager.BootScene);
                }
                else
                {
                    // «ПОВТОРИТЬ УРОК»: reload this scene with the tutorial flag kept.
                    SceneLoader.Reload();
                }
            }
        }

        void RefreshDoneMenu()
        {
            for (int i = 0; i < doneMenu.Count; i++)
            {
                bool sel = i == doneIndex;
                string label = i == 0 ? Loc.TutorialStartShift : Loc.TutorialRepeat;
                doneMenu[i].text = (sel ? "> " : "") + label + (sel ? " <" : "");
                doneMenu[i].color = sel ? new Color(0.95f, 1f, 0.7f) : new Color(0.55f, 0.75f, 0.6f);
                doneMenu[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
                doneMenuBgs[i].color = sel ? new Color(0.2f, 0.45f, 0.25f, 0.35f) : new Color(0f, 0f, 0f, 0f);
            }
        }
    }
}
