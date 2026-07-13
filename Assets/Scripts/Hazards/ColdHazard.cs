using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Hazards
{
    /// <summary>
    /// Freezing fog: heavy slow, steady stress. Active cold zones also drive the
    /// HUD fog overlay via FogIntensity. Rolling fog particles + frost edges;
    /// opacity kept low so routes and the engineer stay readable.
    /// </summary>
    public class ColdHazard : HazardZone
    {
        public static int ActiveColdCount { get; private set; }

        /// <summary>0..1 driver for the HUD fog overlay.</summary>
        public static float FogIntensity => Mathf.Clamp01(ActiveColdCount / 3f);

        bool counted;
        ParticleSystem fog;

        protected override Color ZoneColor => new Color(0.55f, 0.78f, 1f, 0.20f);

        protected override void OnSetupDefaults()
        {
            slowFactor = 0.6f;
            stressPerSec = 5f;
            resolveOnEnter = 8f;
            dangerCost = 2f;
            ActiveChanged += OnActiveChanged;
        }

        protected override void BuildExtraVisuals()
        {
            fog = FxFactory.ColdFog(transform, Area.size);

            // Frost edge buildup along the zone borders.
            var frost = new Color(0.85f, 0.95f, 1f, 0.35f);
            Viz.Make("FrostTop", transform, PlaceholderShape.Square, frost,
                new Vector2(0f, Area.size.y * 0.5f), new Vector2(Area.size.x, 0.16f), 3, unlit: true);
            Viz.Make("FrostBottom", transform, PlaceholderShape.Square, frost,
                new Vector2(0f, -Area.size.y * 0.5f), new Vector2(Area.size.x, 0.16f), 3, unlit: true);
        }

        void OnActiveChanged(bool active)
        {
            if (active && !counted) { ActiveColdCount++; counted = true; }
            else if (!active && counted) { ActiveColdCount = Mathf.Max(0, ActiveColdCount - 1); counted = false; }
            if (fog != null)
            {
                if (active) fog.Play();
                else fog.Stop();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (counted) { ActiveColdCount = Mathf.Max(0, ActiveColdCount - 1); counted = false; }
        }
    }
}
