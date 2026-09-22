using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Machines;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// The diegetic industrial CRT terminal panel on the left edge of the screen:
    /// metal frame with corner bolts, phosphor-green screen, scanlines, status LEDs.
    /// Pure view: FactoryCommandTerminal owns selection/activation logic.
    /// </summary>
    public class CommandTerminalUI : MonoBehaviour
    {
        FactoryCommandTerminal terminal;
        Core.LevelManager lm;

        Image screenBg;
        Text header;
        Text statusLine;
        Image powerLed;
        RectTransform rowsRt;      // viewport (clips the list)
        RectTransform rowsContent; // scrolled content
        Text scrollUpMark;
        Text scrollDownMark;
        float rowHeight;
        float scrollOffset;
        readonly List<CommandListItemUI> rows = new List<CommandListItemUI>();

        readonly TacticalDetailPanelController detail = new TacticalDetailPanelController();

        /// <summary>Command list container (tutorial step «СИСТЕМЫ ЗАВОДА»).</summary>
        public RectTransform CommandListRect => rowsRt;
        /// <summary>Top status strip and its two gauges (tutorial steps 4-5).</summary>
        public TacticalDetailPanelController Detail => detail;

        /// <summary>
        /// Smoke-test check: the selected command must always be fully inside the
        /// list viewport — a room with many systems must never hide its selection.
        /// </summary>
        public bool DevSelectedRowVisible
        {
            get
            {
                if (terminal == null || rowsRt == null) return true;
                int sel = terminal.SelectedIndex;
                if (sel < 0 || sel >= rows.Count || rows[sel] == null) return true;
                Rect view = TutorialUiSpace.ScreenRectOf(rowsRt);
                if (view.height < 1f) return true;
                Rect row = TutorialUiSpace.ScreenRectOf((RectTransform)rows[sel].transform);
                return row.yMin >= view.yMin - 1f && row.yMax <= view.yMax + 1f;
            }
        }

        static readonly Color ScreenGreen = new Color(0.02f, 0.07f, 0.04f, 1f);
        static readonly Color ScreenRed = new Color(0.11f, 0.025f, 0.02f, 1f);
        static readonly Color PhosphorText = new Color(0.62f, 1f, 0.72f);

        public void Bind(FactoryCommandTerminal commandTerminal, Core.LevelManager levelManager, Canvas canvas)
        {
            terminal = commandTerminal;
            lm = levelManager;
            Build(canvas.transform);
        }

        void Build(Transform root)
        {
            // Outer metal frame.
            RectTransform frame = UIBuilder.Panel(root, "TerminalFrame",
                Vector2.zero, new Vector2(0.28f, 1f), new Color(0.16f, 0.17f, 0.19f));
            AddBolt(frame, new Vector2(0.04f, 0.985f));
            AddBolt(frame, new Vector2(0.96f, 0.985f));
            AddBolt(frame, new Vector2(0.04f, 0.015f));
            AddBolt(frame, new Vector2(0.96f, 0.015f));

            // Inner CRT screen.
            RectTransform screen = UIBuilder.PanelPx(frame, "Screen",
                Vector2.zero, Vector2.one, new Vector2(10f, 12f), new Vector2(-10f, -12f), ScreenGreen);
            screenBg = screen.GetComponent<Image>();

            // Scanline overlay across the whole screen.
            var scan = UIBuilder.Panel(screen, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.color = new Color(1f, 1f, 1f, 0.5f);
            scanImg.raycastTarget = false;
            scanImg.pixelsPerUnitMultiplier = 0.35f;

            // Header with power LED and a thin separator.
            header = UIBuilder.Label(screen, "Header", Loc.FactorySystems, 26, PhosphorText, TextAnchor.MiddleCenter);
            SetRect(header.rectTransform, new Vector2(0f, 0.935f), new Vector2(1f, 1f));
            var ledGO = new GameObject("PowerLed");
            ledGO.transform.SetParent(screen, false);
            var ledRt = ledGO.AddComponent<RectTransform>();
            ledRt.anchorMin = new Vector2(0.055f, 0.955f);
            ledRt.anchorMax = new Vector2(0.055f, 0.955f);
            ledRt.sizeDelta = new Vector2(14f, 14f);
            powerLed = ledGO.AddComponent<Image>();
            powerLed.sprite = TextureFactory.SoftCircle();
            powerLed.color = new Color(0.4f, 1f, 0.5f);
            UIBuilder.Panel(screen, "HeaderLine", new Vector2(0.04f, 0.932f), new Vector2(0.96f, 0.9345f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));

            statusLine = UIBuilder.Label(screen, "Status", "", 16,
                new Color(0.95f, 0.75f, 0.35f), TextAnchor.MiddleCenter);
            SetRect(statusLine.rectTransform, new Vector2(0f, 0.895f), new Vector2(1f, 0.93f));

            // Command list fills the whole panel below the header: the lower-left
            // detail panel is gone (live-cabinet playtest), so the list takes back
            // the bottom third instead of leaving a black hole there. It is a
            // clipped viewport: rows shrink to fit, and if there are too many for a
            // readable height the list scrolls with the selection instead of
            // running off the panel.
            const float ListBottom = 0.05f, ListTop = 0.888f;
            var rowsGO = new GameObject("Rows");
            rowsGO.transform.SetParent(screen, false);
            rowsRt = rowsGO.AddComponent<RectTransform>();
            SetRect(rowsRt, new Vector2(0.02f, ListBottom), new Vector2(0.98f, ListTop));
            rowsGO.AddComponent<RectMask2D>();

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(rowsRt, false);
            rowsContent = contentGO.AddComponent<RectTransform>();
            rowsContent.anchorMin = new Vector2(0f, 1f);
            rowsContent.anchorMax = new Vector2(1f, 1f);
            rowsContent.pivot = new Vector2(0.5f, 1f);
            rowsContent.offsetMin = new Vector2(0f, rowsContent.offsetMin.y);
            rowsContent.offsetMax = new Vector2(0f, rowsContent.offsetMax.y);

            var items = terminal.Items;
            // Reference-resolution height of the viewport (canvas is 1920x1080-based).
            float viewportHeight = (1080f - 24f) * (ListTop - ListBottom);
            rowHeight = Mathf.Clamp(viewportHeight / Mathf.Max(1, items.Count), 42f, 66f);
            rowsContent.sizeDelta = new Vector2(0f, rowHeight * items.Count);
            for (int i = 0; i < items.Count; i++)
                rows.Add(CommandListItemUI.Create(rowsContent, i, rowHeight));

            // Terminal-style "more above / more below" marks (no mouse scrollbar).
            scrollUpMark = UIBuilder.Label(screen, "ScrollUp", "▲", 14,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleCenter);
            SetRect(scrollUpMark.rectTransform, new Vector2(0.86f, ListTop), new Vector2(0.98f, ListTop + 0.028f));
            scrollDownMark = UIBuilder.Label(screen, "ScrollDown", "▼", 14,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleCenter);
            SetRect(scrollDownMark.rectTransform, new Vector2(0.86f, ListBottom - 0.028f), new Vector2(0.98f, ListBottom));
            scrollUpMark.enabled = false;
            scrollDownMark.enabled = false;

            // The two permanent gauges live on the game canvas, not in this panel:
            // a thin strip along the top row of the play area, clear of the
            // terminal frame on the left.
            detail.Build(root, new Vector2(0.29f, 0.905f), new Vector2(0.995f, 0.99f));
        }

        void AddBolt(RectTransform frame, Vector2 anchor)
        {
            var go = new GameObject("Bolt");
            go.transform.SetParent(frame, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(10f, 10f);
            var img = go.AddComponent<Image>();
            img.sprite = TextureFactory.SoftCircle();
            img.color = new Color(0.45f, 0.47f, 0.5f);
            img.raycastTarget = false;
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (terminal == null) return;
            bool emergency = lm != null && lm.Escalated;

            // Red accents in emergency, screen stays readable.
            screenBg.color = emergency ? ScreenRed : ScreenGreen;
            header.text = emergency ? Loc.EmergencyOverride : Loc.FactorySystems;
            header.color = emergency ? new Color(1f, 0.5f, 0.4f) : PhosphorText;
            if (powerLed != null)
            {
                float pulse = 0.6f + 0.4f * Mathf.PingPong(Time.unscaledTime * (emergency ? 6f : 1.5f), 1f);
                powerLed.color = emergency
                    ? new Color(1f, 0.35f, 0.25f, pulse)
                    : new Color(0.4f, 1f, 0.5f, pulse);
            }

            if (!terminal.AnyReady)
            {
                float blink = Mathf.PingPong(Time.unscaledTime * 2f, 1f);
                statusLine.text = blink > 0.3f ? Loc.AllSystemsCooldown : "";
            }
            else statusLine.text = "";

            var items = terminal.Items;
            for (int i = 0; i < rows.Count && i < items.Count; i++)
                rows[i].Refresh(items[i], i == terminal.SelectedIndex, emergency);

            detail.SetEmergency(emergency);
            detail.Refresh(lm != null ? lm.Engineer : null);

            UpdateScroll(items.Count);
        }

        /// <summary>Keeps the selected command visible when the list is longer than the panel.</summary>
        void UpdateScroll(int count)
        {
            if (rowsContent == null || rowsRt == null) return;
            float viewH = rowsRt.rect.height;
            if (viewH < 1f) return; // layout has not run yet

            // Re-fit to the panel's real height (it varies with aspect ratio), so the
            // list only scrolls when the rows would otherwise become unreadable.
            float ideal = Mathf.Clamp(viewH / Mathf.Max(1, count), 42f, 66f);
            if (Mathf.Abs(ideal - rowHeight) > 0.5f)
            {
                rowHeight = ideal;
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i] != null) rows[i].SetRowHeight(i, rowHeight);
                rowsContent.sizeDelta = new Vector2(0f, rowHeight * count);
            }

            float contentH = rowHeight * count;
            float maxScroll = Mathf.Max(0f, contentH - viewH);

            if (maxScroll <= 0.5f)
            {
                scrollOffset = 0f;
            }
            else
            {
                int sel = Mathf.Clamp(terminal.SelectedIndex, 0, Mathf.Max(0, count - 1));
                // Scroll only as far as needed to bring the selected row into view.
                scrollOffset = Mathf.Clamp(scrollOffset, (sel + 1) * rowHeight - viewH, sel * rowHeight);
                scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
            }

            rowsContent.anchoredPosition = new Vector2(0f, scrollOffset);
            if (scrollUpMark != null) scrollUpMark.enabled = scrollOffset > 0.5f;
            if (scrollDownMark != null) scrollDownMark.enabled = scrollOffset < maxScroll - 0.5f;
        }

        /// <summary>Insufficient-resource feedback: brief red blink on the empty cells.</summary>
        public void FlashResource() => detail.FlashResource();
    }
}
