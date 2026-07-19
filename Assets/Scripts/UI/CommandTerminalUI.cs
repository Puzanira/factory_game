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
        Text descriptionText;
        Text zoneStatusText;
        Text enterHint;
        Image powerLed;
        Text resourceLabel;
        readonly List<Image> resourceCells = new List<Image>();
        float resourceFlashUntil;
        readonly List<CommandListItemUI> rows = new List<CommandListItemUI>();

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

            // «РЕСУРС УПРАВЛЕНИЯ» — three industrial power cells.
            var resRow = UIBuilder.Panel(screen, "ResourceRow",
                new Vector2(0.03f, 0.852f), new Vector2(0.97f, 0.893f), new Color(0.01f, 0.05f, 0.03f, 0.85f));
            resourceLabel = UIBuilder.Label(resRow, "ResourceLabel", Loc.ControlResource, 14,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleLeft);
            SetRect(resourceLabel.rectTransform, new Vector2(0.03f, 0f), new Vector2(0.62f, 1f));
            int maxCells = Data.TacticsData.Get().resourceMax;
            for (int i = 0; i < maxCells; i++)
            {
                var cellGO = new GameObject("Cell" + i);
                cellGO.transform.SetParent(resRow, false);
                var cellRt = cellGO.AddComponent<RectTransform>();
                float x0 = 0.66f + i * 0.11f;
                cellRt.anchorMin = new Vector2(x0, 0.22f);
                cellRt.anchorMax = new Vector2(x0 + 0.085f, 0.78f);
                cellRt.offsetMin = Vector2.zero;
                cellRt.offsetMax = Vector2.zero;
                var img = cellGO.AddComponent<Image>();
                img.color = new Color(0.4f, 1f, 0.55f, 0.9f);
                img.raycastTarget = false;
                var outline = cellGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.7f);
                outline.effectDistance = new Vector2(1f, 1f);
                resourceCells.Add(img);
            }

            // Rows container fills the middle of the panel.
            var rowsGO = new GameObject("Rows");
            rowsGO.transform.SetParent(screen, false);
            var rowsRt = rowsGO.AddComponent<RectTransform>();
            SetRect(rowsRt, new Vector2(0.02f, 0.24f), new Vector2(0.98f, 0.848f));

            var items = terminal.Items;
            float rowHeight = Mathf.Min(72f, 700f / Mathf.Max(1, items.Count));
            for (int i = 0; i < items.Count; i++)
                rows.Add(CommandListItemUI.Create(rowsRt, i, rowHeight));

            // Description box at the bottom, framed like a readout module.
            RectTransform descBox = UIBuilder.PanelPx(screen, "DescriptionBox",
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.22f),
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.04f, 0.025f, 0.95f));
            var descOutline = descBox.gameObject.AddComponent<Outline>();
            descOutline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.45f);
            descOutline.effectDistance = new Vector2(1.5f, 1.5f);
            enterHint = UIBuilder.Label(descBox, "EnterHint", Loc.EnterExecute, 17,
                new Color(0.95f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            SetRect(enterHint.rectTransform, new Vector2(0.04f, 0.7f), new Vector2(0.96f, 0.98f));
            descriptionText = UIBuilder.Label(descBox, "Description", "", 15,
                new Color(0.7f, 0.9f, 0.75f), TextAnchor.UpperLeft);
            SetRect(descriptionText.rectTransform, new Vector2(0.04f, 0.3f), new Vector2(0.96f, 0.7f));
            zoneStatusText = UIBuilder.Label(descBox, "ZoneStatus", "", 14,
                new Color(0.5f, 0.95f, 0.6f), TextAnchor.UpperLeft);
            SetRect(zoneStatusText.rectTransform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.3f));
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

            var selected = terminal.Selected;
            if (selected != null)
            {
                enterHint.text = selected.IsReady
                    ? Loc.EnterExecute + selected.CommandLabel.ToUpper()
                    : Loc.SystemBusy + " — " + selected.displayName.ToUpper();

                // Purpose: one main line + one tactical hint.
                string purpose = selected.PurposeLine;
                if (!string.IsNullOrEmpty(selected.PurposeHint))
                    purpose += "\n" + selected.PurposeHint;
                descriptionText.text = string.IsNullOrEmpty(purpose) ? selected.Description : purpose;

                // Zone status: teaches that timing matters (recovery moves stay neutral).
                if (selected.ActivationAlwaysEffective)
                {
                    zoneStatusText.text = "";
                }
                else if (selected.EngineerInEffectiveZone)
                {
                    zoneStatusText.text = selected is DoorMachine ? Loc.InsightDoorBlock : Loc.TargetInZone;
                    zoneStatusText.color = new Color(0.5f, 0.95f, 0.6f);
                }
                else
                {
                    zoneStatusText.text = Loc.TargetOutOfZone;
                    zoneStatusText.color = new Color(0.85f, 0.7f, 0.4f);
                }
            }
            else
            {
                enterHint.text = Loc.NoSystemsOnline;
                descriptionText.text = "";
                zoneStatusText.text = "";
            }

            RefreshResourceCells();
        }

        void RefreshResourceCells()
        {
            var res = Core.FactoryControlResource.Instance;
            if (res == null || resourceCells.Count == 0) return;
            bool flashing = Time.unscaledTime < resourceFlashUntil;
            for (int i = 0; i < resourceCells.Count; i++)
            {
                float fill = Mathf.Clamp01(res.Charges - i);
                Color c;
                if (fill >= 1f) c = new Color(0.4f, 1f, 0.55f, 0.95f);          // full cell
                else if (fill > 0f) c = new Color(0.4f, 0.85f, 0.5f, 0.2f + 0.5f * fill); // charging
                else c = new Color(0.15f, 0.3f, 0.2f, 0.55f);                    // empty
                if (flashing && fill < 1f)
                {
                    float blink = Mathf.PingPong(Time.unscaledTime * 8f, 1f);
                    c = Color.Lerp(c, new Color(1f, 0.35f, 0.25f, 0.9f), blink);
                }
                resourceCells[i].color = c;
            }
        }

        /// <summary>Insufficient-resource feedback: brief red blink on the empty cells.</summary>
        public void FlashResource() => resourceFlashUntil = Time.unscaledTime + 1.2f;
    }
}
