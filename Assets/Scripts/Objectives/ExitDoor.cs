using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Objectives
{
    /// <summary>
    /// The room exit. Locked (and path-blocked) until the engineer's Resolve breaks;
    /// when the retreating engineer reaches it, the room is complete.
    /// Visual: heavy frame with hazard stripes, sliding slab, green emergency lamp
    /// that stays dim while locked and glows when the way is open.
    /// </summary>
    public class ExitDoor : ObjectiveNode
    {
        public bool Locked { get; private set; } = true;

        GridBlocker blocker;
        Transform slab;
        SpriteRenderer slabSr;
        SpriteRenderer lamp;
        SpriteRenderer lampGlow;
        Vector2 size = new Vector2(2.4f, 0.9f);
        bool reported;

        public void Setup(Vector2 pos, Vector2 exitSize)
        {
            objectiveName = "Exit";
            size = exitSize;
            transform.position = new Vector3(pos.x, pos.y, 0f);

            // Striped threshold + frame.
            Viz.MakeTiled("Threshold", transform, TextureFactory.HazardStripes(),
                new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, -size.y * 0.65f),
                new Vector2(size.x + 0.3f, 0.18f), 3);
            Viz.Make("Frame", transform, PlaceholderShape.Square, new Color(0.3f, 0.62f, 0.38f),
                Vector2.zero, size + new Vector2(0.3f, 0.3f), 4);

            // Sliding slab (two-tone with a center split line).
            var slabRoot = new GameObject("Slab");
            slabRoot.transform.SetParent(transform, false);
            slab = slabRoot.transform;
            slabSr = Viz.Make("Plate", slab, PlaceholderShape.Square, new Color(0.5f, 0.24f, 0.2f), Vector2.zero, Vector2.one, 5);
            Viz.Make("Split", slab, PlaceholderShape.Square, new Color(0.2f, 0.12f, 0.1f), Vector2.zero, new Vector2(0.03f, 0.9f), 6);
            slab.localScale = new Vector3(size.x, size.y, 1f);

            // Emergency lamp above the door.
            lamp = Viz.Make("Lamp", transform, PlaceholderShape.Circle, new Color(0.25f, 0.5f, 0.3f),
                new Vector2(0f, size.y * 0.5f + 0.28f), new Vector2(0.22f, 0.22f), 7);
            lampGlow = FxFactory.Glow(transform, new Color(0.35f, 0.95f, 0.5f, 0.1f), new Vector2(1.6f, 1.6f), 6);
            lampGlow.transform.localPosition = new Vector3(0f, size.y * 0.5f + 0.28f, 0f);
            FxFactory.PointLight(transform, new Vector2(0f, size.y * 0.5f + 0.28f),
                new Color(0.4f, 1f, 0.55f), 2.4f, 0.6f);

            blocker = gameObject.AddComponent<GridBlocker>();
            blocker.size = size;
            blocker.SetBlocked(true);
        }

        public void Unlock()
        {
            if (!Locked) return;
            Locked = false;
            blocker.SetBlocked(false);
            if (slab != null) slab.localScale = new Vector3(size.x * 0.16f, size.y, 1f);
            if (slabSr != null) slabSr.color = new Color(0.25f, 0.4f, 0.3f, 0.7f);
            if (lamp != null) lamp.color = new Color(0.35f, 0.98f, 0.5f);
        }

        void Update()
        {
            // Lamp breathing: dim red-green while locked, bright green pulse when open.
            if (lampGlow != null)
            {
                float pulse = Mathf.PingPong(Time.time * (Locked ? 1.2f : 3.5f), 1f);
                lampGlow.color = Locked
                    ? new Color(0.35f, 0.95f, 0.5f, 0.06f + 0.05f * pulse)
                    : new Color(0.35f, 0.98f, 0.5f, 0.25f + 0.2f * pulse);
            }

            if (Locked || reported) return;
            var e = LastShift.Engineer.EngineerController.Instance;
            if (e == null || !e.IsRetreating) return;
            if (Vector2.Distance(e.Pos, transform.position) < Mathf.Max(size.x, size.y) * 0.6f)
            {
                reported = true;
                RaiseCompleted();
                var lm = Core.LevelManager.Instance;
                if (lm != null) lm.OnEngineerEscaped();
            }
        }
    }
}
