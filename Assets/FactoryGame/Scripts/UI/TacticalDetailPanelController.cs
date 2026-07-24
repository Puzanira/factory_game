using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Machines;

namespace LastShift.UI
{
    /// <summary>
    /// The lower-left detail panel of the factory terminal — everything the player
    /// needs about the *currently selected* system, in one fixed reading order:
    /// 1) name + activation instruction, 2) purpose, 3) tactical status,
    /// 4) «РЕСУРС УПРАВЛЕНИЯ», 5) «РЕШИМОСТЬ ИНЖЕНЕРА».
    /// The last two moved here from the removed top HUD strip. Pure view; built in
    /// code like the rest of the UI. Exposes its sub-rects so the tutorial can
    /// highlight one row at a time.
    /// </summary>
    public class TacticalDetailPanelController
    {
        Text enterHint;
        Text descriptionText;
        Text zoneStatusText;
        Text resourceLabel;
        RectTransform resolveFill;
        Text resolveLabel;
        readonly List<Image> resourceCells = new List<Image>();
        float resourceFlashUntil;

        RectTransform panelRect;
        RectTransform resourceRow;
        RectTransform resolveRow;

        /// <summary>Whole detail panel (tutorial step «НАЗНАЧЕНИЕ СИСТЕМЫ»).</summary>
        public RectTransform PanelRect => panelRect;
        /// <summary>Control-resource row (tutorial step «РЕСУРС УПРАВЛЕНИЯ»).</summary>
        public RectTransform ResourceRect => resourceRow;
        /// <summary>Engineer-resolve row (tutorial step «РЕШИМОСТЬ ИНЖЕНЕРА»).</summary>
        public RectTransform ResolveRect => resolveRow;

        static readonly Color Amber = new Color(0.95f, 0.85f, 0.45f);
        static readonly Color Phosphor = new Color(0.7f, 0.9f, 0.75f);
        static readonly Color Separator = new Color(0.32f, 0.62f, 0.42f, 0.4f);

        /// <summary>Builds the panel inside the terminal screen (anchors are screen-relative).</summary>
        public void Build(Transform screen, Vector2 anchorMin, Vector2 anchorMax)
        {
            panelRect = UIBuilder.PanelPx(screen, "DetailPanel", anchorMin, anchorMax,
                Vector2.zero, Vector2.zero, new Color(0.01f, 0.04f, 0.025f, 1f));
            var outline = panelRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            // 1. Selected system + activation instruction.
            enterHint = UIBuilder.Label(panelRect, "EnterHint", Loc.EnterExecute, 17, Amber, TextAnchor.UpperLeft);
            SetRect(enterHint.rectTransform, new Vector2(0.04f, 0.79f), new Vector2(0.96f, 0.985f));

            // 2. What the system does.
            descriptionText = UIBuilder.Label(panelRect, "Description", "", 15, Phosphor, TextAnchor.UpperLeft);
            SetRect(descriptionText.rectTransform, new Vector2(0.04f, 0.575f), new Vector2(0.96f, 0.785f));

            // 3. Tactical status (in zone / out of zone).
            zoneStatusText = UIBuilder.Label(panelRect, "ZoneStatus", "", 14,
                new Color(0.5f, 0.95f, 0.6f), TextAnchor.UpperLeft);
            SetRect(zoneStatusText.rectTransform, new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.575f));

            UIBuilder.Panel(panelRect, "Sep1", new Vector2(0.04f, 0.425f), new Vector2(0.96f, 0.4295f), Separator);

            // 4. «РЕСУРС УПРАВЛЕНИЯ ● ● ●» (moved out of the old top area).
            resourceRow = UIBuilder.Panel(panelRect, "ResourceRow",
                new Vector2(0.03f, 0.255f), new Vector2(0.97f, 0.41f), new Color(0f, 0f, 0f, 0f));
            resourceLabel = UIBuilder.Label(resourceRow, "ResourceLabel", Loc.ControlResource, 14,
                new Color(0.6f, 0.9f, 0.68f), TextAnchor.MiddleLeft);
            SetRect(resourceLabel.rectTransform, new Vector2(0.02f, 0f), new Vector2(0.66f, 1f));
            int maxCells = TacticsData.Get().resourceMax;
            for (int i = 0; i < maxCells; i++)
            {
                var cellGO = new GameObject("Cell" + i);
                cellGO.transform.SetParent(resourceRow, false);
                var cellRt = cellGO.AddComponent<RectTransform>();
                float x0 = 0.68f + i * 0.1f;
                cellRt.anchorMin = new Vector2(x0, 0.26f);
                cellRt.anchorMax = new Vector2(x0 + 0.075f, 0.74f);
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

            UIBuilder.Panel(panelRect, "Sep2", new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.2445f), Separator);

            // 5. «РЕШИМОСТЬ ИНЖЕНЕРА» + compact bar (moved out of the removed top HUD).
            resolveRow = UIBuilder.Panel(panelRect, "ResolveRow",
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.228f), new Color(0f, 0f, 0f, 0f));
            resolveLabel = UIBuilder.Label(resolveRow, "ResolveLabel", Loc.EngineerResolve, 14,
                new Color(1f, 0.72f, 0.42f), TextAnchor.UpperLeft);
            SetRect(resolveLabel.rectTransform, new Vector2(0.02f, 0.5f), new Vector2(0.98f, 1f));
            resolveFill = UIBuilder.Bar(resolveRow, "ResolveBar", new Vector2(0.02f, 0.1f), new Vector2(0.98f, 0.46f),
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.25f));
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------------- refresh ----------------

        public void Refresh(InteractableMachine selected, LastShift.Engineer.EngineerController engineer)
        {
            if (selected != null)
            {
                enterHint.text = selected.IsReady
                    ? Loc.EnterExecute + selected.CommandLabel.ToUpper()
                    : Loc.SystemBusy + " — " + selected.displayName.ToUpper();

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
