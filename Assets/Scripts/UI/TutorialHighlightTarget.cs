using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>Highlight geometry, chosen to match what the target actually is.</summary>
    public enum TutorialHighlightShape
    {
        /// <summary>Rectangular objects and UI panels: thin border + corner brackets.</summary>
        Rect,
        /// <summary>Irregular objects (engineer, arm, forklift): corner brackets only.</summary>
        Brackets,
        /// <summary>Genuinely round elements (lamps, round indicators): thin ring.</summary>
        Circle,
    }

    /// <summary>
    /// Draws the tutorial's single target marker over a screen-pixel rect: a thin
    /// technical frame with terminal corner brackets, a bracket-only frame for
    /// irregular shapes, or a ring for truly circular elements. Amber = current
    /// target, green = confirmed, red = warning. Pulse stays subtle (no strobe).
    /// </summary>
    public class TutorialHighlightTarget : MonoBehaviour
    {
        public static readonly Color Amber = new Color(0.98f, 0.78f, 0.32f, 1f);
        public static readonly Color Green = new Color(0.45f, 0.98f, 0.55f, 1f);
        public static readonly Color Warn = new Color(0.95f, 0.35f, 0.25f, 1f);

        Canvas canvas;
        RectTransform holder;
        CanvasGroup group;
        readonly List<Image> parts = new List<Image>();
        TutorialHighlightShape shape = TutorialHighlightShape.Rect;
        Color color = Amber;
        bool visible;

        public void Init(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            transform.SetParent(canvas.transform, false);
            holder = gameObject.AddComponent<RectTransform>();
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Rebuild(TutorialHighlightShape.Rect);
        }

        /// <summary>Show the marker over a screen rect (pixels) in the given shape/colour.</summary>
        public void Show(Rect screenRect, TutorialHighlightShape newShape, Color newColor)
        {
            if (newShape != shape || parts.Count == 0) Rebuild(newShape);
            color = newColor;
            visible = true;
            // Small breathing room so the frame never sits on top of the target.
            TutorialUiSpace.Apply(holder, TutorialUiSpace.Expand(screenRect, 10f), canvas);
        }

        public void SetColor(Color c) => color = c;

        public void Hide() => visible = false;

        void Rebuild(TutorialHighlightShape newShape)
        {
            shape = newShape;
            for (int i = holder.childCount - 1; i >= 0; i--) Destroy(holder.GetChild(i).gameObject);
            parts.Clear();

            if (shape == TutorialHighlightShape.Circle)
            {
                var ring = Bar("Ring", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                ring.sprite = SpriteFactory.Get(PlaceholderShape.Ring);
                return;
            }

            const float t = 2.5f;   // border thickness (canvas units)
            const float len = 26f;  // corner bracket length

            if (shape == TutorialHighlightShape.Rect)
            {
                Bar("EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, t));
                Bar("EdgeB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, t));
                Bar("EdgeL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(t, 0f));
                Bar("EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(t, 0f));
            }

            // Corner brackets (both Rect and Brackets shapes).
            const float bt = 3.5f;
            Corner("BL", new Vector2(0f, 0f), new Vector2(0f, 0f), len, bt, +1, +1);
            Corner("BR", new Vector2(1f, 0f), new Vector2(1f, 0f), len, bt, -1, +1);
            Corner("TL", new Vector2(0f, 1f), new Vector2(0f, 1f), len, bt, +1, -1);
            Corner("TR", new Vector2(1f, 1f), new Vector2(1f, 1f), len, bt, -1, -1);
        }

        /// <summary>One L-shaped corner bracket: a horizontal and a vertical stub.</summary>
        void Corner(string name, Vector2 anchor, Vector2 pivot, float len, float thick, int dirX, int dirY)
        {
            var h = Bar(name + "_h", anchor, anchor, new Vector2(dirX > 0 ? 0f : 1f, pivot.y), Vector2.zero, new Vector2(len, thick));
            h.rectTransform.anchoredPosition = Vector2.zero;
            var v = Bar(name + "_v", anchor, anchor, new Vector2(pivot.x, dirY > 0 ? 0f : 1f), Vector2.zero, new Vector2(thick, len));
            v.rectTransform.anchoredPosition = Vector2.zero;
        }

        Image Bar(string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            parts.Add(img);
            return img;
        }

        void Update()
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (group.alpha <= 0.001f) return;
            // Restrained pulse: brightness only, never a blink or strobe.
            float pulse = 0.78f + 0.22f * Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);
            var c = new Color(color.r, color.g, color.b, color.a * pulse);
            for (int i = 0; i < parts.Count; i++)
                if (parts[i] != null) parts[i].color = c;
        }
    }
}
