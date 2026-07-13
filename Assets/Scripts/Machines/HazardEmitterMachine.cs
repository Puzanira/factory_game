using System.Collections;
using UnityEngine;
using LastShift.Data;
using LastShift.Hazards;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// Machine that projects a temporary hazard zone into the room: cleaning spray
    /// (wet floor), freezer fans (cold fog), ice system (frozen floor), steam vents.
    /// </summary>
    public class HazardEmitterMachine : InteractableMachine
    {
        HazardZone zone;
        float emitDuration = 9f;
        Rect emitRect;
        HazardKind emitKind;

        public override string ActivationMessage
        {
            get
            {
                switch (emitKind)
                {
                    case HazardKind.Cold: return displayName.ToUpper() + ": ТУМАН ВЫПУЩЕН";
                    case HazardKind.Steam: return displayName.ToUpper() + ": ПАР ВЫПУЩЕН";
                    default: return displayName.ToUpper() + ": ПОЛ ОБРАБОТАН";
                }
            }
        }

        protected override void OnConfigure(MachineSpec s)
        {
            emitDuration = s.emitDuration > 0f ? s.emitDuration : 9f;
            emitRect = s.emitRect;
            emitKind = s.emitKind;

            Color nozzleColor = s.emitKind == HazardKind.Cold ? new Color(0.5f, 0.75f, 1f)
                : s.emitKind == HazardKind.Steam ? new Color(0.95f, 0.6f, 0.5f)
                : new Color(0.4f, 0.85f, 0.9f);

            // Housing with a feed pipe running toward the affected zone.
            FxFactory.Shadow(transform, new Vector2(0.9f, 0.6f), 0.35f, 3);
            Vector2 toZone = emitRect.center - (Vector2)transform.position;
            float pipeLen = Mathf.Min(toZone.magnitude, 1.6f);
            if (pipeLen > 0.3f)
            {
                float ang = Mathf.Atan2(toZone.y, toZone.x) * Mathf.Rad2Deg;
                var pipe = Viz.Make("FeedPipe", transform, PlaceholderShape.Square,
                    new Color(0.45f, 0.48f, 0.52f), toZone.normalized * pipeLen * 0.5f,
                    new Vector2(pipeLen, 0.16f), 4);
                pipe.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            }
            Viz.Make("Housing", transform, PlaceholderShape.Square,
                new Color(0.35f, 0.38f, 0.42f), Vector2.zero, new Vector2(0.8f, 0.8f), 5);
            Viz.Make("HousingTop", transform, PlaceholderShape.Square,
                new Color(0.45f, 0.48f, 0.53f), new Vector2(0f, 0.12f), new Vector2(0.62f, 0.3f), 6);
            Viz.Make("Valve", transform, PlaceholderShape.Circle, new Color(0.7f, 0.3f, 0.25f),
                new Vector2(-0.2f, -0.18f), new Vector2(0.2f, 0.2f), 7);
            Viz.Make("Nozzle", transform, PlaceholderShape.Circle, nozzleColor,
                new Vector2(0.16f, -0.05f), new Vector2(0.32f, 0.32f), 7);

            zone = HazardFactory.Create(s.emitKind, emitRect, startsActive: false, parent: transform.parent);
            zone.name = displayName + " Zone";
        }

        protected override void BuildPreview(Transform root)
        {
            Vector2 center = emitRect.center - (Vector2)transform.position;
            Viz.Make("EmitZone", root, PlaceholderShape.Square,
                new Color(0.4f, 0.85f, 1f, 0.22f), center, emitRect.size, 11, unlit: true);
            Viz.Make("EmitEdge", root, PlaceholderShape.Ring,
                new Color(0.4f, 0.85f, 1f, 0.6f), center,
                new Vector2(Mathf.Min(emitRect.width, 2.5f), Mathf.Min(emitRect.height, 2.5f)), 11, unlit: true);
        }

        protected override string ActivateSfxName => "valve_click";

        protected override IEnumerator OnActivate()
        {
            // Zone loops/bursts themselves are handled by the zone's HazardAudio.
            if (zone != null) zone.ActivateFor(emitDuration);
            yield return new WaitForSeconds(1f); // stay Active briefly; cooldown runs while the zone persists
        }
    }
}
