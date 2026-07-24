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

        public static Font DefaultFont
        {
            get
            {
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
