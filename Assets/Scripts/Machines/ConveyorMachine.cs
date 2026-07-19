using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// A belt that pushes whatever stands on it (crates and the engineer).
    /// Activation reverses the belt (or starts it when stopped).
    /// Visuals: tiling rubber belt with slats, scrolling direction arrows, edge
    /// rails, milk containers riding the belt, green running lamp.
    /// </summary>
    public class ConveyorMachine : InteractableMachine
    {
        public static readonly List<ConveyorMachine> All = new List<ConveyorMachine>();

        bool running = true;
        int dirSign = 1;
        Vector2 baseDir = Vector2.right;
        float speed = 2.2f;
        Rect area;

        readonly List<Transform> arrows = new List<Transform>();
        readonly List<Transform> crates = new List<Transform>();
        SpriteRenderer runLamp;

        public Vector2 CurrentPush => running ? baseDir * (dirSign * speed) : Vector2.zero;

        public override string CommandLabel =>
            displayName + " — " + (running ? Loc.VerbReverse : Loc.VerbStart);

        public override string ActivationMessage =>
            displayName.ToUpper() + ": НАПРАВЛЕНИЕ ИЗМЕНЕНО";

        public override string PurposeLine => Loc.PurposeConveyor;
        public override string PurposeHint => Loc.PurposeConveyorHint;

        /// <summary>The belt only matters while the engineer is standing on (or beside) it.</summary>
        public override bool EngineerInEffectiveZone
        {
            get
            {
                var e = Engineer;
                if (e == null || e.IsEscaped) return false;
                Rect r = area;
                r.xMin -= 0.5f; r.xMax += 0.5f; r.yMin -= 0.5f; r.yMax += 0.5f;
                return r.Contains(e.Pos);
            }
        }

        public static Vector2 TotalPushAt(Vector2 p)
        {
            Vector2 total = Vector2.zero;
            for (int i = 0; i < All.Count; i++)
            {
                var c = All[i];
                if (c != null && c.running && c.area.Contains(p)) total += c.CurrentPush;
            }
            return total;
        }

        // One shared, refcounted motor loop for all running belts in the room —
        // never one AudioSource per conveyor and never a stacked/duplicated loop.
        static int runningLoopCount;
        bool loopCounted;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        void OnDisable()
        {
            All.Remove(this);
            ReportLoop(false);
        }

        void ReportLoop(bool audible)
        {
            if (audible == loopCounted) return;
            loopCounted = audible;
            runningLoopCount = Mathf.Max(0, runningLoopCount + (audible ? 1 : -1));
            if (audible && runningLoopCount == 1)
                AudioManager.LoopOn("conveyor_shared", "conveyor_loop", SfxBus.Machines, 0.45f, 0.7f);
            else if (!audible && runningLoopCount == 0)
                AudioManager.LoopOff("conveyor_shared", 0.9f);
        }

        protected override void OnConfigure(MachineSpec s)
        {
            baseDir = s.conveyorDir.sqrMagnitude > 0.01f ? s.conveyorDir.normalized : Vector2.right;
            speed = s.conveyorSpeed;
            area = Viz.RectAt(s.pos, s.size);

            // Belt bed with slat texture.
            Viz.MakeTiled("Belt", transform, TextureFactory.BeltTexture(), Color.white,
                Vector2.zero, s.size, 2);

            // Edge rails with end caps (pseudo-depth).
            Viz.Make("RailTop", transform, PlaceholderShape.Square, new Color(0.58f, 0.61f, 0.66f),
                new Vector2(0f, s.size.y * 0.5f), new Vector2(s.size.x + 0.15f, 0.12f), 4);
            Viz.Make("RailTopShadow", transform, PlaceholderShape.Square, new Color(0.1f, 0.1f, 0.12f, 0.6f),
                new Vector2(0f, s.size.y * 0.5f - 0.09f), new Vector2(s.size.x, 0.07f), 3);
            Viz.Make("RailBottom", transform, PlaceholderShape.Square, new Color(0.58f, 0.61f, 0.66f),
                new Vector2(0f, -s.size.y * 0.5f), new Vector2(s.size.x + 0.15f, 0.12f), 4);
            Viz.Make("LegL", transform, PlaceholderShape.Square, new Color(0.4f, 0.42f, 0.46f),
                new Vector2(-s.size.x * 0.5f, 0f), new Vector2(0.14f, s.size.y + 0.1f), 4);
            Viz.Make("LegR", transform, PlaceholderShape.Square, new Color(0.4f, 0.42f, 0.46f),
                new Vector2(s.size.x * 0.5f, 0f), new Vector2(0.14f, s.size.y + 0.1f), 4);

            // Running lamp.
            runLamp = FxFactory.Glow(transform, new Color(0.4f, 1f, 0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), 5);
            runLamp.transform.localPosition = new Vector2(-s.size.x * 0.5f, s.size.y * 0.5f);

            bool horizontal = Mathf.Abs(baseDir.x) >= Mathf.Abs(baseDir.y);
            float length = horizontal ? s.size.x : s.size.y;

            // Scrolling direction arrows (very visible).
            int arrowCount = Mathf.Max(3, Mathf.FloorToInt(length / 1.8f));
            for (int i = 0; i < arrowCount; i++)
            {
                float t = (i + 0.5f) / arrowCount - 0.5f;
                Vector2 p = horizontal ? new Vector2(t * s.size.x, 0f) : new Vector2(0f, t * s.size.y);
                var sr = Viz.Make("Arrow" + i, transform, PlaceholderShape.Arrow,
                    new Color(0.98f, 0.85f, 0.35f, 0.9f), p, new Vector2(0.62f, 0.44f), 3, unlit: true);
                arrows.Add(sr.transform);
            }

            // Milk containers riding the belt.
            int crateCount = Mathf.Max(1, Mathf.FloorToInt(length / 4f));
            for (int i = 0; i < crateCount; i++)
            {
                float t = (i + 0.5f) / crateCount - 0.5f;
                Vector2 p = horizontal ? new Vector2(t * s.size.x, 0f) : new Vector2(0f, t * s.size.y);
                var crate = new GameObject("MilkCrate" + i);
                crate.transform.SetParent(transform, false);
                crate.transform.localPosition = p;
                Viz.Make("Can", crate.transform, PlaceholderShape.Circle, new Color(0.85f, 0.87f, 0.9f),
                    Vector2.zero, new Vector2(0.5f, 0.5f), 4);
                Viz.Make("Lid", crate.transform, PlaceholderShape.Circle, new Color(0.6f, 0.64f, 0.7f),
                    Vector2.zero, new Vector2(0.3f, 0.3f), 5);
                Viz.Make("Cap", crate.transform, PlaceholderShape.Circle, new Color(0.42f, 0.62f, 0.85f),
                    Vector2.zero, new Vector2(0.12f, 0.12f), 6);
                crates.Add(crate.transform);
            }

            UpdateArrowFacing();
        }

        protected override void BuildPreview(Transform root)
        {
            // Affected-tiles band: the whole belt surface reads as the effect zone.
            Viz.Make("EffectBand", root, PlaceholderShape.Square,
                new Color(0.4f, 1f, 0.7f, 0.16f), Vector2.zero,
                spec.size + new Vector2(0.4f, 0.4f), 11, unlit: true);
            Viz.Make("FlowArrow", root, PlaceholderShape.Arrow,
                new Color(0.4f, 1f, 0.7f, 0.9f), Vector2.zero, new Vector2(1.8f, 1.2f), 12, unlit: true);
            UpdateArrowFacing();
        }

        protected override string ActivateSfxName => "conveyor_reverse";

        protected override IEnumerator OnActivate()
        {
            if (!running) running = true;
            else dirSign = -dirSign;
            UpdateArrowFacing();
            yield return new WaitForSeconds(0.25f);
        }

        void UpdateArrowFacing()
        {
            Vector2 d = baseDir * dirSign;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            foreach (var a in arrows)
                if (a != null) a.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (preview != null)
            {
                var flow = preview.transform.Find("FlowArrow");
                if (flow != null) flow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void Update()
        {
            if (spec == null) return;
            ReportLoop(running);
            if (runLamp != null)
            {
                float pulse = running ? 0.35f + 0.25f * Mathf.PingPong(Time.time * 2f, 1f) : 0.05f;
                runLamp.color = new Color(0.4f, 1f, 0.5f, pulse);
            }
            if (!running) return;

            Vector2 push = CurrentPush * Time.deltaTime;

            // Crates ride and wrap.
            foreach (var crate in crates)
            {
                if (crate == null) continue;
                Vector3 p = crate.position + new Vector3(push.x, push.y, 0f);
                if (p.x < area.xMin) p.x = area.xMax; else if (p.x > area.xMax) p.x = area.xMin;
                if (p.y < area.yMin) p.y = area.yMax; else if (p.y > area.yMax) p.y = area.yMin;
                crate.position = p;
            }

            // Arrows scroll with the belt (half speed, wrapping) so direction reads instantly.
            Vector2 arrowPush = push * 0.6f;
            foreach (var a in arrows)
            {
                if (a == null) continue;
                Vector3 p = a.position + new Vector3(arrowPush.x, arrowPush.y, 0f);
                if (p.x < area.xMin) p.x = area.xMax; else if (p.x > area.xMax) p.x = area.xMin;
                if (p.y < area.yMin) p.y = area.yMax; else if (p.y > area.yMax) p.y = area.yMin;
                a.position = p;
            }
        }
    }
}
