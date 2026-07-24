using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>Where a callout should sit relative to its target, when it matters.</summary>
    public enum TutorialCalloutSide { Auto, Above, Below, Left, Right }

    /// <summary>
    /// The tutorial's single information panel: an opaque near-black terminal card
    /// with a thin green border, amber corner brackets, amber title, pale-green body,
    /// a status line and a footer prompt, plus a thin technical connector line to the
    /// highlighted target. Squared, industrial — no speech bubbles, no cartoon tails.
    /// Placement picks the free side of the target and never covers it.
    /// </summary>
    public class TutorialCalloutPanel : MonoBehaviour
    {
        Canvas canvas;
        RectTransform panel;
        CanvasGroup group;
        Text headerText;
        Text bodyText;
        Text stepText;
        Text statusText;
        Text footerText;
        RectTransform connector;
        Image connectorImg;

        bool visible;
        bool lastHadTarget;
        Rect lastTargetRect;
        Vector2 placedWithSize;   // panel screen size the last placement was computed for
        TutorialCalloutSide preferredSide = TutorialCalloutSide.Auto;
        Vector2 lastPlacedFor = new Vector2(float.MinValue, float.MinValue);

        static readonly Color Fill = new Color(0.012f, 0.035f, 0.026f, 1f);
        static readonly Color Border = new Color(0.34f, 0.7f, 0.45f, 0.9f);
        static readonly Color Amber = new Color(0.97f, 0.82f, 0.4f);
        static readonly Color Body = new Color(0.74f, 0.98f, 0.8f);
        static readonly Color Dim = new Color(0.45f, 0.68f, 0.52f);

        const float PanelWidth = 560f;   // canvas units (1920x1080 reference)
        const float PanelHeight = 300f;

        public void Init(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            transform.SetParent(canvas.transform, false);
            var root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            // Connector first so it renders behind the opaque card.
            var connGo = new GameObject("Connector");
            connGo.transform.SetParent(transform, false);
            connector = connGo.AddComponent<RectTransform>();
            connector.anchorMin = new Vector2(0.5f, 0.5f);
            connector.anchorMax = new Vector2(0.5f, 0.5f);
            connector.pivot = new Vector2(0f, 0.5f);
            connectorImg = connGo.AddComponent<Image>();
            connectorImg.color = new Color(0.97f, 0.82f, 0.4f, 0.55f);
            connectorImg.raycastTarget = false;

            panel = UIBuilder.Panel(transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Fill);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Frame(panel, Border, 2f);
            Brackets(panel, Amber);

            var scan = UIBuilder.Panel(panel, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.18f);
            scanImg.raycastTarget = false;

            headerText = UIBuilder.Label(panel, "Header", "", 26, Amber, TextAnchor.MiddleLeft);
            SetRect(headerText.rectTransform, new Vector2(0.05f, 0.845f), new Vector2(0.72f, 0.95f));
            stepText = UIBuilder.Label(panel, "Step", "", 15, Dim, TextAnchor.MiddleRight);
            SetRect(stepText.rectTransform, new Vector2(0.7f, 0.85f), new Vector2(0.95f, 0.95f));
            UIBuilder.Panel(panel, "HeaderLine", new Vector2(0.05f, 0.833f), new Vector2(0.95f, 0.838f),
                new Color(0.34f, 0.7f, 0.45f, 0.55f));

            bodyText = UIBuilder.Label(panel, "Body", "", 19, Body, TextAnchor.UpperLeft);
            SetRect(bodyText.rectTransform, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.81f));

            statusText = UIBuilder.Label(panel, "Status", "", 17, Amber, TextAnchor.MiddleLeft);
            SetRect(statusText.rectTransform, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.26f));

            UIBuilder.Panel(panel, "FooterLine", new Vector2(0.05f, 0.135f), new Vector2(0.95f, 0.14f),
                new Color(0.34f, 0.7f, 0.45f, 0.45f));
            footerText = UIBuilder.Label(panel, "Footer", Loc.TutorialFooterAck, 17, Amber, TextAnchor.MiddleCenter);
            SetRect(footerText.rectTransform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.13f));
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Frame(Transform parent, Color color, float thickness)
        {
            Edge(parent, "EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), color);
            Edge(parent, "EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), color);
        }

        static void Brackets(Transform parent, Color color)
        {
            const float len = 22f, t = 3f;
            Edge(parent, "BrBL_h", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBL_v", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrBR_h", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBR_v", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrTL_h", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTL_v", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(t, len), color);
            Edge(parent, "BrTR_h", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTR_v", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(t, len), color);
        }

        static void Edge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ---------------- content ----------------

        public void Show(string header, string body, string footer, string stepLabel,
                         Rect targetScreenRect, bool hasTarget,
                         TutorialCalloutSide side = TutorialCalloutSide.Auto)
        {
            preferredSide = side;
            headerText.text = header ?? "";
            bodyText.text = body ?? "";
            footerText.text = footer ?? "";
            stepText.text = stepLabel ?? "";
            statusText.text = "";
            visible = true;
            lastPlacedFor = new Vector2(float.MinValue, float.MinValue);
            Place(targetScreenRect, hasTarget);
        }

        public void SetFooter(string footer) => footerText.text = footer ?? "";

        public void SetStatus(string text, bool warning)
        {
            if (statusText == null) return;
            statusText.text = text ?? "";
            statusText.color = warning ? new Color(1f, 0.55f, 0.4f) : Amber;
        }

        public void Hide() => visible = false;

        public bool Visible => visible;

        /// <summary>Screen-pixel rect of the card (used by the headless layout checks).</summary>
        public Rect ScreenRect => panel != null ? TutorialUiSpace.ScreenRectOf(panel) : new Rect();

        /// <summary>Reposition against a moving target (only when it actually moved).</summary>
        public void Follow(Rect targetScreenRect, bool hasTarget)
        {
            if (!visible) return;
            Vector2 c = targetScreenRect.center;
            if (hasTarget == lastHadTarget && (c - lastPlacedFor).sqrMagnitude < 40f * 40f) return;
            Place(targetScreenRect, hasTarget);
        }

        // ---------------- placement ----------------

        /// <summary>
        /// Real on-screen size of the card. The canvas scale factor is not yet valid
        /// on the frame the canvas is created, so placement must not trust it — a
        /// card that renders larger than assumed would sit on top of its own target.
        /// </summary>
        Vector2 MeasuredScreenSize()
        {
            if (panel != null)
            {
                Rect r = TutorialUiSpace.ScreenRectOf(panel);
                if (r.width > 1f && r.height > 1f) return r.size;
            }
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            return new Vector2(PanelWidth * scale, PanelHeight * scale);
        }

        void Place(Rect target, bool hasTarget)
        {
            lastHadTarget = hasTarget;
            lastTargetRect = target;
            lastPlacedFor = target.center;

            Vector2 size = MeasuredScreenSize();
            placedWithSize = size;
            float w = size.x;
            float h = size.y;
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;

            if (!hasTarget)
            {
                TutorialUiSpace.ApplyPosition(panel, new Vector2(sw * 0.5f, sh * 0.5f));
                connectorImg.color = new Color(0.97f, 0.82f, 0.4f, 0f);
                return;
            }

            const float gap = 40f;
            const float pad = 26f;
            // The terminal column owns the left 28% of the screen; keep the card off
            // it unless the target itself lives there.
            float terminalEdge = sw * 0.285f;
            bool targetInTerminal = target.center.x < terminalEdge;

            var candidates = new[]
            {
                new Rect(target.xMax + gap, target.center.y - h * 0.5f, w, h),          // right
                new Rect(target.xMin - gap - w, target.center.y - h * 0.5f, w, h),      // left
                new Rect(target.center.x - w * 0.5f, target.yMin - gap - h, w, h),      // below
                new Rect(target.center.x - w * 0.5f, target.yMax + gap, w, h),          // above
            };

            // candidates[] order: right, left, below, above.
            int preferred = -1;
            switch (preferredSide)
            {
                case TutorialCalloutSide.Right: preferred = 0; break;
                case TutorialCalloutSide.Left: preferred = 1; break;
                case TutorialCalloutSide.Below: preferred = 2; break;
                case TutorialCalloutSide.Above: preferred = 3; break;
            }

            Rect best = candidates[0];
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Length; i++)
            {
                Rect r = TutorialUiSpace.ClampToScreen(candidates[i], pad);
                float score = 0f;
                if (Overlaps(r, TutorialUiSpace.Expand(target, 12f))) score -= 1000f;   // never cover the target
                if (!targetInTerminal && r.xMin < terminalEdge) score -= 300f;          // keep the command list clear
                // Prefer the candidate that moved least while being clamped.
                score -= (r.center - candidates[i].center).magnitude * 0.5f;
                score -= i * 2f; // stable ordering preference
                // An explicitly requested side wins unless it would cover the target.
                if (i == preferred) score += 500f;
                if (score > bestScore) { bestScore = score; best = r; }
            }

            TutorialUiSpace.ApplyPosition(panel, best.center);
            DrawConnector(best, target, canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f);
        }

        static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

        /// <summary>Thin technical line from the card edge to the target edge.</summary>
        void DrawConnector(Rect card, Rect target, float scale)
        {
            Vector2 from = ClosestEdgePoint(card, target.center);
            Vector2 to = ClosestEdgePoint(TutorialUiSpace.Expand(target, 10f), card.center);
            Vector2 delta = to - from;
            float len = delta.magnitude;
            if (len < 6f)
            {
                connectorImg.color = new Color(0.97f, 0.82f, 0.4f, 0f);
                return;
            }
            connectorImg.color = new Color(0.97f, 0.82f, 0.4f, 0.5f);
            connector.sizeDelta = new Vector2(len / scale, 2f);
            connector.position = new Vector3(from.x, from.y, 0f);
            connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static Vector2 ClosestEdgePoint(Rect r, Vector2 toward)
        {
            Vector2 c = r.center;
            Vector2 d = toward - c;
            if (d.sqrMagnitude < 0.001f) return c;
            float hx = r.width * 0.5f, hy = r.height * 0.5f;
            float sx = Mathf.Abs(d.x) > 0.0001f ? hx / Mathf.Abs(d.x) : float.MaxValue;
            float sy = Mathf.Abs(d.y) > 0.0001f ? hy / Mathf.Abs(d.y) : float.MaxValue;
            return c + d * Mathf.Min(sx, sy);
        }

        void Update()
        {
            if (group == null) return;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (!visible || panel == null) return;

            // Re-place as soon as the card's real size is known (canvas scale settles a
            // frame after creation) or when it changes — otherwise a card placed with a
            // stale scale factor can end up covering the very thing it explains.
            Vector2 measured = MeasuredScreenSize();
            if (Mathf.Abs(measured.x - placedWithSize.x) > 2f || Mathf.Abs(measured.y - placedWithSize.y) > 2f)
                Place(lastTargetRect, lastHadTarget);
        }
    }
}
