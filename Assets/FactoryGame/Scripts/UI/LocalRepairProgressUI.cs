using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Objectives;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// Compact «РЕМОНТ» readout that lives directly above its own repair console.
    /// Replaces the removed global top progress bar: progress is shown where the
    /// work happens, only while that console is actually being repaired, and it
    /// fades out again so it never hides the engineer or the route.
    /// </summary>
    public class LocalRepairProgressUI : MonoBehaviour
    {
        RepairObjective objective;
        CanvasGroup group;
        RectTransform fill;
        Text percentLabel;
        Text titleLabel;

        public static LocalRepairProgressUI Attach(RepairObjective target, float worldYOffset)
        {
            var go = new GameObject("LocalRepairProgress");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = new Vector3(0f, worldYOffset, 0f);
            var ui = go.AddComponent<LocalRepairProgressUI>();
            ui.Build(target);
            return ui;
        }

        void Build(RepairObjective target)
        {
            objective = target;

            const float canvasScale = 0.014f;
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localScale = Vector3.one * canvasScale;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 42;
            if (Viz.HasSortingLayer("WorldUI")) canvas.sortingLayerName = "WorldUI";
            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(240f, 62f);
            UIBuilder.ApplyWorldCanvasDensity(canvas, canvasScale);

            group = canvasGO.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            // Opaque industrial plate with a thin amber border.
            RectTransform plate = UIBuilder.Panel(canvasGO.transform, "Plate",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.05f, 0.038f, 0.96f));
            Edge(plate, "EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
            Edge(plate, "EdgeB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
            Edge(plate, "EdgeL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            Edge(plate, "EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));

            titleLabel = UIBuilder.Label(plate, "Title", Loc.RepairProgress, 20,
                new Color(1f, 0.72f, 0.4f), TextAnchor.MiddleLeft);
            SetRect(titleLabel.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.6f, 0.95f));
            percentLabel = UIBuilder.Label(plate, "Percent", "0%", 20,
                new Color(0.95f, 0.9f, 0.7f), TextAnchor.MiddleRight);
            SetRect(percentLabel.rectTransform, new Vector2(0.6f, 0.48f), new Vector2(0.94f, 0.95f));

            RectTransform back = UIBuilder.Panel(plate, "BarBack",
                new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.42f), new Color(0.1f, 0.13f, 0.11f, 1f));
            fill = UIBuilder.Panel(back, "BarFill", Vector2.zero, Vector2.one, new Color(1f, 0.55f, 0.28f, 1f));
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
        }

        static void Edge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
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
            img.color = new Color(0.95f, 0.7f, 0.35f, 0.75f);
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
            if (objective == null) return;
            // Visible only for the console under repair right now; a finished or
            // untouched console shows nothing (no duplicated global bar anywhere).
            bool show = objective.IsBeingRepaired && !objective.Completed;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.deltaTime * (show ? 6f : 2.5f));
            if (group.alpha <= 0.001f) return;

            float p = Mathf.Clamp01(objective.Progress01);
            UIBuilder.SetBar(fill, p);
            percentLabel.text = Mathf.RoundToInt(p * 100f) + "%";
            // Amber while working, red as it nears completion (a real warning).
            var img = fill.GetComponent<Image>();
            if (img != null)
                img.color = p > 0.75f ? new Color(1f, 0.35f, 0.25f, 1f) : new Color(1f, 0.55f, 0.28f, 1f);
            if (titleLabel != null)
                titleLabel.color = p > 0.75f ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.72f, 0.4f);
        }
    }
}
