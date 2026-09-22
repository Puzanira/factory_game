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
        /// <summary>
        /// Screen-pixel rect of a UI element.
        ///
        /// On an overlay canvas world units already are screen pixels. The lesson's
        /// canvas is an overlay in the game, but the headless frame grabber
        /// (SmokeShots) flips canvases to camera space for one frame to get the UI
        /// into its capture — so the conversion has to survive both, otherwise every
        /// captured lesson frame comes back without its callout.
        /// </summary>
        public static Rect ScreenRectOf(RectTransform rt)
        {
            if (rt == null) return new Rect(0f, 0f, 0f, 0f);
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Camera cam = CameraOf(rt);
            Vector2 a = cam == null ? (Vector2)corners[0] : (Vector2)cam.WorldToScreenPoint(corners[0]);
            Vector2 b = cam == null ? (Vector2)corners[2] : (Vector2)cam.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                                   Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>The camera a rect's canvas renders through — null while it is an overlay.</summary>
        static Camera CameraOf(Component c)
        {
            Canvas canvas = c != null ? c.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
        }

        /// <summary>Puts an element's centre on a screen point, whatever the canvas mode.</summary>
        static void SetScreenCenter(RectTransform rt, Vector2 screenPoint, Canvas canvas)
        {
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            if (root == null)
            {
                var found = rt.GetComponentInParent<Canvas>();
                root = found != null ? found.rootCanvas : null;
            }
            if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rt.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
                return;
            }
            var canvasRect = root.transform as RectTransform;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, root.worldCamera, out Vector2 local))
                rt.anchoredPosition = local;
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
            SetScreenCenter(rt, screenRect.center, canvas);
        }

        /// <summary>
        /// Move a fixed-size overlay element to a screen point without touching its
        /// size — so its own size can never feed back into the placement maths.
        /// </summary>
        public static void ApplyPosition(RectTransform rt, Vector2 screenCenter, Canvas canvas = null)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            SetScreenCenter(rt, screenCenter, canvas);
        }

        public static Rect Expand(Rect r, float px) =>
            Rect.MinMaxRect(r.xMin - px, r.yMin - px, r.xMax + px, r.yMax + px);

        public static Rect FullScreen => new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height);

        /// <summary>
        /// The area the lesson may place things in, in the same pixel space its
        /// target rects come back in. That is Screen for an overlay canvas — but NOT
        /// while the frame grabber renders through a camera, where the canvas covers
        /// the capture size instead. Placing against Screen there threw the card into
        /// a corner of every captured frame.
        /// </summary>
        public static Rect Viewport(Canvas canvas)
        {
            var rect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (rect == null) return FullScreen;
            Rect r = ScreenRectOf(rect);
            return r.width > 1f && r.height > 1f ? r : FullScreen;
        }

        /// <summary>Clamp a rect fully inside the screen with a pixel padding.</summary>
        public static Rect ClampToScreen(Rect r, float pad) => ClampTo(r, pad, FullScreen);

        /// <summary>Clamp a rect fully inside a given viewport with a pixel padding.</summary>
        public static Rect ClampTo(Rect r, float pad, Rect view)
        {
            float x = Mathf.Clamp(r.x, view.xMin + pad, Mathf.Max(view.xMin + pad, view.xMax - pad - r.width));
            float y = Mathf.Clamp(r.y, view.yMin + pad, Mathf.Max(view.yMin + pad, view.yMax - pad - r.height));
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
