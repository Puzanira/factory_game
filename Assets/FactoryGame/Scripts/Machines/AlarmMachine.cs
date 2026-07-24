using System.Collections;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// Emergency alarm (room-wide stress) or scanner gate (useGateRect: only affects
    /// the engineer while he is inside the scan band). Drives the HUD red vignette
    /// via the static ActiveCount.
    /// </summary>
    public class AlarmMachine : InteractableMachine
    {
        public static int ActiveCount { get; private set; }

        float duration = 6f;
        bool useGate;
        Rect gateRect;
        SpriteRenderer beacon;
        SpriteRenderer beaconGlow;
        SpriteRenderer scanBand;

        public override string ActivationMessage => useGate
            ? displayName.ToUpper() + ": СКАНИРОВАНИЕ ПРОХОДА"
            : displayName.ToUpper() + ": ТРЕВОГА ВКЛЮЧЕНА";

        public override string PurposeLine => useGate ? Loc.PurposeScanner : Loc.PurposeAlarm;
        public override string PurposeHint => useGate ? Loc.PurposeScannerHint : Loc.PurposeAlarmHint;

        /// <summary>Room-wide alarm always lands; a scanner gate needs the engineer near the band.</summary>
        public override bool EngineerInEffectiveZone
        {
            get
            {
                if (!useGate) return true;
                var e = Engineer;
                if (e == null || e.IsEscaped) return false;
                Rect r = gateRect;
                r.xMin -= 1.2f; r.xMax += 1.2f; r.yMin -= 1.2f; r.yMax += 1.2f;
                return r.Contains(e.Pos);
            }
        }

        public override bool ActivationAlwaysEffective => !useGate;

        protected override void OnConfigure(MachineSpec s)
        {
            duration = s.alarmDuration > 0f ? s.alarmDuration : 6f;
            useGate = s.useGateRect;
            gateRect = s.gateRect;

            // Striped mount + housing + lamp under a soft glow.
            Viz.MakeTiled("Mount", transform, TextureFactory.HazardStripes(),
                new Color(1f, 1f, 1f, 0.85f), Vector2.zero, new Vector2(0.85f, 0.85f), 4);
            Viz.Make("Housing", transform, PlaceholderShape.Square,
                new Color(0.33f, 0.34f, 0.37f), Vector2.zero, new Vector2(0.55f, 0.55f), 5);
            beacon = Viz.Make("Beacon", transform, PlaceholderShape.Circle,
                new Color(0.6f, 0.1f, 0.1f), Vector2.zero, new Vector2(0.32f, 0.32f), 6);
            beaconGlow = FxFactory.Glow(transform, new Color(1f, 0.2f, 0.12f, 0f),
                new Vector2(1.6f, 1.6f), 5);

            if (useGate)
            {
                Vector2 center = gateRect.center - (Vector2)transform.position;
                scanBand = Viz.Make("ScanBand", transform, PlaceholderShape.Square,
                    new Color(0.3f, 0.8f, 1f, 0.12f), center, gateRect.size, 3, unlit: true);
                Viz.Make("GatePostA", transform, PlaceholderShape.Square, new Color(0.5f, 0.55f, 0.6f),
                    center + new Vector2(0f, gateRect.height * 0.5f), new Vector2(0.34f, 0.34f), 6);
                Viz.Make("GatePostB", transform, PlaceholderShape.Square, new Color(0.5f, 0.55f, 0.6f),
                    center - new Vector2(0f, gateRect.height * 0.5f), new Vector2(0.34f, 0.34f), 6);
                Viz.Make("GateLampA", transform, PlaceholderShape.Circle, new Color(0.4f, 0.9f, 1f),
                    center + new Vector2(0f, gateRect.height * 0.5f), new Vector2(0.12f, 0.12f), 7);
                Viz.Make("GateLampB", transform, PlaceholderShape.Circle, new Color(0.4f, 0.9f, 1f),
                    center - new Vector2(0f, gateRect.height * 0.5f), new Vector2(0.12f, 0.12f), 7);
            }
        }

        protected override void BuildPreview(Transform root)
        {
            if (useGate)
            {
                Vector2 center = gateRect.center - (Vector2)transform.position;
                Viz.Make("ScanZone", root, PlaceholderShape.Square,
                    new Color(0.3f, 0.8f, 1f, 0.3f), center, gateRect.size, 9);
            }
            else
            {
                Viz.Make("RoomWide", root, PlaceholderShape.Ring,
                    new Color(1f, 0.3f, 0.2f, 0.7f), Vector2.zero, new Vector2(2.2f, 2.2f), 9);
            }
        }

        // Shared loop ids keyed by mode: simultaneous alarms retarget the same loop
        // instead of stacking a second siren.
        static int alarmLoopCount;
        static int scannerLoopCount;

        protected override IEnumerator OnActivate()
        {
            ActiveCount++;
            if (useGate)
            {
                scannerLoopCount++;
                AudioManager.LoopOn("alarm_scanner", "scanner_loop", SfxBus.Alerts, 0.5f, 0.3f);
            }
            else
            {
                alarmLoopCount++;
                AudioManager.LoopOn("alarm_siren", "alarm_loop", SfxBus.Alerts, 0.55f, 0.35f);
            }
            AddPressure(3f, displayName);
            bool scannedBurst = false;
            float t = 0f;
            var e = Engineer;
            float alarmPerSec = e != null && e.Data != null ? e.Data.lossAlarmPerSec : 1.1f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.PingPong(t * 4f, 1f);
                if (beacon != null) beacon.color = new Color(0.6f + 0.4f * pulse, 0.1f, 0.08f);
                if (beaconGlow != null) beaconGlow.color = new Color(1f, 0.2f, 0.12f, 0.45f * pulse);
                if (scanBand != null) scanBand.color = new Color(0.3f, 0.8f, 1f, 0.08f + 0.2f * pulse);

                e = Engineer;
                if (e != null && !e.IsEscaped)
                {
                    if (useGate)
                    {
                        if (gateRect.Contains(e.Pos))
                        {
                            if (!scannedBurst)
                            {
                                scannedBurst = true;
                                AudioManager.PlayAt("drone_alert", e.Pos, SfxBus.Alerts, 0.5f);
                                e.AddStress(20f);
                                e.ApplySlow(0.7f, 2f);
                                AddPressure(5f, "scanned");
                            }
                            // Scanning a repairing engineer is the gate's anti-repair
                            // role — same focus-breaking multiplier as the siren.
                            float gateStress = 6f;
                            if (e.Fsm != null && e.Fsm.CurrentId == LastShift.Engineer.EngineerStateId.RepairObjective)
                                gateStress *= TacticsData.Get().alarmRepairStressMultiplier;
                            e.AddStress(gateStress * Time.deltaTime);
                            e.DrainResolve(alarmPerSec, Time.deltaTime, displayName);
                        }
                    }
                    else
                    {
                        // The siren is the room-wide anti-repair tool: while he is
                        // focused on a panel, the noise stresses him far faster,
                        // driving him toward panic (which makes him leave the panel).
                        float stressRate = 4f;
                        if (e.Fsm != null && e.Fsm.CurrentId == LastShift.Engineer.EngineerStateId.RepairObjective)
                            stressRate *= TacticsData.Get().alarmRepairStressMultiplier;
                        e.AddStress(stressRate * Time.deltaTime);
                        e.DrainResolve(alarmPerSec, Time.deltaTime, displayName);
                    }
                }
                yield return null;
            }

            if (beacon != null) beacon.color = new Color(0.6f, 0.1f, 0.1f);
            if (beaconGlow != null) beaconGlow.color = new Color(1f, 0.2f, 0.12f, 0f);
            if (scanBand != null) scanBand.color = new Color(0.3f, 0.8f, 1f, 0.12f);
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            ReleaseLoop();
        }

        void ReleaseLoop()
        {
            if (useGate)
            {
                scannerLoopCount = Mathf.Max(0, scannerLoopCount - 1);
                if (scannerLoopCount == 0) AudioManager.LoopOff("alarm_scanner", 0.9f);
            }
            else
            {
                alarmLoopCount = Mathf.Max(0, alarmLoopCount - 1);
                if (alarmLoopCount == 0) AudioManager.LoopOff("alarm_siren", 0.9f);
            }
        }

        void OnDestroy()
        {
            // Scene unload while active: keep the static counters honest.
            if (State == MachineState.Active)
            {
                ActiveCount = Mathf.Max(0, ActiveCount - 1);
                ReleaseLoop();
            }
        }
    }
}
