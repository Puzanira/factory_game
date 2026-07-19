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
        // Soft restart: he slipped past the door — let him visibly repair ~2 s
        // before the trap phase resets (no abrupt teleport).
        float step3RepairTimer;

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
        Canvas tutorialCanvas;

        // Pointer plaques («указательные плашки»): callouts anchored to the thing
        // they explain, removed the moment the required action is performed.
        readonly Dictionary<string, GameObject> plaques = new Dictionary<string, GameObject>();

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
            tutorialCanvas = canvas;

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

        // ================= pointer plaques =================

        void HidePlaque(string id)
        {
            if (plaques.TryGetValue(id, out var go))
            {
                if (go != null) Destroy(go);
                plaques.Remove(id);
            }
        }

        void ClearPlaques()
        {
            foreach (var kv in plaques)
                if (kv.Value != null) Destroy(kv.Value);
            plaques.Clear();
        }

        static readonly Color BubbleFill = new Color(0.97f, 0.96f, 0.89f, 0.97f);
        static readonly Color BubbleLine = new Color(0.14f, 0.3f, 0.2f, 0.95f);
        static readonly Color BubbleText = new Color(0.1f, 0.24f, 0.16f);

        /// <summary>Comic speech bubble floating in the room, tail of shrinking puffs toward the target.</summary>
        void ShowWorldPlaque(string id, Vector2 target, Vector2 offset, string text)
        {
            HidePlaque(id);
            var root = new GameObject("Plaque_" + id);
            Vector2 center = target + offset;
            root.transform.position = new Vector3(center.x, center.y, 0f);

            Vector2 size = new Vector2(5.4f, 2.0f);

            // Plain oval: dark outline behind, cream fill on top.
            MakePuff(root.transform, Vector2.zero, size, 43, 44);

            // Speech-bubble tail: a solid wedge whose tip points exactly at the
            // explained object (its base merges into the oval fill).
            Vector2 toTarget = target - center;
            Vector2 dir = toTarget.normalized;
            float edge = Mathf.Lerp(size.y, size.x, Mathf.Abs(dir.x)) * 0.5f;
            float tipLen = Mathf.Clamp(toTarget.magnitude - edge - 0.2f, 0.5f, 2.4f);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 wedgePos = dir * (edge - 0.2f + tipLen * 0.5f);
            var wedgeLine = Viz.Make("TailLine", root.transform, PlaceholderShape.Arrow, BubbleLine,
                wedgePos, new Vector2(tipLen + 0.22f, 0.95f), 43, unlit: true);
            wedgeLine.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            var wedgeFill = Viz.Make("TailFill", root.transform, PlaceholderShape.Arrow, BubbleFill,
                wedgePos, new Vector2(tipLen, 0.78f), 44, unlit: true);
            wedgeFill.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            if (Viz.HasSortingLayer("WorldUI"))
            {
                Viz.SetLayer(wedgeLine, "WorldUI");
                Viz.SetLayer(wedgeFill, "WorldUI");
            }

            // Text on top of the cloud.
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(root.transform, false);
            canvasGO.transform.localScale = Vector3.one * 0.015f;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 46;
            if (Viz.HasSortingLayer("WorldUI")) canvas.sortingLayerName = "WorldUI";
            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(310f, 105f);
            UIBuilder.Label(canvasGO.transform, "Text", text, 18, BubbleText, TextAnchor.MiddleCenter);

            plaques[id] = root;
        }

        /// <summary>One cloud puff: outline circle behind + fill circle in front (world sprites).</summary>
        void MakePuff(Transform root, Vector2 pos, Vector2 size, int lineOrder, int fillOrder)
        {
            var line = Viz.Make("PuffLine", root, PlaceholderShape.Circle, BubbleLine,
                pos, size + new Vector2(0.16f, 0.16f), lineOrder, unlit: true);
            var fill = Viz.Make("PuffFill", root, PlaceholderShape.Circle, BubbleFill,
                pos, size, fillOrder, unlit: true);
            if (Viz.HasSortingLayer("WorldUI"))
            {
                Viz.SetLayer(line, "WorldUI");
                Viz.SetLayer(fill, "WorldUI");
            }
        }

        /// <summary>
        /// Comic speech bubble on the screen UI. targetAnchor is the screen-anchor
        /// point of the explained element — the wedge tail aims exactly at it.
        /// </summary>
        void ShowScreenPlaque(string id, Vector2 aMin, Vector2 aMax, string text, Vector2 targetAnchor)
        {
            HidePlaque(id);
            RectTransform rt = UIBuilder.Panel(tutorialCanvas.transform, "Plaque_" + id,
                aMin, aMax, new Color(0f, 0f, 0f, 0f));

            // Geometry in reference pixels (CanvasScaler 1920x1080).
            Vector2 centerA = (aMin + aMax) * 0.5f;
            Vector2 toTargetPx = new Vector2((targetAnchor.x - centerA.x) * 1920f,
                                             (targetAnchor.y - centerA.y) * 1080f);
            Vector2 dir = toTargetPx.sqrMagnitude > 1f ? toTargetPx.normalized : Vector2.left;
            float halfW = (aMax.x - aMin.x) * 1920f * 0.5f;
            float halfH = (aMax.y - aMin.y) * 1080f * 0.5f;
            float edge = Mathf.Min(
                Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);
            float tipLen = Mathf.Clamp(toTargetPx.magnitude - edge - 8f, 44f, 96f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 wedgePos = dir * (edge - 8f + tipLen * 0.5f);

            // Plain opaque oval (UI hints must not let the game show through).
            // Two passes so the dark outlines sit behind the cream fills.
            for (int layer = 0; layer < 2; layer++)
            {
                ScreenPuff(rt, layer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
                ScreenWedge(rt, layer, wedgePos, tipLen, angle);
            }

            var label = UIBuilder.Label(rt, "Text", text, 17, BubbleText, TextAnchor.MiddleCenter);
            SetRect(label.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));

            plaques[id] = rt.gameObject;
        }

        /// <summary>Speech tail wedge for a screen bubble (arrow sprite aimed at the target).</summary>
        void ScreenWedge(RectTransform parent, int layer, Vector2 posPx, float tipLen, float angle)
        {
            var go = new GameObject(layer == 0 ? "TailLine" : "TailFill");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = posPx;
            rt.sizeDelta = layer == 0
                ? new Vector2(tipLen + 12f, tipLen * 0.62f + 12f)
                : new Vector2(tipLen, tipLen * 0.62f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteFactory.Get(PlaceholderShape.Arrow);
            Color c = layer == 0 ? BubbleLine : BubbleFill;
            img.color = new Color(c.r, c.g, c.b, 1f);
            img.raycastTarget = false;
        }

        /// <summary>UI cloud puff, one layer at a time: 0 = dark outline, 1 = cream fill.</summary>
        void ScreenPuff(RectTransform parent, int layer, Vector2 anchor, Vector2 offsetPx, Vector2 sizePx, bool stretch = false)
        {
            var go = new GameObject(layer == 0 ? "PuffLine" : "PuffFill");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                float grow = layer == 0 ? 5f : 0f;
                rt.offsetMin = new Vector2(-grow, -grow);
                rt.offsetMax = new Vector2(grow, grow);
            }
            else
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.anchoredPosition = offsetPx;
                float grow = layer == 0 ? 8f : 0f;
                rt.sizeDelta = sizePx + new Vector2(grow, grow);
            }
            var img = go.AddComponent<Image>();
            // Crisp circle sprite (not the soft glow): the oval must be solid.
            img.sprite = SpriteFactory.Get(PlaceholderShape.Circle);
            // Fully opaque on screen: interface hints must cover what is behind them.
            Color c = layer == 0 ? BubbleLine : BubbleFill;
            img.color = new Color(c.r, c.g, c.b, 1f);
            img.raycastTarget = false;
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

            ClearPlaques();

            switch (step)
            {
                case 1:
                    // Any equipment counts on this step — the plaques do the guiding.
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep1Header, Loc.TutorialStep1Body);
                    SetGate(null);
                    Highlight(null);
                    if (refs.objectives.Count > 0)
                        ShowWorldPlaque("panel", refs.objectives[0].Pos, new Vector2(3.4f, 0.3f), Loc.TutPlaquePanel);
                    // Hugs the terminal edge; tail aims at the command list rows.
                    ShowScreenPlaque("terminal", new Vector2(0.285f, 0.44f), new Vector2(0.55f, 0.61f),
                        Loc.TutPlaqueTerminal, new Vector2(0.14f, 0.52f));
                    break;

                case 2:
                    // The only route now leads through the arm radius: door open.
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep2Header, Loc.TutorialStep2Body);
                    SetGate(m => m == arm);
                    Highlight(arm);
                    if (arm != null)
                        ShowWorldPlaque("arm", arm.transform.position, new Vector2(3.4f, 0.6f), Loc.TutPlaqueArm);
                    break;

                case 3:
                    if (door != null && door.IsClosed) door.ForceActivate();
                    SetCard(Loc.TutorialStep3Header, Loc.TutorialStep3Body + "\n" + Loc.TutorialStep3Hint);
                    SetGate(m => m == door || m == conveyor);
                    Highlight(door);
                    if (door != null)
                        ShowWorldPlaque("door", door.transform.position, new Vector2(3.4f, -1.6f), Loc.TutPlaqueDoor);
                    break;

                case 4:
                    SetCard(Loc.TutorialStep4Header, Loc.TutorialStep4Body);
                    SetGate(null);
                    Highlight(null);
                    // Hugs the terminal edge; tail aims at the three power cells.
                    ShowScreenPlaque("resource", new Vector2(0.285f, 0.79f), new Vector2(0.53f, 0.92f),
                        Loc.TutPlaqueResource, new Vector2(0.23f, 0.87f));
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
                    // Any activated system completes the intro step; its plaques
                    // vanish the moment the action is performed.
                    ClearPlaques();
                    Advance(Loc.TutorialStep1Done);
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
                    {
                        doorClosedAt = Time.time;
                        // Door done — the pointer moves on to the conveyor.
                        HidePlaque("door");
                        if (conveyor != null)
                            ShowWorldPlaque("conveyor", conveyor.transform.position,
                                new Vector2(0f, -2.0f), Loc.TutPlaqueConveyor);
                    }
                    if (machine == conveyor)
                    {
                        if (doorClosedAt > 0f)
                        {
                            conveyorActivatedAt = Time.time;
                            HidePlaque("conveyor");
                            // Reversing the belt while the engineer is ON it is the
                            // whole lesson — count it immediately, no second flip.
                            if (effective) Advance(Loc.ComboRedirect, 2.2f);
                        }
                        else ResetStep3();
                    }
                    break;

                case 4:
                    if (effective)
                    {
                        HidePlaque("resource");
                        Advance(Loc.ResourceRestored);
                    }
                    break;
            }
        }

        void OnStunned(string source)
        {
            // Step 2 completes on a real arm hit («ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» and
            // «РЕШИМОСТЬ −10» arrive through the shared feedback path).
            if (step == 2 && !stepAdvancing)
            {
                HidePlaque("arm");
                // Point at the resolve gauge: the stun just visibly drained it.
                // Cleared when step 3 begins (~3 s on screen).
                // Right under the top HUD; tail aims at the resolve bar itself.
                ShowScreenPlaque("resolve", new Vector2(0.60f, 0.775f), new Vector2(0.94f, 0.88f),
                    Loc.TutPlaqueResolve, new Vector2(0.76f, 0.945f));
                Advance(null, 3.2f);
            }
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
            RestartTrapPhase();
        }

        /// <summary>He repaired for a couple of seconds after slipping past the door.</summary>
        void SoftResetStep3()
        {
            lm.ShowToast(Loc.TutorialRetry, 2.6f, warning: true);
            RestartTrapPhase();
        }

        /// <summary>Step 3 back to phase one: door open, pointer on the door, fresh approach.</summary>
        void RestartTrapPhase()
        {
            doorClosedAt = -999f;
            conveyorActivatedAt = -999f;
            step3RepairTimer = 0f;
            if (door != null && door.IsClosed) door.ForceActivate(); // retry needs an open route
            HidePlaque("conveyor");
            if (door != null)
                ShowWorldPlaque("door", door.transform.position, new Vector2(3.4f, -1.6f), Loc.TutPlaqueDoor);
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

            // Step 3 soft restart: he got past the door. He is allowed to reach the
            // panel and repair for ~2 seconds (the player sees the missed moment),
            // then the trap phase starts over.
            if (step == 3 && !stepAdvancing)
            {
                var e3 = lm.Engineer;
                bool repairing = e3 != null && e3.Fsm != null &&
                    e3.Fsm.CurrentId == LastShift.Engineer.EngineerStateId.RepairObjective;
                if (repairing)
                {
                    step3RepairTimer += Time.deltaTime;
                    if (step3RepairTimer >= 2f)
                    {
                        step3RepairTimer = 0f;
                        SoftResetStep3();
                    }
                }
                else step3RepairTimer = 0f;
            }

            // Watchdog for the other steps: no completion for a while (engineer
            // wandered off, got the situation into a dead end) — repeat it.
            // Step 3 uses the soft repair-based restart above instead.
            // Never teleport mid-strike: a landing hit must be allowed to connect.
            if (step >= 1 && step <= 4 && step != 3 && !stepAdvancing &&
                !(arm != null && arm.State == MachineState.Active) &&
                Time.time - situationResetAt > StepRetrySeconds)
            {
                RetrySituation(null);
            }
        }

        void ShowDonePanel()
        {
            lm.TutorialOverlayLock = true;
            ClearPlaques();
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
