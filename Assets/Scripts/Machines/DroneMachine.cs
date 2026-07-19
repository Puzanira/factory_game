using System.Collections;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// Inventory drone: flies to the engineer, marks him, shadows him for a while
    /// (stress + slight slow + slow Resolve drain), then returns to its dock.
    /// No combat — pure psychological pressure.
    /// </summary>
    public class DroneMachine : InteractableMachine
    {
        float flySpeed = 4.5f;
        float followDuration = 6f;
        Vector2 dock;
        Transform drone;
        Transform rotorA;
        Transform rotorB;
        GameObject scanCone;
        LineRenderer previewLine;

        public override string ActivationMessage => displayName.ToUpper() + ": ДРОН ВЫЛЕТЕЛ";

        public override string PurposeLine => Loc.PurposeDrone;
        public override string PurposeHint => Loc.PurposeDroneHint;

        /// <summary>The drone chases the engineer itself — launching it is never wasted.</summary>
        public override bool ActivationAlwaysEffective => true;

        string LoopId => "drone_" + GetInstanceID();

        protected override void OnConfigure(MachineSpec s)
        {
            flySpeed = s.moveSpeed > 0f ? s.moveSpeed : 4.5f;
            followDuration = s.alarmDuration > 0f ? s.alarmDuration : 6f;
            dock = s.pos;

            // Dock pad with hazard corner.
            Viz.Make("Dock", transform, PlaceholderShape.Square,
                new Color(0.28f, 0.36f, 0.48f), Vector2.zero, new Vector2(0.8f, 0.8f), 4);
            Viz.Make("DockPad", transform, PlaceholderShape.Circle,
                new Color(0.2f, 0.26f, 0.36f), Vector2.zero, new Vector2(0.55f, 0.55f), 5);

            var droneGO = new GameObject("Drone");
            droneGO.transform.SetParent(transform, false);
            FxFactory.Shadow(droneGO.transform, new Vector2(0.5f, 0.32f), 0.3f, 8);
            var rA = Viz.Make("RotorA", droneGO.transform, PlaceholderShape.Square,
                new Color(0.75f, 0.85f, 0.95f, 0.8f), Vector2.zero, new Vector2(0.75f, 0.07f), 9);
            var rB = Viz.Make("RotorB", droneGO.transform, PlaceholderShape.Square,
                new Color(0.75f, 0.85f, 0.95f, 0.8f), Vector2.zero, new Vector2(0.07f, 0.75f), 9);
            rotorA = rA.transform;
            rotorB = rB.transform;
            Viz.Make("Hull", droneGO.transform, PlaceholderShape.Circle,
                new Color(0.35f, 0.62f, 0.9f), Vector2.zero, new Vector2(0.4f, 0.4f), 10);
            Viz.Make("Eye", droneGO.transform, PlaceholderShape.Circle,
                new Color(0.95f, 0.2f, 0.2f), new Vector2(0f, -0.08f), new Vector2(0.13f, 0.13f), 11);

            // Scan cone shown while shadowing the engineer.
            scanCone = new GameObject("ScanCone");
            scanCone.transform.SetParent(droneGO.transform, false);
            var cone = Viz.Make("Cone", scanCone.transform, PlaceholderShape.Arrow,
                new Color(0.4f, 0.85f, 1f, 0.25f), new Vector2(0f, -0.55f), new Vector2(1.1f, 0.9f), 8, unlit: true);
            cone.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // point down
            var coneGlow = FxFactory.Glow(scanCone.transform, new Color(0.4f, 0.85f, 1f, 0.15f),
                new Vector2(1.2f, 1.2f), 7);
            coneGlow.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            scanCone.SetActive(false);

            drone = droneGO.transform;
        }

        void RotorSpin()
        {
            if (rotorA == null) return;
            float a = Time.time * 720f;
            rotorA.localRotation = Quaternion.Euler(0f, 0f, a);
            rotorB.localRotation = Quaternion.Euler(0f, 0f, a + 90f);
        }

        protected override void BuildPreview(Transform root)
        {
            previewLine = Viz.MakeLine("TargetLine", root, new Color(0.4f, 0.75f, 1f, 0.8f), 0.06f, 9);
            previewLine.positionCount = 2;
        }

        void Update()
        {
            RotorSpin();
            // Keep the preview line pointing at the live target region.
            if (previewLine != null && previewLine.gameObject.activeInHierarchy && Engineer != null)
            {
                previewLine.SetPosition(0, drone.position);
                previewLine.SetPosition(1, new Vector3(Engineer.Pos.x, Engineer.Pos.y, 0f));
            }
        }

        protected override IEnumerator OnActivate()
        {
            var e = Engineer;
            if (e == null) yield break;

            AudioManager.LoopOn(LoopId, "drone_loop", SfxBus.Machines, 0.45f, 0.5f);

            // Fly out.
            float timeout = 4f;
            while (e != null && !e.IsEscaped && Vector2.Distance(drone.position, e.Pos) > 0.7f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                drone.position = Vector2.MoveTowards(drone.position, e.Pos + Vector2.up * 0.5f, flySpeed * Time.deltaTime);
                yield return null;
            }

            // Mark and shadow.
            if (e != null && !e.IsEscaped)
            {
                e.SetMarked(true);
                if (scanCone != null) scanCone.SetActive(true);
                AudioManager.PlayAt("drone_alert", e.Pos, SfxBus.Machines, 0.5f);
                AddPressure(5f, displayName);
                float t = 0f;
                float nextPingAt = 0f;
                float dronePerSec = e.Data != null ? e.Data.lossDronePerSec : 1.4f;
                while (t < followDuration && e != null && !e.IsEscaped)
                {
                    t += Time.deltaTime;
                    if (t >= nextPingAt)
                    {
                        nextPingAt = t + 2.4f;
                        AudioManager.PlayAt("drone_scan", drone.position, SfxBus.Machines, 0.4f);
                    }
                    drone.position = Vector2.MoveTowards(drone.position, e.Pos + Vector2.up * 0.6f, flySpeed * Time.deltaTime);
                    e.AddStress(6f * Time.deltaTime);
                    e.ApplySlow(0.85f, 0.2f);
                    e.DrainResolve(dronePerSec, Time.deltaTime, displayName);
                    yield return null;
                }
                e.SetMarked(false);
                if (scanCone != null) scanCone.SetActive(false);
            }

            // Return to dock.
            while (Vector2.Distance(drone.position, dock) > 0.1f)
            {
                drone.position = Vector2.MoveTowards(drone.position, dock, flySpeed * Time.deltaTime);
                yield return null;
            }

            AudioManager.LoopOff(LoopId, 0.8f);
        }
    }
}
