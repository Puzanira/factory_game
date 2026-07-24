using UnityEngine;
using UnityEngine.UI;

namespace LastShift.UI
{
    /// <summary>
    /// Dims the whole screen except one rectangular cut-out, so exactly one tutorial
    /// target stays readable while the rest of the interface and the room fall back.
    /// Built from four opaque bands around the hole (no shader, no stencil), faded
    /// on unscaled time so it also works while the lesson freezes the clock.
    /// </summary>
    public class TutorialFocusOverlay : MonoBehaviour
    {
        Canvas canvas;
        CanvasGroup group;
        readonly RectTransform[] bands = new RectTransform[4];

        static readonly Color Dim = new Color(0.008f, 0.016f, 0.012f, 0.88f);

        float targetAlpha;
        Rect hole;
        bool hasHole;

        public void Init(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            transform.SetParent(canvas.transform, false);
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            for (int i = 0; i < bands.Length; i++)
            {
                var go = new GameObject("Dim" + i);
                go.transform.SetParent(transform, false);
                bands[i] = go.AddComponent<RectTransform>();
                var img = go.AddComponent<Image>();
                img.color = Dim;
                img.raycastTarget = false;
            }
            SetHoleRects(new Rect(0f, 0f, 0f, 0f), false);
        }

        /// <summary>Dim everything but this screen-pixel rect.</summary>
        public void Focus(Rect screenRect)
        {
            hole = screenRect;
            hasHole = true;
            targetAlpha = 1f;
            SetHoleRects(hole, true);
        }

        /// <summary>Dim the whole screen (no cut-out).</summary>
        public void FocusNone()
        {
            hasHole = false;
            targetAlpha = 1f;
            SetHoleRects(new Rect(0f, 0f, 0f, 0f), false);
        }

        public void Hide() => targetAlpha = 0f;

        void SetHoleRects(Rect h, bool useHole)
        {
            float w = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            if (!useHole)
            {
                TutorialUiSpace.Apply(bands[0], new Rect(0f, 0f, w, sh), canvas);
                for (int i = 1; i < bands.Length; i++)
                    TutorialUiSpace.Apply(bands[i], new Rect(0f, 0f, 0f, 0f), canvas);
                return;
            }
            float x0 = Mathf.Clamp(h.xMin, 0f, w);
            float x1 = Mathf.Clamp(h.xMax, 0f, w);
            float y0 = Mathf.Clamp(h.yMin, 0f, sh);
            float y1 = Mathf.Clamp(h.yMax, 0f, sh);
            TutorialUiSpace.Apply(bands[0], Rect.MinMaxRect(0f, y1, w, sh), canvas);   // above
            TutorialUiSpace.Apply(bands[1], Rect.MinMaxRect(0f, 0f, w, y0), canvas);   // below
            TutorialUiSpace.Apply(bands[2], Rect.MinMaxRect(0f, y0, x0, y1), canvas);  // left
            TutorialUiSpace.Apply(bands[3], Rect.MinMaxRect(x1, y0, w, y1), canvas);   // right
        }

        void Update()
        {
            if (group == null) return;
            // Fades stay short and never strobe: this is a readability aid, not an effect.
            float speed = targetAlpha > group.alpha ? 4.5f : 6f;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * speed);
            if (hasHole && group.alpha > 0.001f) SetHoleRects(hole, true);
        }
    }
}
