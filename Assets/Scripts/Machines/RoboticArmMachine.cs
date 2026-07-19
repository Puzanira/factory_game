using System.Collections;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// A grabber arm (radius range) or an industrial press (rect range, pressMode).
    /// Telegraphs with a wind-up, then stuns the engineer if he is still inside the
    /// zone. Registers danger on the grid during the cycle so the AI reacts to it.
    /// Visuals: jointed arm with gripper / striped press frame with slamming plate.
    /// </summary>
    public class RoboticArmMachine : InteractableMachine
    {
        float radius = 2f;
        bool pressMode;
        float windup = 0.9f;
        float stunDuration = 1.6f;
        Transform armPivot;
        Transform pressPlate;

        protected override Vector2 BodySize =>
            pressMode && spec != null ? spec.size : new Vector2(1f, 1f);

        public override string ActivationMessage => pressMode
            ? displayName.ToUpper() + ": ЦИКЛ ЗАПУЩЕН"
            : displayName.ToUpper() + ": ЗАХВАТ АКТИВИРОВАН";

        public override string PurposeLine => Loc.PurposeArm;
        public override string PurposeHint => Loc.PurposeArmHint;

        /// <summary>Slightly wider than the strike zone: he can still walk in during wind-up.</summary>
        public override bool EngineerInEffectiveZone
        {
            get
            {
                var e = Engineer;
                if (e == null || e.IsEscaped) return false;
                if (pressMode)
                {
                    Rect r = Viz.RectAt(spec.pos, spec.size + new Vector2(1.2f, 1.2f));
                    return r.Contains(e.Pos);
                }
                return Vector2.Distance(transform.position, e.Pos) <= radius + 0.6f;
            }
        }

        protected override string SelectionSfxName => "servo_soft";
        protected override string ActivateSfxName => "arm_windup"; // hydraulic charge telegraph
        protected override float ActivateSfxVolume => 0.55f;

        protected override void OnConfigure(MachineSpec s)
        {
            radius = s.radius;
            pressMode = s.pressMode;
            windup = s.windup;
            stunDuration = s.stunDuration;

            if (pressMode)
            {
                FxFactory.Shadow(transform, s.size * 1.15f, 0.35f, 3);
                Viz.MakeTiled("FrameStripes", transform, TextureFactory.HazardStripes(),
                    new Color(1f, 1f, 1f, 0.9f), Vector2.zero, s.size + new Vector2(0.35f, 0.35f), 4);
                Viz.Make("Bed", transform, PlaceholderShape.Square, new Color(0.22f, 0.24f, 0.27f),
                    Vector2.zero, s.size, 5);
                Viz.Make("BedPlate", transform, PlaceholderShape.Square, new Color(0.3f, 0.32f, 0.36f),
                    Vector2.zero, s.size * 0.8f, 5);
                var plate = Viz.Make("Plate", transform, PlaceholderShape.Square, new Color(0.9f, 0.55f, 0.16f),
                    Vector2.zero, s.size * 0.35f, 6);
                Viz.Make("PlateBolt", plate.transform, PlaceholderShape.Circle, new Color(0.5f, 0.32f, 0.1f),
                    Vector2.zero, new Vector2(0.25f, 0.25f), 7);
                pressPlate = plate.transform;
            }
            else
            {
                FxFactory.Shadow(transform, new Vector2(1.1f, 0.8f), 0.35f, 3);
                Viz.Make("BasePlate", transform, PlaceholderShape.Circle, new Color(0.4f, 0.4f, 0.44f),
                    Vector2.zero, new Vector2(1.0f, 1.0f), 4);
                Viz.Make("Base", transform, PlaceholderShape.Circle, new Color(0.75f, 0.6f, 0.2f),
                    Vector2.zero, new Vector2(0.7f, 0.7f), 5);

                // Jointed arm: pivot -> segment A -> joint -> segment B -> gripper.
                var pivot = new GameObject("ArmPivot");
                pivot.transform.SetParent(transform, false);
                armPivot = pivot.transform;
                Viz.Make("SegA", armPivot, PlaceholderShape.Square, new Color(0.92f, 0.74f, 0.26f),
                    new Vector2(radius * 0.22f, 0f), new Vector2(radius * 0.44f, 0.22f), 6);
                Viz.Make("Joint", armPivot, PlaceholderShape.Circle, new Color(0.55f, 0.45f, 0.16f),
                    new Vector2(radius * 0.45f, 0f), new Vector2(0.24f, 0.24f), 7);
                Viz.Make("SegB", armPivot, PlaceholderShape.Square, new Color(0.88f, 0.7f, 0.24f),
                    new Vector2(radius * 0.65f, 0.06f), new Vector2(radius * 0.4f, 0.16f), 6);
                Viz.Make("GripperL", armPivot, PlaceholderShape.Square, new Color(0.4f, 0.4f, 0.44f),
                    new Vector2(radius * 0.88f, 0.1f), new Vector2(0.2f, 0.08f), 7);
                Viz.Make("GripperR", armPivot, PlaceholderShape.Square, new Color(0.4f, 0.4f, 0.44f),
                    new Vector2(radius * 0.88f, -0.02f), new Vector2(0.2f, 0.08f), 7);
            }
        }

        protected override void BuildPreview(Transform root)
        {
            if (pressMode)
            {
                Viz.Make("PressZone", root, PlaceholderShape.Square,
                    new Color(1f, 0.45f, 0.25f, 0.22f), Vector2.zero, spec.size, 11, unlit: true);
                Viz.Make("PressEdge", root, PlaceholderShape.Ring,
                    new Color(1f, 0.6f, 0.3f, 0.7f), Vector2.zero,
                    new Vector2(spec.size.x * 1.3f, spec.size.y * 1.3f), 11, unlit: true);
            }
            else
            {
                Viz.Make("Range", root, PlaceholderShape.Ring,
                    new Color(1f, 0.7f, 0.25f, 0.8f), Vector2.zero, new Vector2(radius * 2f, radius * 2f), 11, unlit: true);
                var fill = FxFactory.Glow(root, new Color(1f, 0.7f, 0.25f, 0.10f),
                    new Vector2(radius * 2.4f, radius * 2.4f), 10);
                fill.name = "RangeFill";
            }
        }

        bool EngineerInRange()
        {
            var e = Engineer;
            if (e == null) return false;
            if (pressMode) return Viz.RectAt(spec.pos, spec.size).Contains(e.Pos);
            return Vector2.Distance(transform.position, e.Pos) <= radius;
        }

        protected override IEnumerator OnActivate()
        {
            Rect dangerArea = pressMode
                ? Viz.RectAt(spec.pos, spec.size)
                : Viz.RectAt(spec.pos, new Vector2(radius * 2f, radius * 2f));
            var grid = PathGrid.Instance;
            if (grid != null) grid.AddDanger(dangerArea, 2f);

            // Telegraph.
            var warn = Viz.Make("Warning", transform,
                pressMode ? PlaceholderShape.Square : PlaceholderShape.Ring,
                new Color(1f, 0.2f, 0.15f, 0.4f), Vector2.zero,
                pressMode ? spec.size : new Vector2(radius * 2f, radius * 2f), 11, unlit: true);
            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                float pulse = 0.22f + 0.3f * Mathf.PingPong(t * 6f, 1f);
                warn.color = new Color(1f, 0.2f, 0.15f, pulse);
                if (armPivot != null)
                    armPivot.localRotation = Quaternion.Euler(0f, 0f, t / windup * 360f);
                if (pressPlate != null)
                {
                    // Press plate rises during wind-up (anticipation).
                    float rise = 1f + (t / windup) * 0.5f;
                    pressPlate.localScale = new Vector3(spec.size.x * 0.35f * rise, spec.size.y * 0.35f * rise, 1f);
                }
                yield return null;
            }

            // Strike.
            warn.color = new Color(1f, 0.1f, 0.05f, 0.65f);
            AudioManager.PlayAt(pressMode ? "press_slam" : "arm_strike",
                transform.position, SfxBus.Machines, pressMode ? 0.75f : 0.65f);
            if (pressPlate != null) pressPlate.localScale = new Vector3(spec.size.x * 0.95f, spec.size.y * 0.95f, 1f);
            float strike = 0f;
            bool hit = false;
            while (strike < 0.4f)
            {
                strike += Time.deltaTime;
                if (!hit && EngineerInRange())
                {
                    hit = true;
                    AudioManager.PlayAt("arm_hit", Engineer.Pos, SfxBus.Machines, 0.65f);
                    Engineer.StunHit(stunDuration, displayName);
                    ReportEffective(); // he walked in after all — upgrade a wasted verdict
                }
                yield return null;
            }

            if (pressPlate != null) pressPlate.localScale = new Vector3(spec.size.x * 0.35f, spec.size.y * 0.35f, 1f);
            if (armPivot != null) armPivot.localRotation = Quaternion.identity;
            Destroy(warn.gameObject);
            if (grid != null) grid.AddDanger(dangerArea, -2f);
        }
    }
}
