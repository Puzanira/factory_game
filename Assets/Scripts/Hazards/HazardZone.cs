using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Hazards
{
    /// <summary>
    /// Base hazard zone: a visibly marked rectangle that slows/stresses the engineer
    /// and registers danger on the path grid so his AI routes around it. The engineer
    /// polls zones via the static All list; Resolve loss on entry is applied there.
    /// </summary>
    public class HazardZone : MonoBehaviour
    {
        public static readonly List<HazardZone> All = new List<HazardZone>();

        public HazardKind kind = HazardKind.Danger;
        [Tooltip("Movement speed multiplier while inside (1 = no slow).")]
        public float slowFactor = 0.8f;
        public float stressPerSec = 3f;
        public float resolveOnEnter = 8f;
        [Tooltip("Pathfinding danger cost added over this zone.")]
        public float dangerCost = 2f;
        public bool escalationExpand;

        public event System.Action<bool> ActiveChanged;

        public Rect Area { get; private set; }
        public bool Active { get; private set; }

        protected SpriteRenderer overlay;
        Coroutine timedRoutine;
        bool dangerApplied;

        protected virtual Color ZoneColor => new Color(1f, 0.25f, 0.2f, 0.30f);

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); ApplyDanger(false); }
        protected virtual void OnDestroy() { All.Remove(this); ApplyDanger(false); }

        public void Setup(HazardKind hazardKind, Rect area, bool startsActive)
        {
            kind = hazardKind;
            Area = area;
            transform.position = new Vector3(area.center.x, area.center.y, 0f);
            OnSetupDefaults();
            BuildVisual();
            BuildExtraVisuals();
            gameObject.AddComponent<LastShift.Audio.HazardAudio>().Bind(this);
            if (startsActive) SetActive(true);
            else UpdateVisualAlpha(0.18f); // dormant zones stay faintly visible
        }

        /// <summary>Subclasses add their kind-specific art (particles, puddles, frost).</summary>
        protected virtual void BuildExtraVisuals() { }

        /// <summary>Subclasses set their slow/stress/resolve profile here.</summary>
        protected virtual void OnSetupDefaults() { }

        void BuildVisual()
        {
            overlay = Viz.Make("Overlay", transform, PlaceholderShape.Square,
                ZoneColor, Vector2.zero, Area.size, 2, unlit: true);
            var edge = Viz.Make("Edge", transform, PlaceholderShape.Square,
                new Color(ZoneColor.r, ZoneColor.g, ZoneColor.b, 0.55f),
                new Vector2(0f, Area.size.y * 0.5f), new Vector2(Area.size.x, 0.06f), 3, unlit: true);
            edge.name = "EdgeTop";
            Viz.Make("EdgeBottom", transform, PlaceholderShape.Square,
                new Color(ZoneColor.r, ZoneColor.g, ZoneColor.b, 0.55f),
                new Vector2(0f, -Area.size.y * 0.5f), new Vector2(Area.size.x, 0.06f), 3, unlit: true);
        }

        public bool Contains(Vector2 p) => Active && Area.Contains(p);

        public float SlowFactor => slowFactor;
        public float StressPerSec => stressPerSec;
        public float ResolveOnEnter => resolveOnEnter;

        public void SetActive(bool active)
        {
            if (Active == active) return;
            Active = active;
            if (overlay != null) overlay.gameObject.SetActive(true);
            UpdateVisualAlpha(active ? 1f : 0.18f);
            ApplyDanger(active);
            ActiveChanged?.Invoke(active);
        }

        void ApplyDanger(bool apply)
        {
            if (apply == dangerApplied) return;
            var grid = PathGrid.Instance;
            if (grid == null) { dangerApplied = false; return; }
            grid.AddDanger(Area, apply ? dangerCost : -dangerCost);
            dangerApplied = apply;
        }

        void UpdateVisualAlpha(float k)
        {
            if (overlay == null) return;
            var c = ZoneColor;
            overlay.color = new Color(c.r, c.g, c.b, c.a * k);
        }

        public void ActivateFor(float seconds)
        {
            if (timedRoutine != null) StopCoroutine(timedRoutine);
            timedRoutine = StartCoroutine(TimedRoutine(seconds));
        }

        IEnumerator TimedRoutine(float seconds)
        {
            SetActive(true);
            yield return new WaitForSeconds(seconds);
            SetActive(false);
            timedRoutine = null;
        }

        /// <summary>Escalation: grow the zone around its centre.</summary>
        public void Expand(float scale)
        {
            bool wasActive = Active;
            ApplyDanger(false);
            Vector2 newSize = Area.size * scale;
            Area = new Rect(Area.center - newSize * 0.5f, newSize);
            if (overlay != null) overlay.transform.localScale = new Vector3(newSize.x, newSize.y, 1f);
            if (wasActive) ApplyDanger(true);
            else SetActive(true); // escalation also switches dormant zones on
        }

        protected virtual void Update()
        {
            if (!Active || overlay == null) return;
            // Gentle pulsing so hazards read as live.
            float pulse = 0.85f + 0.15f * Mathf.PingPong(Time.time * 1.6f, 1f);
            UpdateVisualAlpha(pulse);
        }
    }

    /// <summary>Creates the right HazardZone subclass for a kind (used by RoomBuilder fallback and emitters).</summary>
    public static class HazardFactory
    {
        public static HazardZone Create(HazardKind kind, Rect area, bool startsActive, Transform parent = null)
        {
            var go = new GameObject(kind + "Hazard");
            if (parent != null) go.transform.SetParent(parent, false);
            HazardZone zone;
            switch (kind)
            {
                case HazardKind.Steam: zone = go.AddComponent<SteamHazard>(); break;
                case HazardKind.Cold: zone = go.AddComponent<ColdHazard>(); break;
                case HazardKind.Slippery: zone = go.AddComponent<SlipperyFloor>(); break;
                default: zone = go.AddComponent<HazardZone>(); break;
            }
            zone.Setup(kind, area, startsActive);
            return zone;
        }
    }
}
