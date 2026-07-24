using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// Forklift / pallet mover / autonomous stacker. Drives a waypoint route (out and
    /// back), blocks grid cells as it moves, rams the engineer on contact, and can
    /// optionally shift pallet obstacles between two positions (stacker behaviour).
    /// </summary>
    public class MobileUnitMachine : InteractableMachine
    {
        Vector2[] route;
        float moveSpeed = 3f;
        float stunDuration = 1.6f;
        Transform unit;
        GridBlocker unitBlocker;
        SpriteRenderer beacon;

        public override string ActivationMessage =>
            (spec != null && spec.palletShifts != null && spec.palletShifts.Length > 0)
                ? displayName.ToUpper() + ": ПЕРЕСТАНОВКА ПАЛЕТ"
                : displayName.ToUpper() + ": МАРШРУТ ЗАПУЩЕН";

        public override string PurposeLine => Loc.PurposeMobileUnit;
        public override string PurposeHint => Loc.PurposeMobileUnitHint;

        /// <summary>Effective when the engineer is near the drive corridor.</summary>
        public override bool EngineerInEffectiveZone
        {
            get
            {
                var e = Engineer;
                if (e == null || e.IsEscaped || route == null) return false;
                for (int i = 0; i < route.Length - 1; i++)
                {
                    Vector2 a = route[i], b = route[i + 1];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(e.Pos - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                    if (Vector2.Distance(e.Pos, a + ab * t) <= 2.0f) return true;
                }
                // Pallet shifts change the room topology — that is useful on its own.
                return pallets.Count > 0;
            }
        }

        protected override string ActivateSfxName => "forklift_start";
        string LoopId => "forklift_" + GetEntityId();
        float nextWarnBeepAt;

        class Pallet
        {
            public Transform tf;
            public GridBlocker blocker;
            public PalletShiftSpec shift;
            public bool atB;
        }
        readonly List<Pallet> pallets = new List<Pallet>();

        protected override void OnConfigure(MachineSpec s)
        {
            route = (s.route != null && s.route.Length >= 2)
                ? s.route
                : new[] { s.pos, s.pos + Vector2.up * 2f };
            moveSpeed = s.moveSpeed;
            stunDuration = s.stunDuration;

            var unitGO = new GameObject("Unit");
            unitGO.transform.SetParent(transform, false);
            unitGO.transform.position = new Vector3(route[0].x, route[0].y, 0f);

            // Industrial forklift silhouette: shadow, wheels, chassis, cab, forks, beacon.
            FxFactory.Shadow(unitGO.transform, s.size * 1.1f, 0.4f, 4);
            float wx = s.size.x * 0.42f, wy = s.size.y * 0.32f;
            Viz.Make("WheelFL", unitGO.transform, PlaceholderShape.Circle, new Color(0.12f, 0.12f, 0.13f), new Vector2(-wx, wy), new Vector2(0.24f, 0.24f), 5);
            Viz.Make("WheelFR", unitGO.transform, PlaceholderShape.Circle, new Color(0.12f, 0.12f, 0.13f), new Vector2(wx, wy), new Vector2(0.24f, 0.24f), 5);
            Viz.Make("WheelBL", unitGO.transform, PlaceholderShape.Circle, new Color(0.12f, 0.12f, 0.13f), new Vector2(-wx, -wy), new Vector2(0.24f, 0.24f), 5);
            Viz.Make("WheelBR", unitGO.transform, PlaceholderShape.Circle, new Color(0.12f, 0.12f, 0.13f), new Vector2(wx, -wy), new Vector2(0.24f, 0.24f), 5);
            Viz.Make("Chassis", unitGO.transform, PlaceholderShape.Square,
                new Color(0.93f, 0.74f, 0.14f), Vector2.zero, s.size, 6);
            Viz.Make("ChassisStripe", unitGO.transform, PlaceholderShape.Square,
                new Color(0.2f, 0.2f, 0.2f), new Vector2(0f, -s.size.y * 0.28f), new Vector2(s.size.x * 0.9f, 0.1f), 7);
            Viz.Make("Cab", unitGO.transform, PlaceholderShape.Square,
                new Color(0.35f, 0.37f, 0.4f), new Vector2(0f, -s.size.y * 0.12f), new Vector2(s.size.x * 0.62f, s.size.y * 0.4f), 7);
            Viz.Make("ForkL", unitGO.transform, PlaceholderShape.Square,
                new Color(0.62f, 0.65f, 0.7f), new Vector2(-s.size.x * 0.2f, s.size.y * 0.58f), new Vector2(0.1f, s.size.y * 0.35f), 6);
            Viz.Make("ForkR", unitGO.transform, PlaceholderShape.Square,
                new Color(0.62f, 0.65f, 0.7f), new Vector2(s.size.x * 0.2f, s.size.y * 0.58f), new Vector2(0.1f, s.size.y * 0.35f), 6);
            beacon = FxFactory.Glow(unitGO.transform, new Color(1f, 0.6f, 0.15f, 0.15f),
                new Vector2(0.7f, 0.7f), 8);
            beacon.transform.localPosition = new Vector2(0f, -s.size.y * 0.05f);

            unit = unitGO.transform;
            unitBlocker = unitGO.AddComponent<GridBlocker>();
            unitBlocker.size = s.size;
            unitBlocker.SetBlocked(true); // parked vehicles block too

            if (s.palletShifts != null)
            {
                foreach (var shift in s.palletShifts)
                {
                    var pGO = new GameObject("ShiftPallet");
                    pGO.transform.SetParent(transform, false);
                    pGO.transform.position = new Vector3(shift.posA.x, shift.posA.y, 0f);
                    Viz.Make("Wood", pGO.transform, PlaceholderShape.Square,
                        new Color(0.54f, 0.42f, 0.25f), Vector2.zero, shift.size, 5);
                    Viz.Make("Strap", pGO.transform, PlaceholderShape.Square,
                        new Color(0.3f, 0.24f, 0.14f), Vector2.zero, new Vector2(shift.size.x, 0.12f), 6);
                    var blocker = pGO.AddComponent<GridBlocker>();
                    blocker.size = shift.size;
                    blocker.SetBlocked(true);
                    pallets.Add(new Pallet { tf = pGO.transform, blocker = blocker, shift = shift, atB = false });
                }
            }
        }

        protected override void BuildPreview(Transform root)
        {
            var line = Viz.MakeLine("Route", root, new Color(0.4f, 1f, 0.7f, 0.55f), 0.07f, 11);
            line.positionCount = route.Length;
            for (int i = 0; i < route.Length; i++)
                line.SetPosition(i, new Vector3(route[i].x, route[i].y, 0f));

            // Projected floor arrows along the route.
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 a = route[i], b = route[i + 1];
                Vector2 dir = (b - a).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float dist = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.FloorToInt(dist / 1.6f));
                for (int k = 0; k < steps; k++)
                {
                    Vector2 p = a + dir * ((k + 0.5f) / steps) * dist;
                    var arrow = Viz.Make("RouteArrow", root, PlaceholderShape.Arrow,
                        new Color(0.4f, 1f, 0.7f, 0.75f), root.InverseTransformPoint(p),
                        new Vector2(0.5f, 0.36f), 11, unlit: true);
                    arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            foreach (var p in pallets)
            {
                Vector2 target = p.atB ? p.shift.posA : p.shift.posB;
                Viz.Make("PalletGhost", root, PlaceholderShape.Square,
                    new Color(0.9f, 0.75f, 0.4f, 0.3f), root.InverseTransformPoint(target), p.shift.size, 11, unlit: true);
            }
        }

        protected override IEnumerator OnActivate()
        {
            AudioManager.LoopOn(LoopId, "forklift_loop", SfxBus.Machines, 0.45f, 0.4f);

            // Shift pallets first (topology change), then drive the route.
            foreach (var p in pallets)
                yield return MovePallet(p);

            bool rammed = false;
            yield return DriveRoute(forward: true, () => rammed, v => rammed = v);
            yield return DriveRoute(forward: false, () => rammed, v => rammed = v);
            unitBlocker.Reapply();

            AudioManager.LoopOff(LoopId, 0.5f);
            AudioManager.PlayAt("forklift_brake", unit.position, SfxBus.Machines, 0.5f);
        }

        IEnumerator MovePallet(Pallet p)
        {
            Vector2 from = p.tf.position;
            Vector2 to = p.atB ? p.shift.posA : p.shift.posB;
            p.atB = !p.atB;
            p.blocker.SetBlocked(false);
            float t = 0f, dur = 0.8f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Vector2 pos = Vector2.Lerp(from, to, t / dur);
                p.tf.position = new Vector3(pos.x, pos.y, 0f);
                yield return null;
            }
            p.tf.position = new Vector3(to.x, to.y, 0f);
            p.blocker.SetBlocked(true);
        }

        IEnumerator DriveRoute(bool forward, System.Func<bool> getRammed, System.Action<bool> setRammed)
        {
            int count = route.Length;
            float blockTimer = 0f;
            for (int step = 0; step < count - 1; step++)
            {
                Vector2 from = forward ? route[step] : route[count - 1 - step];
                Vector2 to = forward ? route[step + 1] : route[count - 2 - step];
                float dist = Vector2.Distance(from, to);
                float t = 0f;
                while (t < 1f)
                {
                    t += moveSpeed * Time.deltaTime / Mathf.Max(0.01f, dist);
                    Vector2 pos = Vector2.Lerp(from, to, Mathf.Clamp01(t));
                    unit.position = new Vector3(pos.x, pos.y, 0f);

                    // Blinking warning beacon while the vehicle moves.
                    if (beacon != null)
                    {
                        float blink = Mathf.PingPong(Time.time * 5f, 1f) > 0.5f ? 0.7f : 0.15f;
                        beacon.color = new Color(1f, 0.6f, 0.15f, blink);
                    }

                    blockTimer += Time.deltaTime;
                    if (blockTimer > 0.3f) { blockTimer = 0f; unitBlocker.Reapply(); }

                    var e = Engineer;
                    // Soft reverse-warning beep only while the engineer is near the vehicle.
                    if (e != null && !e.IsEscaped && Time.time >= nextWarnBeepAt &&
                        Vector2.Distance(e.Pos, pos) < 3.2f)
                    {
                        nextWarnBeepAt = Time.time + 2.3f;
                        AudioManager.PlayAt("forklift_beep", pos, SfxBus.Machines, 0.45f);
                    }
                    if (!getRammed() && e != null && Vector2.Distance(e.Pos, pos) < 0.9f)
                    {
                        setRammed(true);
                        AudioManager.PlayAt("forklift_bump", pos, SfxBus.Machines, 0.6f);
                        e.StunHit(stunDuration, displayName);
                        ReportEffective();
                    }
                    yield return null;
                }
            }
        }
    }
}
