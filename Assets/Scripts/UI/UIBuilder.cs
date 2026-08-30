using UnityEngine;
using UnityEngine.UI;

namespace LastShift.UI
{
    /// <summary>
    /// Code-built uGUI helpers. All game UI is constructed at runtime from these,
    /// so no scene/prefab serialization can ever hold a broken reference.
    /// </summary>
    public static class UIBuilder
    {
        static Font cachedFont;

        /// <summary>
        /// The project font, shipped in Resources rather than taken from Unity's
        /// built-in LegacyRuntime.ttf.
        ///
        /// Why: a dynamic font only rasterises glyphs it actually contains, and on
        /// desktop Unity quietly fills the gaps from the operating system's fonts.
        /// WebGL has no OS to ask, so every Cyrillic character in the game came out
        /// blank in the browser build. Inter ships with the editor under the SIL Open
        /// Font License (license copied next to the .ttf) and was checked to cover all
        /// 108 distinct characters the game's strings use — Cyrillic plus « » — № ↑ ↓
        /// and the true minus sign. RobotoMono was rejected: it has no ↑ or ↓, which
        /// the menu hints need.
        ///
        /// The built-in font stays as a last-resort fallback so UI can never come up
        /// with no font at all.
        /// </summary>
        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                    cachedFont = Resources.Load<Font>("Fonts/Inter-Regular");
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// Sets the rasterisation density of a WORLD-SPACE canvas so its dynamic-font
        /// text is baked at the size it is actually drawn at.
        ///
        /// A dynamic font bakes each glyph into an atlas at fontSize * canvas.scaleFactor
        /// pixels. A world canvas is scaled down to a fraction of a world unit, and the
        /// camera then blows a world unit up to ~77 screen px at 1080p — so with the
        /// default scaleFactor of 1 the glyph bitmap ends up magnified and goes blocky
        /// (the engineer's state label was baked at 22 px and drawn at 34).
        /// CanvasScaler.dynamicPixelsPerUnit becomes that scaleFactor for a world canvas,
        /// and uGUI divides the generated glyph geometry back by it, so the text keeps
        /// its size and only gains resolution.
        ///
        /// The density is MATCHED, not overshot: the font atlas carries no mipmaps, so a
        /// wildly oversized glyph would alias when minified. It is read from the camera's
        /// current pixel height, which is why this is called after the canvas is placed;
        /// rooms rebuild their UI on load, so a resolution change is picked up then.
        /// </summary>
        public static void ApplyWorldCanvasDensity(Canvas canvas, float canvasLocalScale)
        {
            if (canvas == null || canvasLocalScale <= 0f) return;

            // Orthographic height in world units -> screen pixels per world unit.
            float pxPerWorldUnit = 100f; // fallback if no camera has been set up yet
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic && cam.orthographicSize > 0.001f)
                pxPerWorldUnit = cam.pixelHeight / (2f * cam.orthographicSize);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            // Clamped: never coarser than 1:1, and capped so a freak camera cannot ask
            // for glyphs big enough to blow up the font atlas.
            scaler.dynamicPixelsPerUnit = Mathf.Clamp(canvasLocalScale * pxPerWorldUnit, 1f, 8f);
        }

        public static RectTransform Panel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Panel with pixel offsets relative to its anchors.</summary>
        public static RectTransform PanelPx(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rt = Panel(parent, name, anchorMin, anchorMax, color);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize,
            Color color, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>Horizontal stat bar; returns the fill RectTransform (set with SetBar).</summary>
        public static RectTransform Bar(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Color backColor, Color fillColor)
        {
            RectTransform back = PanelPx(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, backColor);
            RectTransform fill = Panel(back, "Fill", Vector2.zero, Vector2.one, fillColor);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            return fill;
        }

        public static void SetBar(RectTransform fill, float value01)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
        }
    }
}
