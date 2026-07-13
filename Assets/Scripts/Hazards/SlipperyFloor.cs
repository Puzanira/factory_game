using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Hazards
{
    /// <summary>
    /// Wet or icy floor: milder slow, low stress, small Resolve sting on entry.
    /// Reflective puddle blobs with a moving shine instead of a flat rectangle.
    /// </summary>
    public class SlipperyFloor : HazardZone
    {
        SpriteRenderer shine;

        protected override Color ZoneColor => new Color(0.35f, 0.9f, 0.95f, 0.16f);

        protected override void OnSetupDefaults()
        {
            slowFactor = 0.65f;
            stressPerSec = 2f;
            resolveOnEnter = 5f;
            dangerCost = 1.2f;
        }

        protected override void BuildExtraVisuals()
        {
            // Reflective puddles scattered over the zone.
            var puddleColor = new Color(0.55f, 0.72f, 0.85f, 0.5f);
            int count = Mathf.Max(2, Mathf.FloorToInt(Area.size.x * Area.size.y / 2.5f));
            count = Mathf.Min(count, 7);
            for (int i = 0; i < count; i++)
            {
                float fx = (Hash(i * 3 + 1) - 0.5f) * (Area.size.x - 0.8f);
                float fy = (Hash(i * 7 + 2) - 0.5f) * (Area.size.y - 0.5f);
                float sx = 0.7f + Hash(i * 5 + 3) * 0.9f;
                var go = new GameObject("Puddle" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(fx, fy, 0f);
                go.transform.localScale = new Vector3(sx, sx * 0.6f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = TextureFactory.Puddle(i * 13 + 5);
                sr.sharedMaterial = SpriteFactory.UnlitMaterial;
                sr.color = puddleColor;
                sr.sortingOrder = 2;
            }

            // A thin light streak that drifts — reads as reflection/shine.
            shine = Viz.Make("Shine", transform, PlaceholderShape.Square,
                new Color(0.95f, 0.98f, 1f, 0.18f), Vector2.zero,
                new Vector2(Mathf.Min(1.6f, Area.size.x * 0.4f), 0.08f), 3, unlit: true);
            shine.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        }

        static float Hash(int i)
        {
            unchecked
            {
                uint h = (uint)i * 747796405u + 2891336453u;
                h = ((h >> 13) ^ h) * 1274126177u;
                return ((h >> 16) & 0xffffu) / 65535f;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (shine != null && Active)
            {
                float t = Mathf.PingPong(Time.time * 0.35f, 1f) - 0.5f;
                shine.transform.localPosition = new Vector3(t * (Area.size.x - 1f), t * 0.3f, 0f);
            }
        }
    }
}
