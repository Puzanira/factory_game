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
        public float CooldownDuration => cooldown;
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

        void BuildHighlight()
        {
            float d = Mathf.Max(BodySize.x, BodySize.y) + 0.55f;
            var holder = new GameObject("Highlight");
            holder.transform.SetParent(transform, false);
            Viz.Make("Ring", holder.transform, PlaceholderShape.Ring,
                new Color(1f, 0.85f, 0.4f, 0.95f), Vector2.zero, new Vector2(d, d), 12, unlit: true);
            var glow = FxFactory.Glow(holder.transform, new Color(1f, 0.8f, 0.35f, 0.18f),
                new Vector2(d * 1.5f, d * 1.5f), 11);
            glow.name = "SelectionGlow";
            var pulse = holder.AddComponent<SelectionPulse>();
            pulse.target = holder.transform;
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
            Run();
            return true;
        }

        /// <summary>Escalation override: fires even while cooling down (never while active).</summary>
        public void ForceActivate()
        {
            if (State == MachineState.Active) return;
            if (cooldownRoutine != null) { StopCoroutine(cooldownRoutine); cooldownRoutine = null; }
            Run();
        }

        void Run()
        {
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
            CooldownRemaining = cooldown;
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
            float d = Mathf.Max(BodySize.x, BodySize.y) + 0.3f;
            var sr = Viz.Make("Flash", transform, PlaceholderShape.Ring,
                new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(d, d), 11);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = t / 0.35f;
                sr.transform.localScale = Vector3.one * (d * (1f + k * 0.7f));
                sr.color = new Color(1f, 1f, 1f, 0.9f * (1f - k));
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        protected static void AddPressure(float amount, string reason)
        {
            var lm = Core.LevelManager.Instance;
            if (lm != null && lm.Pressure != null) lm.Pressure.Add(amount, reason);
        }
    }
}
