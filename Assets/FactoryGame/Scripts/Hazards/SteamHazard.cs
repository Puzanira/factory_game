using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Hazards
{
    /// <summary>Scalding steam: strong deterrent, hurts Resolve on contact, animated plume.</summary>
    public class SteamHazard : HazardZone
    {
        ParticleSystem plume;

        protected override Color ZoneColor => new Color(1f, 0.45f, 0.3f, 0.30f);

        protected override void OnSetupDefaults()
        {
            slowFactor = 0.75f;
            stressPerSec = 8f;
            resolveOnEnter = 10f;
            dangerCost = 3f;
            ActiveChanged += OnActive;
        }

        protected override void BuildExtraVisuals()
        {
            plume = FxFactory.Steam(transform, Area.size);
            // Dormant zones must look OFF: particles autoplay on creation, and
            // SetActive is only called later (if ever) — stop them now.
            if (!Active) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnActive(bool active)
        {
            if (plume == null) return;
            if (active) plume.Play();
            else plume.Stop();
        }
    }
}
