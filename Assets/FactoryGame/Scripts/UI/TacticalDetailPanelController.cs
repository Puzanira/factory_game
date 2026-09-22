using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;

namespace LastShift.UI
{
    /// <summary>
    /// The two permanent readouts of the factory terminal, each in its own corner
    /// (founder, 2026-09-22):
    ///   «РЕСУРС УПРАВЛЕНИЯ» — bottom of the terminal column, where it used to live
    ///   «РЕШИМОСТЬ ИНЖЕНЕРА» — top right of the play area
    ///
    /// They spent one pass together as a thin strip along the top of the play area,
    /// after the lower-left detail panel that had held them was killed by the
    /// live-cabinet playtest. The strip put the player's own resource and the
    /// target's resolve side by side, as if they were the same kind of thing; split
    /// across two corners, each reads as what it is — yours down by your commands,
    /// his up by the room.
    /// </summary>
    public class TacticalDetailPanelController
    {
        Text resourceLabel;
        RectTransform resolveFill;
        Text resolveLabel;
        readonly List<Image> resourceCells = new List<Image>();
        float resourceFlashUntil;


        /// <summary>
        /// Builds both readouts. <paramref name="canvas"/> is the game canvas (the
        /// resolve bar sits in its top-right corner); <paramref name="terminalScreen"/>
        /// is the CRT screen of the command terminal, whose bottom the control
        /// resource takes back — the command list stops above it.
        /// </summary>
        public void Build(Transform canvas, Transform terminalScreen)
        {
            BuildResolve(canvas);
            BuildResource(terminalScreen);
        }

        /// <summary>Top-right of the play area: his resolve, over his room.</summary>
        void BuildResolve(Transform canvas)
        {
            RectTransform panel = UIBuilder.PanelPx(canvas, "ResolveReadout",
                new Vector2(0.60f, 0.905f), new Vector2(0.995f, 0.99f),
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.04f, 0.025f, 0.88f));
            panel.GetComponent<Image>().raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            resolveLabel = UIBuilder.Label(panel, "ResolveLabel", Loc.EngineerResolve, 19,
                new Color(1f, 0.72f, 0.42f), TextAnchor.MiddleLeft);
            SetRect(resolveLabel.rectTransform, new Vector2(0.03f, 0.12f), new Vector2(0.52f, 0.88f));
            resolveFill = UIBuilder.Bar(panel, "ResolveBar", new Vector2(0.54f, 0.28f), new Vector2(0.97f, 0.72f),
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.25f));
        }

        /// <summary>Bottom of the terminal column: your resource, under your commands.</summary>
        void BuildResource(Transform terminalScreen)
        {
            RectTransform block = UIBuilder.Panel(terminalScreen, "ResourceReadout",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.155f), new Color(0f, 0f, 0f, 0f));
            block.GetComponent<Image>().raycastTarget = false;

            UIBuilder.Panel(block, "TopLine", new Vector2(0.02f, 0.965f), new Vector2(0.98f, 0.98f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));

            resourceLabel = UIBuilder.Label(block, "ResourceLabel", Loc.ControlResource, 19,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleCenter);
            SetRect(resourceLabel.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.93f));

            // Three cells, centred under the label.
            int maxCells = TacticsData.Get().resourceMax;
            const float cellW = 0.105f, gap = 0.04f;
            float total = maxCells * cellW + (maxCells - 1) * gap;
            float x = (1f - total) * 0.5f;
            for (int i = 0; i < maxCells; i++)
            {
                var cellGO = new GameObject("Cell" + i);
                cellGO.transform.SetParent(block, false);
                var cellRt = cellGO.AddComponent<RectTransform>();
                float x0 = x + i * (cellW + gap);
                cellRt.anchorMin = new Vector2(x0, 0.14f);
                cellRt.anchorMax = new Vector2(x0 + cellW, 0.44f);
                cellRt.offsetMin = Vector2.zero;
                cellRt.offsetMax = Vector2.zero;
                var img = cellGO.AddComponent<Image>();
                img.color = new Color(0.4f, 1f, 0.55f, 0.9f);
                img.raycastTarget = false;
                var cellOutline = cellGO.AddComponent<Outline>();
                cellOutline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.7f);
                cellOutline.effectDistance = new Vector2(1f, 1f);
                resourceCells.Add(img);
            }
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------------- refresh ----------------

        public void Refresh(LastShift.Engineer.EngineerController engineer)
        {
            RefreshResourceCells();
            RefreshResolve(engineer);
        }

        void RefreshResolve(LastShift.Engineer.EngineerController engineer)
        {
            if (resolveFill == null) return;
            if (engineer == null || engineer.Stats == null)
            {
                UIBuilder.SetBar(resolveFill, 0f);
                return;
            }
            float max = engineer.Data != null ? engineer.Data.resolveMax : 100f;
            UIBuilder.SetBar(resolveFill, engineer.Stats.Resolve / Mathf.Max(1f, max));
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
                if (fill >= 1f) c = new Color(0.4f, 1f, 0.55f, 0.95f);                     // full cell
                else if (fill > 0f) c = new Color(0.4f, 0.85f, 0.5f, 0.2f + 0.5f * fill);  // charging
                else c = new Color(0.15f, 0.3f, 0.2f, 0.55f);                              // empty
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

        /// <summary>Emergency tint for the labels (mirrors the terminal's alarm state).</summary>
        public void SetEmergency(bool emergency)
        {
            if (resourceLabel != null)
                resourceLabel.color = emergency ? new Color(1f, 0.6f, 0.45f) : new Color(0.6f, 0.9f, 0.68f);
            if (resolveLabel != null)
                resolveLabel.color = emergency ? new Color(1f, 0.55f, 0.4f) : new Color(1f, 0.72f, 0.42f);
        }
    }
}
