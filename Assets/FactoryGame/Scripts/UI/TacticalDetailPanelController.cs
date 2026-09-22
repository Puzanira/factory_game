using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;

namespace LastShift.UI
{
    /// <summary>
    /// The two permanent readouts of the factory terminal — «РЕШИМОСТЬ ИНЖЕНЕРА»
    /// and «РЕСУРС УПРАВЛЕНИЯ» — laid out as one thin strip along the very top of
    /// the play area, left to right: resolve bar, then the control-resource cells.
    ///
    /// They used to sit in a lower-left detail panel together with the selected
    /// system's name, purpose and zone status. The live-cabinet playtest (founder,
    /// 2026-09) killed that panel: at the machine it read as noise in the corner of
    /// the eye and nobody looked at it. The two gauges survived the panel and moved
    /// up here, where they are in the player's line of sight; the descriptive rows
    /// went away with it.
    ///
    /// Pure view; built in code like the rest of the UI. Exposes its sub-rects so
    /// the tutorial can highlight one gauge at a time.
    /// </summary>
    public class TacticalDetailPanelController
    {
        Text resourceLabel;
        RectTransform resolveFill;
        Text resolveLabel;
        readonly List<Image> resourceCells = new List<Image>();
        float resourceFlashUntil;

        RectTransform panelRect;
        RectTransform resourceRow;
        RectTransform resolveRow;

        /// <summary>The whole top strip.</summary>
        public RectTransform PanelRect => panelRect;
        /// <summary>Control-resource row (tutorial step «РЕСУРС УПРАВЛЕНИЯ»).</summary>
        public RectTransform ResourceRect => resourceRow;
        /// <summary>Engineer-resolve row (tutorial step «РЕШИМОСТЬ ИНЖЕНЕРА»).</summary>
        public RectTransform ResolveRect => resolveRow;

        /// <summary>
        /// Builds the strip on the game canvas (anchors are screen-relative). It is
        /// kept clear of the terminal frame on the left and of the room title, which
        /// now flashes just below it.
        /// </summary>
        public void Build(Transform canvas, Vector2 anchorMin, Vector2 anchorMax)
        {
            panelRect = UIBuilder.PanelPx(canvas, "TopStatusStrip", anchorMin, anchorMax,
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.04f, 0.025f, 0.88f));
            panelRect.GetComponent<Image>().raycastTarget = false;
            var outline = panelRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            // Left half: «РЕШИМОСТЬ ИНЖЕНЕРА» + bar, on one line.
            resolveRow = UIBuilder.Panel(panelRect, "ResolveRow",
                new Vector2(0.015f, 0.12f), new Vector2(0.62f, 0.88f), new Color(0f, 0f, 0f, 0f));
            resolveRow.GetComponent<Image>().raycastTarget = false;
            resolveLabel = UIBuilder.Label(resolveRow, "ResolveLabel", Loc.EngineerResolve, 19,
                new Color(1f, 0.72f, 0.42f), TextAnchor.MiddleLeft);
            SetRect(resolveLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.42f, 1f));
            resolveFill = UIBuilder.Bar(resolveRow, "ResolveBar", new Vector2(0.44f, 0.26f), new Vector2(1f, 0.74f),
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.25f));

            // Right half: «РЕСУРС УПРАВЛЕНИЯ ■ ■ ■».
            resourceRow = UIBuilder.Panel(panelRect, "ResourceRow",
                new Vector2(0.65f, 0.12f), new Vector2(0.985f, 0.88f), new Color(0f, 0f, 0f, 0f));
            resourceRow.GetComponent<Image>().raycastTarget = false;
            resourceLabel = UIBuilder.Label(resourceRow, "ResourceLabel", Loc.ControlResource, 19,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleLeft);
            SetRect(resourceLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.70f, 1f));
            int maxCells = TacticsData.Get().resourceMax;
            for (int i = 0; i < maxCells; i++)
            {
                var cellGO = new GameObject("Cell" + i);
                cellGO.transform.SetParent(resourceRow, false);
                var cellRt = cellGO.AddComponent<RectTransform>();
                float x0 = 0.725f + i * 0.093f;
                cellRt.anchorMin = new Vector2(x0, 0.24f);
                cellRt.anchorMax = new Vector2(x0 + 0.072f, 0.76f);
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
