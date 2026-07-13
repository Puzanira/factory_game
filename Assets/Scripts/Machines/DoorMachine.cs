using System.Collections;
using UnityEngine;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.Machines
{
    /// <summary>
    /// A powered gate/door. Closed doors block grid pathfinding; toggling triggers
    /// navigation revalidation for the engineer automatically via the grid event.
    /// Heavy industrial visual: striped frame band, sliding two-tone slab, blinking
    /// warning lamp while moving.
    /// </summary>
    public class DoorMachine : InteractableMachine
    {
        bool closed;
        GridBlocker blocker;
        Transform panel;
        SpriteRenderer panelSr;
        SpriteRenderer panelStripe;
        SpriteRenderer warnLamp;
        bool horizontal; // door slab wider than tall

        public override string CommandLabel =>
            displayName + " — " + (closed ? Loc.VerbOpen : Loc.VerbClose);

        public override string Description => closed
            ? "Снова открыть проход."
            : (string.IsNullOrEmpty(description) ? "Перекрыть этот проход." : description);

        public override string ActivationMessage =>
            displayName.ToUpper() + (closed ? ": ЗАКРЫТО" : ": ОТКРЫТО");

        protected override void OnConfigure(MachineSpec s)
        {
            closed = s.startsClosed;
            horizontal = s.size.x >= s.size.y;

            Vector2 alongAxis = horizontal ? Vector2.right : Vector2.up;
            float half = (horizontal ? s.size.x : s.size.y) * 0.5f;

            // Hazard-stripe threshold band under the door.
            Vector2 bandSize = s.size + (horizontal ? new Vector2(0.5f, 0.12f) : new Vector2(0.12f, 0.5f));
            var band = Viz.MakeTiled("StripeBand", transform, TextureFactory.HazardStripes(),
                new Color(1f, 1f, 1f, 0.85f), Vector2.zero, bandSize, 3);
            band.transform.localScale = Vector3.one;

            // Frame posts with lamps.
            Vector2 postSize = new Vector2(0.4f, 0.4f);
            Viz.Make("PostA", transform, PlaceholderShape.Square, new Color(0.55f, 0.5f, 0.3f), alongAxis * (half + 0.12f), postSize, 6);
            Viz.Make("PostB", transform, PlaceholderShape.Square, new Color(0.55f, 0.5f, 0.3f), -alongAxis * (half + 0.12f), postSize, 6);
            warnLamp = FxFactory.Glow(transform, new Color(1f, 0.35f, 0.2f, 0f),
                new Vector2(0.9f, 0.9f), 8);
            warnLamp.transform.localPosition = alongAxis * (half + 0.12f);

            // Sliding slab: heavy plate + darker inset + handle line.
            var slabRoot = new GameObject("Slab");
            slabRoot.transform.SetParent(transform, false);
            panel = slabRoot.transform;
            panelSr = Viz.Make("Plate", panel, PlaceholderShape.Square, new Color(0.72f, 0.3f, 0.24f), Vector2.zero, Vector2.one, 5);
            panelStripe = Viz.Make("Inset", panel, PlaceholderShape.Square, new Color(0.5f, 0.2f, 0.16f), Vector2.zero,
                new Vector2(0.86f, 0.6f), 6);

            blocker = gameObject.AddComponent<GridBlocker>();
            blocker.size = s.size;
            blocker.SetBlocked(closed);
            ApplyPanelVisual(instant: true);
        }

        protected override void BuildPreview(Transform root)
        {
            Viz.Make("DoorArea", root, PlaceholderShape.Square,
                new Color(1f, 0.85f, 0.3f, 0.28f), Vector2.zero, spec.size + new Vector2(0.3f, 0.3f), 9, unlit: true);
        }

        protected override IEnumerator OnActivate()
        {
            closed = !closed;
            Vector2 pos = transform.position;
            if (closed)
            {
                AudioManager.PlayAt("door_warn", pos, SfxBus.Machines, 0.45f);
                AudioManager.PlayAt("door_close", pos, SfxBus.Machines, 0.75f);
            }
            else
            {
                AudioManager.PlayAt("door_open", pos, SfxBus.Machines, 0.65f);
            }
            blocker.SetBlocked(closed);
            float t = 0f;
            const float dur = 0.55f; // slower, telegraphed slide
            while (t < dur)
            {
                t += Time.deltaTime;
                ApplyPanelVisual(instant: false, k: t / dur);
                // Blinking warning lamp while the door moves.
                if (warnLamp != null)
                {
                    float blink = Mathf.PingPong(t * 6f, 1f) > 0.5f ? 0.75f : 0.1f;
                    warnLamp.color = new Color(1f, 0.35f, 0.2f, blink);
                }
                yield return null;
            }
            ApplyPanelVisual(instant: true);
            if (warnLamp != null) warnLamp.color = new Color(1f, 0.35f, 0.2f, 0f);
        }

        void ApplyPanelVisual(bool instant, float k = 1f)
        {
            if (panel == null) return;
            // Closed = full slab; open = slab retracted to 15% along its long axis.
            float target = closed ? 1f : 0.15f;
            float start = closed ? 0.15f : 1f;
            float f = instant ? target : Mathf.Lerp(start, target, k);
            Vector3 scale = new Vector3(spec.size.x, spec.size.y, 1f);
            if (horizontal) scale.x *= f; else scale.y *= f;
            panel.localScale = scale;
            if (panelSr != null)
                panelSr.color = closed ? new Color(0.72f, 0.3f, 0.24f) : new Color(0.42f, 0.47f, 0.44f);
            if (panelStripe != null)
                panelStripe.color = closed ? new Color(0.5f, 0.2f, 0.16f) : new Color(0.3f, 0.35f, 0.32f);
        }
    }
}
