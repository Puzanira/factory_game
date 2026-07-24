using UnityEngine;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Objectives
{
    /// <summary>
    /// A repair point. The engineer stands near it to raise Progress over time; the
    /// factory can damage it (escalation) which also costs the engineer Resolve.
    /// Visual: electrical panel with cables, blinking status LED, amber point light,
    /// repair sparks. States: dormant / current (amber glow) / repaired (green).
    /// </summary>
    public class RepairObjective : ObjectiveNode
    {
        public float repairTime = 20f;

        public float Progress01 { get; private set; }

        /// <summary>True while the engineer is actively working on this console.</summary>
        public bool IsBeingRepaired => !Completed && Time.time - lastRepairTick < 0.35f;

        SpriteRenderer body;
        SpriteRenderer screen;
        SpriteRenderer statusLed;
        SpriteRenderer glow;
        SpriteRenderer progressBar;
        SpriteRenderer currentRing;
        ParticleSystem sparks;
        float lastRepairTick = -99f;
        bool isCurrent;

        public void Setup(ObjectiveSpec spec)
        {
            objectiveName = spec.name;
            repairTime = Mathf.Max(1f, spec.repairTime);
            transform.position = new Vector3(spec.pos.x, spec.pos.y, 0f);
            name = "Objective_" + spec.name.Replace(' ', '_');

            FxFactory.Shadow(transform, new Vector2(1.05f, 0.7f), 0.4f, 3);

            // Cables snaking from the panel to the floor.
            var cable = Viz.MakeLine("Cable", transform, new Color(0.16f, 0.16f, 0.18f), 0.06f, 4);
            cable.useWorldSpace = false;
            cable.positionCount = 4;
            cable.SetPosition(0, new Vector3(-0.3f, -0.4f, 0f));
            cable.SetPosition(1, new Vector3(-0.55f, -0.62f, 0f));
            cable.SetPosition(2, new Vector3(-0.35f, -0.8f, 0f));
            cable.SetPosition(3, new Vector3(0.2f, -0.92f, 0f));
            var cable2 = Viz.MakeLine("Cable2", transform, new Color(0.5f, 0.35f, 0.15f), 0.045f, 4);
            cable2.useWorldSpace = false;
            cable2.positionCount = 3;
            cable2.SetPosition(0, new Vector3(0.3f, -0.4f, 0f));
            cable2.SetPosition(1, new Vector3(0.55f, -0.7f, 0f));
            cable2.SetPosition(2, new Vector3(0.75f, -0.85f, 0f));

            // Panel body: casing, inner face, screen, vents, status LED.
            // Red while broken (reads as "нужен ремонт" against the yellow machinery),
            // turns green when the engineer finishes the repair.
            body = MakeBody(new Color(0.78f, 0.26f, 0.2f), new Vector2(0.95f, 0.95f));
            Viz.Make("Face", transform, PlaceholderShape.Square, new Color(0.24f, 0.23f, 0.2f),
                Vector2.zero, new Vector2(0.78f, 0.78f), 5);
            screen = Viz.Make("Screen", transform, PlaceholderShape.Square, new Color(0.12f, 0.2f, 0.1f),
                new Vector2(0f, 0.16f), new Vector2(0.55f, 0.32f), 6);
            Viz.Make("VentA", transform, PlaceholderShape.Square, new Color(0.14f, 0.14f, 0.13f),
                new Vector2(-0.15f, -0.18f), new Vector2(0.32f, 0.05f), 6);
            Viz.Make("VentB", transform, PlaceholderShape.Square, new Color(0.14f, 0.14f, 0.13f),
                new Vector2(-0.15f, -0.28f), new Vector2(0.32f, 0.05f), 6);
            statusLed = Viz.Make("StatusLed", transform, PlaceholderShape.Circle, new Color(0.95f, 0.3f, 0.2f),
                new Vector2(0.26f, -0.23f), new Vector2(0.1f, 0.1f), 7);

            glow = FxFactory.Glow(transform, new Color(1f, 0.45f, 0.3f, 0.18f), new Vector2(2.2f, 2.2f), 2);
            FxFactory.PointLight(transform, Vector2.zero, new Color(1f, 0.8f, 0.45f), 2.6f, 0.7f);

            // Progress bar under the panel.
            Viz.Make("BarBack", transform, PlaceholderShape.Square, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0f, -0.68f), new Vector2(0.98f, 0.15f), 6);
            progressBar = Viz.Make("BarFill", transform, PlaceholderShape.Square, new Color(0.4f, 0.95f, 0.5f),
                new Vector2(-0.475f, -0.68f), new Vector2(0.01f, 0.11f), 7);

            currentRing = Viz.Make("CurrentRing", transform, PlaceholderShape.Ring,
                new Color(1f, 0.85f, 0.4f, 0.9f), Vector2.zero, new Vector2(1.6f, 1.6f), 8, unlit: true);
            currentRing.gameObject.AddComponent<SelectionPulse>().target = currentRing.transform;
            currentRing.gameObject.SetActive(false);

            sparks = FxFactory.Sparks(transform);

            // Local «РЕМОНТ» readout above the console — repair progress is shown
            // where the work happens, never as a global bar at the top of the screen.
            LastShift.UI.LocalRepairProgressUI.Attach(this, 1.05f);
        }

        public void SetIsCurrent(bool current)
        {
            isCurrent = current;
            if (currentRing != null) currentRing.gameObject.SetActive(current && !Completed);
        }

        public void TickRepair(float dt)
        {
            if (Completed) return;
            Progress01 = Mathf.Clamp01(Progress01 + dt / repairTime);
            lastRepairTick = Time.time;
            UpdateBar();
            if (Progress01 >= 1f)
            {
                LastShift.Audio.AudioManager.PlayAt("repair_done", transform.position,
                    LastShift.Audio.SfxBus.Engineer, 0.7f);
                if (body != null) body.color = new Color(0.35f, 0.75f, 0.4f);
                if (screen != null) screen.color = new Color(0.2f, 0.5f, 0.25f);
                if (statusLed != null) statusLed.color = new Color(0.35f, 0.95f, 0.45f);
                if (glow != null) glow.color = new Color(0.4f, 0.95f, 0.5f, 0.14f);
                SetIsCurrent(false);
                RaiseCompleted();
            }
        }

        /// <summary>Tutorial helper: silently wipe accumulated progress (no resolve/pressure side effects).</summary>
        public void ResetProgress()
        {
            if (Completed) return;
            Progress01 = 0f;
            UpdateBar();
        }

        /// <summary>Factory sabotage: destroy part of the progress. Costs the engineer Resolve.</summary>
        public void Damage(float fraction, string reason)
        {
            if (Completed || Progress01 <= 0f) return;
            Progress01 = Mathf.Clamp01(Progress01 - fraction);
            UpdateBar();
            if (sparks != null) sparks.Emit(18);
            LastShift.Audio.AudioManager.PlayAt("spark_crackle", transform.position,
                LastShift.Audio.SfxBus.Hazards, 0.65f);
            var e = LastShift.Engineer.EngineerController.Instance;
            if (e != null)
            {
                e.Stats.LoseResolve(e.Data != null ? e.Data.lossObjectiveDamaged : 12f, reason);
                e.AddStress(15f);
            }
            var lm = Core.LevelManager.Instance;
            if (lm != null && lm.Pressure != null) lm.Pressure.Add(6f, reason);
        }

        void UpdateBar()
        {
            if (progressBar == null) return;
            float w = Mathf.Max(0.01f, 0.98f * Progress01);
            progressBar.transform.localScale = new Vector3(w, 0.11f, 1f);
            progressBar.transform.localPosition = new Vector3(-0.49f + w * 0.5f, -0.68f, 0f);
        }

        void Update()
        {
            if (Completed) return;

            // Blinking screen + LED; sparks while actively being repaired.
            bool repairing = Time.time - lastRepairTick < 0.3f;
            float pulse = Mathf.PingPong(Time.time * (repairing ? 6f : 2f), 1f);
            if (screen != null)
                screen.color = repairing
                    ? new Color(0.25f + 0.2f * pulse, 0.45f + 0.2f * pulse, 0.2f)
                    : new Color(0.12f + 0.06f * pulse, 0.2f + 0.08f * pulse, 0.1f);
            if (statusLed != null)
                statusLed.color = new Color(0.95f, 0.3f, 0.2f, 0.5f + 0.5f * pulse);
            if (glow != null && isCurrent)
                glow.color = new Color(1f, 0.45f, 0.3f, 0.14f + 0.1f * pulse);

            if (sparks != null)
            {
                var emission = sparks.emission;
                emission.enabled = repairing;
                if (repairing && !sparks.isPlaying) sparks.Play();
            }
        }
    }
}
