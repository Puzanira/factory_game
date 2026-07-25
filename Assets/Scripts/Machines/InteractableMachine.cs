using System.Collections;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    public enum MachineState { Ready, Active, Cooldown }

    /// <summary>
    /// Base class for every terminal-controlled machine: Ready/Active/Cooldown state,
    /// activation flow, selection highlight and effect-range preview.
    /// </summary>
    public abstract class InteractableMachine : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Machine";
        public string commandVerb = "Activate";
        [TextArea] public string description = "";

        [Header("Config")]
        [Tooltip("Optional defaults; MachineSpec values override when provided.")]
        public MachineData defaultData;
        public float cooldown = 8f;
        public bool escalationAuto;

        [Header("Audio (optional placeholder hook)")]
        public AudioClip activateClip;

        public MachineState State { get; protected set; } = MachineState.Ready;
        public float CooldownRemaining { get; private set; }
        public float CooldownDuration => currentCooldownDuration > 0f ? currentCooldownDuration : cooldown;
        float currentCooldownDuration;
        public bool IsReady => State == MachineState.Ready;

        /// <summary>
        /// Global cooldown recovery multiplier. The terminal raises it when the player
        /// has too few ready commands, so the game never stalls on full cooldown.
        /// </summary>
        public static float GlobalCooldownScale = 1f;

        /// <summary>Short Russian status line shown as a toast when this machine fires.</summary>
        public virtual string ActivationMessage => displayName.ToUpper() + ": АКТИВИРОВАНО";

        /// <summary>Raised on every successful activation.</summary>
        public event System.Action<InteractableMachine> Activated;

        /// <summary>
        /// Player-activation verdict: true = the engineer was inside the effective
        /// zone (or walked into the effect while it ran), false = wasted activation.
        /// Not raised for escalation auto-fire.
        /// </summary>
        public event System.Action<InteractableMachine, bool> Judged;

        // ---------------- tactical info ----------------

        /// <summary>Main Russian purpose line shown for the selected command.</summary>
        public virtual string PurposeLine => "";
        /// <summary>Optional second tactical hint line.</summary>
        public virtual string PurposeHint => "";

        /// <summary>True while the engineer is where this machine can meaningfully affect him.</summary>
        public virtual bool EngineerInEffectiveZone => true;

        /// <summary>Recovery-style activations (e.g. re-opening a door) are never judged wasted.</summary>
        public virtual bool ActivationAlwaysEffective => false;

        /// <summary>
        /// World-space footprint of this machine's effective zone. Subclasses report
        /// their real shape (belt strip, doorway, arm work area) so the tutorial can
        /// frame the actual zone instead of a generic circle around everything.
        /// </summary>
        public virtual Rect EffectiveZoneRect =>
            Viz.RectAt(transform.position, BodySize + new Vector2(0.6f, 0.6f));

        /// <summary>Control charges one player activation costs.</summary>
        public virtual int ResourceCost => 1;

        float pendingCooldownMult = 1f;
        bool judgedThisRun;
        bool lastJudgedEffective;

        protected MachineSpec spec;
        protected GameObject highlight;
        protected GameObject preview;
        AudioSource audioSource;
        Coroutine activationRoutine;
        Coroutine cooldownRoutine;

        public virtual string CommandLabel => displayName + " — " + commandVerb;
        public virtual string Description => description;
        protected virtual Vector2 BodySize => spec != null ? spec.size : Vector2.one;

        protected static Engineer.EngineerController Engineer => LastShift.Engineer.EngineerController.Instance;

        public void Configure(MachineSpec s)
        {
            spec = s;
            transform.position = new Vector3(s.pos.x, s.pos.y, 0f);
            if (!string.IsNullOrEmpty(s.displayName)) displayName = s.displayName;
            else if (defaultData != null) displayName = defaultData.displayName;
            if (!string.IsNullOrEmpty(s.commandVerb)) commandVerb = s.commandVerb;
            else if (defaultData != null) commandVerb = defaultData.commandVerb;
            if (!string.IsNullOrEmpty(s.description)) description = s.description;
            else if (defaultData != null) description = defaultData.description;
            cooldown = s.cooldown > 0f ? s.cooldown : (defaultData != null ? defaultData.cooldown : 8f);
            escalationAuto = s.escalationAuto;

            OnConfigure(s);
            BuildHighlight();
        }

        protected abstract void OnConfigure(MachineSpec s);

        /// <summary>Build the effect-range / route preview shown while selected.</summary>
        protected abstract void BuildPreview(Transform root);

        /// <summary>
        /// Selection marker: a thin amber rectangle matching the machine's real
        /// footprint (a conveyor gets a long belt-shaped frame, not a circle around
        /// it), with terminal corner brackets and a soft glow.
        /// </summary>
        void BuildHighlight()
        {
            Vector2 size = BodySize + new Vector2(0.55f, 0.55f);
            var holder = new GameObject("Highlight");
            holder.transform.SetParent(transform, false);

            var amber = new Color(1f, 0.85f, 0.4f, 0.95f);
            float t = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.06f, 0.05f, 0.1f);
            Viz.Make("EdgeT", holder.transform, PlaceholderShape.Square, amber,
                new Vector2(0f, size.y * 0.5f), new Vector2(size.x, t), 12, unlit: true);
            Viz.Make("EdgeB", holder.transform, PlaceholderShape.Square, amber,
                new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, t), 12, unlit: true);
            Viz.Make("EdgeL", holder.transform, PlaceholderShape.Square, amber,
                new Vector2(-size.x * 0.5f, 0f), new Vector2(t, size.y), 12, unlit: true);
            Viz.Make("EdgeR", holder.transform, PlaceholderShape.Square, amber,
                new Vector2(size.x * 0.5f, 0f), new Vector2(t, size.y), 12, unlit: true);

            // Corner brackets: thicker stubs so the frame reads as a technical marker.
            float armLen = Mathf.Min(Mathf.Min(size.x, size.y) * 0.45f, 0.55f);
            float bt = t * 1.8f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? -1f : 1f;
                Vector2 corner = new Vector2(sx * size.x * 0.5f, sy * size.y * 0.5f);
                Viz.Make("Corner" + i + "H", holder.transform, PlaceholderShape.Square, amber,
                    corner - new Vector2(sx * armLen * 0.5f, 0f), new Vector2(armLen, bt), 13, unlit: true);
                Viz.Make("Corner" + i + "V", holder.transform, PlaceholderShape.Square, amber,
                    corner - new Vector2(0f, sy * armLen * 0.5f), new Vector2(bt, armLen), 13, unlit: true);
            }

            var glow = FxFactory.Glow(holder.transform, new Color(1f, 0.8f, 0.35f, 0.16f),
                new Vector2(size.x * 1.15f, size.y * 1.6f), 11);
            glow.name = "SelectionGlow";
            var pulse = holder.AddComponent<SelectionPulse>();
            pulse.target = holder.transform;
            pulse.amount = Mathf.Max(size.x, size.y) > 4f ? 0.02f : 0.05f; // stays subtle on long belts
            highlight = holder;
            highlight.SetActive(false);
        }

        /// <summary>Optional quiet sound when the terminal highlights this machine.</summary>
        protected virtual string SelectionSfxName => null;

        /// <summary>Generic activation sound; subclasses override or layer their own in OnActivate.</summary>
        protected virtual string ActivateSfxName => "relay_click";
        protected virtual float ActivateSfxVolume => 0.6f;

        public void SetSelected(bool selected)
        {
            if (selected && highlight != null && !highlight.activeSelf && SelectionSfxName != null)
                AudioManager.PlayAt(SelectionSfxName, transform.position, SfxBus.Machines, 0.4f);
            if (highlight != null) highlight.SetActive(selected);
            if (selected && preview == null)
            {
                preview = new GameObject("Preview");
                preview.transform.SetParent(transform, false);
                BuildPreview(preview.transform);
            }
            if (preview != null) preview.SetActive(selected);
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            Run(judge: true);
            return true;
        }

        /// <summary>Escalation override: fires even while cooling down (never while active).</summary>
        public void ForceActivate()
        {
            if (State == MachineState.Active) return;
            if (cooldownRoutine != null) { StopCoroutine(cooldownRoutine); cooldownRoutine = null; }
            Run(judge: false);
        }

        /// <summary>
        /// Called by subclasses when the running effect actually connected with the
        /// engineer (arm hit, vehicle ram). Upgrades a wasted verdict to effective.
        /// </summary>
        protected void ReportEffective()
        {
            if (!judgedThisRun || lastJudgedEffective) return;
            lastJudgedEffective = true;
            pendingCooldownMult = Data.TacticsData.Get().effectiveCooldownMultiplier;
            Judged?.Invoke(this, true);
            Core.FactoryControlResource.NotifyEffectiveAction();
        }

        void Run(bool judge)
        {
            var tactics = Data.TacticsData.Get();
            judgedThisRun = judge;
            if (judge)
            {
                lastJudgedEffective = ActivationAlwaysEffective || EngineerInEffectiveZone;
                pendingCooldownMult = lastJudgedEffective
                    ? tactics.effectiveCooldownMultiplier
                    : tactics.wastedCooldownMultiplier;
                Judged?.Invoke(this, lastJudgedEffective);
                // Timing bonus only for machines that actually have a zone to hit —
                // support systems (drone, room alarm, door re-open) earn nothing
                // for merely being pressed.
                if (lastJudgedEffective && !ActivationAlwaysEffective)
                    Core.FactoryControlResource.NotifyEffectiveAction();
            }
            else
            {
                pendingCooldownMult = 1f;
            }
            State = MachineState.Active;
            if (activateClip != null)
            {
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.PlayOneShot(activateClip);
            }
            else if (ActivateSfxName != null)
            {
                AudioManager.PlayAt(ActivateSfxName, transform.position, SfxBus.Machines, ActivateSfxVolume);
            }
            StartCoroutine(FlashRoutine());
            Activated?.Invoke(this);
            activationRoutine = StartCoroutine(RunActivation());
        }

        IEnumerator RunActivation()
        {
            yield return OnActivate();
            activationRoutine = null;
            cooldownRoutine = StartCoroutine(CooldownRoutine());
        }

        /// <summary>The machine's actual behaviour; State stays Active until this finishes.</summary>
        protected abstract IEnumerator OnActivate();

        IEnumerator CooldownRoutine()
        {
            State = MachineState.Cooldown;
            // Wasted activations cool down longer; effective ones slightly faster.
            currentCooldownDuration = cooldown * Mathf.Max(0.1f, pendingCooldownMult);
            pendingCooldownMult = 1f;
            CooldownRemaining = currentCooldownDuration;
            UiSfx.CommandCooldown();
            while (CooldownRemaining > 0f)
            {
                CooldownRemaining -= Time.deltaTime * Mathf.Max(1f, GlobalCooldownScale);
                yield return null;
            }
            CooldownRemaining = 0f;
            State = MachineState.Ready;
            cooldownRoutine = null;
            var lm = Core.LevelManager.Instance;
            if (lm == null || !lm.RoomEnded) UiSfx.CommandReady();
        }

        IEnumerator FlashRoutine()
        {
            // Rectangular flash frame: same shape language as the selection marker,
            // so a long conveyor never flashes as a huge circle.
            Vector2 size = BodySize + new Vector2(0.3f, 0.3f);
            var holder = new GameObject("Flash");
            holder.transform.SetParent(transform, false);
            var white = new Color(1f, 1f, 1f, 0.9f);
            float thick = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.07f, 0.05f, 0.12f);
            var edges = new[]
            {
                Viz.Make("FlashT", holder.transform, PlaceholderShape.Square, white,
                    new Vector2(0f, size.y * 0.5f), new Vector2(size.x, thick), 11, unlit: true),
                Viz.Make("FlashB", holder.transform, PlaceholderShape.Square, white,
                    new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, thick), 11, unlit: true),
                Viz.Make("FlashL", holder.transform, PlaceholderShape.Square, white,
                    new Vector2(-size.x * 0.5f, 0f), new Vector2(thick, size.y), 11, unlit: true),
                Viz.Make("FlashR", holder.transform, PlaceholderShape.Square, white,
                    new Vector2(size.x * 0.5f, 0f), new Vector2(thick, size.y), 11, unlit: true),
            };
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = t / 0.35f;
                holder.transform.localScale = Vector3.one * (1f + k * 0.25f);
                var c = new Color(1f, 1f, 1f, 0.9f * (1f - k));
                for (int i = 0; i < edges.Length; i++)
                    if (edges[i] != null) edges[i].color = c;
                yield return null;
            }
            Destroy(holder);
        }

        protected static void AddPressure(float amount, string reason)
        {
            var lm = Core.LevelManager.Instance;
            if (lm != null && lm.Pressure != null) lm.Pressure.Add(amount, reason);
        }
    }
}
