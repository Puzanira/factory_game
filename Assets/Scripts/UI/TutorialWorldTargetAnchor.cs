using UnityEngine;

namespace LastShift.UI
{
    /// <summary>
    /// Shared screen-space math for the tutorial overlay. Every tutorial visual
    /// (dim overlay, highlight frame, callout panel) works in screen pixels, so a
    /// world object and a UI element can be targeted through exactly one code path.
    /// </summary>
    public static class TutorialUiSpace
    {
        /// <summary>Screen-pixel rect of a UI element on a ScreenSpaceOverlay canvas.</summary>
        public static Rect ScreenRectOf(RectTransform rt)
        {
            if (rt == null) return new Rect(0f, 0f, 0f, 0f);
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // overlay canvas: world units == screen pixels
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Screen-pixel rect covering a world-space box (centre + size in units).</summary>
        public static Rect ScreenRectOfWorld(Vector2 worldCenter, Vector2 worldSize)
        {
            Camera cam = Camera.main;
            if (cam == null) return new Rect(0f, 0f, 0f, 0f);
            Vector2 h = worldSize * 0.5f;
            Vector3 a = cam.WorldToScreenPoint(new Vector3(worldCenter.x - h.x, worldCenter.y - h.y, 0f));
            Vector3 b = cam.WorldToScreenPoint(new Vector3(worldCenter.x + h.x, worldCenter.y + h.y, 0f));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                   Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Place a RectTransform of an overlay canvas onto a screen-pixel rect.</summary>
        public static void Apply(RectTransform rt, Rect screenRect, Canvas canvas)
        {
            if (rt == null) return;
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(screenRect.width / scale, screenRect.height / scale);
            rt.position = new Vector3(screenRect.center.x, screenRect.center.y, 0f);
        }

        /// <summary>
        /// Move a fixed-size overlay element to a screen point without touching its
        /// size — so its own size can never feed back into the placement maths.
        /// </summary>
        public static void ApplyPosition(RectTransform rt, Vector2 screenCenter)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = new Vector3(screenCenter.x, screenCenter.y, 0f);
        }

        public static Rect Expand(Rect r, float px) =>
            Rect.MinMaxRect(r.xMin - px, r.yMin - px, r.xMax + px, r.yMax + px);

        public static Rect FullScreen => new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);

        /// <summary>Clamp a rect fully inside the screen with a pixel padding.</summary>
        public static Rect ClampToScreen(Rect r, float pad)
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            float x = Mathf.Clamp(r.x, pad, Mathf.Max(pad, w - pad - r.width));
            float y = Mathf.Clamp(r.y, pad, Mathf.Max(pad, h - pad - r.height));
            return new Rect(x, y, r.width, r.height);
        }
    }

    /// <summary>
    /// Marks a world object as a tutorial target and reports its screen rect. The
    /// declared size is the object's real footprint, so highlights match the thing
    /// they point at instead of wrapping it in an oversized generic circle.
    /// </summary>
    public class TutorialWorldTargetAnchor : MonoBehaviour
    {
        [Tooltip("World-space footprint of the target (units).")]
        public Vector2 size = Vector2.one;
        [Tooltip("Local offset of the highlight centre from the transform (units).")]
        public Vector2 offset = Vector2.zero;

        public static TutorialWorldTargetAnchor Attach(Transform target, Vector2 size, Vector2 offset = default)
        {
            if (target == null) return null;
            var anchor = target.gameObject.GetComponent<TutorialWorldTargetAnchor>();
            if (anchor == null) anchor = target.gameObject.AddComponent<TutorialWorldTargetAnchor>();
            anchor.size = size;
            anchor.offset = offset;
            return anchor;
        }

        public Vector2 WorldCenter => (Vector2)transform.position + offset;

        public Rect ScreenRect() => TutorialUiSpace.ScreenRectOfWorld(WorldCenter, size);
    }
}
