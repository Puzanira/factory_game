using System;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// Presents one tutorial step at a time: dim everything, highlight exactly one
    /// target, show one opaque terminal callout, and wait for Submit before moving on.
    /// Informational steps freeze the room (the player reads without pressure);
    /// practical steps hand the room back and only keep the marker and a status line.
    /// Input comes from the shared GameInput funnel — no tutorial-only input system.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class TutorialStepController : MonoBehaviour
    {
        enum Mode { Hidden, Info, Practical }

        Core.LevelManager lm;
        Canvas canvas;
        TutorialFocusOverlay overlay;
        TutorialHighlightTarget highlight;
        TutorialCalloutPanel callout;
        TutorialInputLock inputLock;

        Mode mode = Mode.Hidden;
        bool dimmed;
        Func<Rect> targetProvider;
        TutorialHighlightShape shape = TutorialHighlightShape.Rect;
        Color highlightColor = TutorialHighlightTarget.Amber;
        Action onAcknowledged;

        /// <summary>True while an informational step owns the clock.</summary>
        public bool FreezesTime => mode == Mode.Info;
        /// <summary>True while factory systems must ignore Submit.</summary>
        public bool BlocksGameInput => mode == Mode.Info || (inputLock != null && inputLock.Locked);
        public bool WaitingForAck => mode == Mode.Info;

        /// <summary>Set while another lesson panel (the completion card) owns input.</summary>
        public bool ExternalBlock { get; set; }

        public void Init(Core.LevelManager levelManager, Canvas tutorialCanvas)
        {
            lm = levelManager;
            canvas = tutorialCanvas;

            overlay = new GameObject("TutorialFocusOverlay").AddComponent<TutorialFocusOverlay>();
            overlay.Init(canvas);
            highlight = new GameObject("TutorialHighlightTarget").AddComponent<TutorialHighlightTarget>();
            highlight.Init(canvas);
            callout = new GameObject("TutorialCalloutPanel").AddComponent<TutorialCalloutPanel>();
            callout.Init(canvas);
            inputLock = gameObject.AddComponent<TutorialInputLock>();
        }

        // ---------------- presentation ----------------

        /// <summary>
        /// Informational step: dim the room, mark one target, wait for Enter/Submit.
        /// </summary>
        public void ShowInfo(int step, int total, string header, string body,
                             Func<Rect> target, TutorialHighlightShape targetShape, Action onAck,
                             TutorialCalloutSide side = TutorialCalloutSide.Auto)
        {
            mode = Mode.Info;
            targetProvider = target;
            shape = targetShape;
            highlightColor = TutorialHighlightTarget.Amber;
            onAcknowledged = onAck;

            bool hasTarget = target != null;
            Rect rect = hasTarget ? target() : new Rect();
            if (hasTarget) overlay.Focus(rect); else overlay.FocusNone();
            if (hasTarget) highlight.Show(rect, shape, highlightColor); else highlight.Hide();
            callout.Show(header, body, Loc.FooterNext, StepLabel(step, total), rect, hasTarget, side);
            AudioManager.Play("intro_page", SfxBus.UI, 0.5f);
        }

        /// <summary>
        /// Practical step: the room runs again, the marker stays on the target and the
        /// callout keeps the instruction plus a live status line.
        ///
        /// <paramref name="footer"/> must name the control to press. The old shared
        /// footer said «ВЫПОЛНИТЕ ДЕЙСТВИЕ», which is exactly no help to the one
        /// person who needs it — someone who does not yet know what to press.
        /// </summary>
        /// <param name="dimAround">
        /// Darken everything except the target. Used by the first step, where the
        /// whole screen is new and the eye has to be sent to one place; the dimming
        /// is dropped the moment the player has done the thing (see <see cref="Undim"/>).
        /// </param>
        public void ShowPractical(int step, int total, string header, string body, string footer,
                                  Func<Rect> target, TutorialHighlightShape targetShape,
                                  TutorialCalloutSide side = TutorialCalloutSide.Auto,
                                  bool dimAround = false)
        {
            mode = Mode.Practical;
            targetProvider = target;
            shape = targetShape;
            highlightColor = TutorialHighlightTarget.Amber;
            onAcknowledged = null;

            bool hasTarget = target != null;
            Rect rect = hasTarget ? target() : new Rect();
            // Gameplay stays fully readable while the player acts — unless the step
            // asked to point the eye at one thing first.
            dimmed = dimAround && hasTarget;
            if (dimmed) overlay.Focus(rect); else overlay.Hide();
            if (hasTarget) highlight.Show(rect, shape, highlightColor); else highlight.Hide();
            callout.Show(header, body, footer, StepLabel(step, total), rect, hasTarget, side);
        }

        /// <summary>Lift a practical step's dimming — the room is the point again.</summary>
        public void Undim() { dimmed = false; overlay.Hide(); }

        public void SetStatus(string text, bool warning = false) => callout.SetStatus(text, warning);

        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
            highlight.SetColor(color);
        }

        /// <summary>Swap the marked target without changing the step text.</summary>
        public void SetTarget(Func<Rect> target, TutorialHighlightShape targetShape)
        {
            targetProvider = target;
            shape = targetShape;
            if (target == null) { highlight.Hide(); return; }
            Rect rect = target();
            highlight.Show(rect, shape, highlightColor);
            if (mode == Mode.Info || dimmed) overlay.Focus(rect);
            callout.Follow(rect, true);
        }

        public void HideAll()
        {
            mode = Mode.Hidden;
            dimmed = false;
            targetProvider = null;
            onAcknowledged = null;
            overlay.Hide();
            highlight.Hide();
            callout.Hide();
        }

        /// <summary>
        /// Frame-grabber hook: recompute highlight and callout in the canvas space
        /// that is current right now (see TutorialCalloutPanel.Replace).
        /// </summary>
        public void DevRelayout()
        {
            if (mode == Mode.Hidden || targetProvider == null) return;
            Rect rect = targetProvider();
            highlight.Show(rect, shape, highlightColor);
            if (mode == Mode.Info || dimmed) overlay.Focus(rect);
            callout.Replace(rect, true);
        }

        /// <summary>Layout introspection for the headless checks: the marked target.</summary>
        public bool DevHasTarget => targetProvider != null && mode != Mode.Hidden;
        public Rect DevTargetRect => targetProvider != null ? targetProvider() : new Rect();
        public Rect DevCalloutRect => callout != null ? callout.ScreenRect : new Rect();
        public bool DevCalloutVisible => callout != null && callout.Visible;

        /// <summary>Blocks input for a moment so one Enter cannot do two things.</summary>
        public void LockInput(float seconds = TutorialInputLock.DefaultSeconds) => inputLock.Lock(seconds);

        static string StepLabel(int step, int total) =>
            step > 0 ? string.Format(Loc.TutorialStepLabel, step, total) : "";

        // ---------------- per-frame ----------------

        /// <summary>
        /// Runs at execution order -60, i.e. before the command terminal polls input,
        /// so the lock this step needs is already in place when the terminal looks.
        /// </summary>
        void ApplyLocks()
        {
            if (lm == null) return;
            bool freeze = mode == Mode.Info && !lm.IsPaused;
            lm.TutorialTimeFreeze = freeze;
            if (freeze) Time.timeScale = 0f;
            lm.TutorialOverlayLock = BlocksGameInput || ExternalBlock;
        }

        void Update()
        {
            ApplyLocks();
            if (lm != null && lm.IsPaused) return;

            // Keep the marker on a moving target (the engineer walks while marked).
            if (targetProvider != null && mode != Mode.Hidden)
            {
                Rect rect = targetProvider();
                highlight.Show(rect, shape, highlightColor);
                if (mode == Mode.Info) overlay.Focus(rect);
                callout.Follow(rect, true);
            }

            if (mode != Mode.Info) return;
            if (inputLock.Locked) return;
            if (!GameInput.ConfirmPressed) return;

            // Acknowledged: lock input so this Enter cannot reach the terminal, then
            // hand control back to the lesson flow.
            var callback = onAcknowledged;
            onAcknowledged = null;
            mode = Mode.Hidden;
            inputLock.Lock();
            UiSfx.Confirm();
            highlight.SetColor(TutorialHighlightTarget.Green);
            callout.Hide();
            overlay.Hide();
            highlight.Hide();
            callback?.Invoke();
        }
    }
}
