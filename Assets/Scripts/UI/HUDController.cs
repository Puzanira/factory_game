using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Core;
using LastShift.Data;
using LastShift.Hazards;
using LastShift.Machines;

namespace LastShift.UI
{
    /// <summary>
    /// Top HUD (room name, Resolve, Pressure, objective, repair progress), full-screen
    /// overlays (escalation, alarm, fog) and the Russian toast notification stack
    /// (machine actions, combo bonuses, warnings, tutorial hints).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        LevelManager lm;

        Text roomLabel;
        Text objectiveLabel;
        Text stateLabel;
        RectTransform resolveFill;
        RectTransform pressureFill;
        RectTransform repairFill;
        Image escalationOverlay;
        Image alarmOverlay;
        Image fogOverlay;
        RectTransform toastStack;
        readonly List<GameObject> activeToasts = new List<GameObject>();

        public void Bind(LevelManager levelManager, Canvas canvas)
        {
            lm = levelManager;
            Build(canvas.transform);
        }

        void Build(Transform root)
        {
            // Overlays sit under the panels so text stays readable.
            fogOverlay = Panel(root, "FogOverlay", new Color(0.5f, 0.7f, 1f, 0f));
            alarmOverlay = Panel(root, "AlarmOverlay", new Color(1f, 0.15f, 0.1f, 0f));
            escalationOverlay = Panel(root, "EscalationOverlay", new Color(1f, 0.1f, 0.05f, 0f));

            // Top bar over the play area (terminal occupies the left 28%).
            RectTransform top = UIBuilder.Panel(root, "TopBar",
                new Vector2(0.28f, 0.92f), new Vector2(1f, 1f), new Color(0.05f, 0.06f, 0.07f, 0.92f));

            roomLabel = UIBuilder.Label(top, "Room", "ЦЕХ", 28, new Color(0.95f, 0.9f, 0.75f), TextAnchor.MiddleLeft);
            SetOffsets(roomLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(18f, 0f), new Vector2(0f, 0f));

            var resolveTitle = UIBuilder.Label(top, "ResolveTitle", Loc.EngineerResolve, 16, new Color(1f, 0.7f, 0.4f), TextAnchor.UpperLeft);
            SetOffsets(resolveTitle.rectTransform, new Vector2(0.36f, 0.5f), new Vector2(0.66f, 1f), new Vector2(0f, 0f), new Vector2(0f, -6f));
            resolveFill = UIBuilder.Bar(top, "ResolveBar", new Vector2(0.36f, 0.14f), new Vector2(0.66f, 0.5f),
                new Vector2(0f, 0f), new Vector2(-14f, 0f), new Color(0.12f, 0.1f, 0.08f), new Color(1f, 0.6f, 0.25f));

            var pressureTitle = UIBuilder.Label(top, "PressureTitle", Loc.RoomPressure, 16, new Color(1f, 0.35f, 0.3f), TextAnchor.UpperLeft);
            SetOffsets(pressureTitle.rectTransform, new Vector2(0.68f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-14f, -6f));
            pressureFill = UIBuilder.Bar(top, "PressureBar", new Vector2(0.68f, 0.14f), new Vector2(1f, 0.5f),
                new Vector2(0f, 0f), new Vector2(-14f, 0f), new Color(0.12f, 0.07f, 0.07f), new Color(0.95f, 0.2f, 0.15f));

            // Objective strip under the top bar.
            RectTransform strip = UIBuilder.Panel(root, "ObjectiveStrip",
                new Vector2(0.28f, 0.865f), new Vector2(1f, 0.92f), new Color(0.05f, 0.06f, 0.07f, 0.75f));
            objectiveLabel = UIBuilder.Label(strip, "Objective", "", 19, new Color(0.8f, 0.95f, 0.8f), TextAnchor.MiddleLeft);
            SetOffsets(objectiveLabel.rectTransform, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(18f, 0f), Vector2.zero);
            var repairTitle = UIBuilder.Label(strip, "RepairTitle", Loc.RepairProgress, 15, new Color(0.7f, 0.8f, 0.7f), TextAnchor.MiddleLeft);
            SetOffsets(repairTitle.rectTransform, new Vector2(0.64f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            repairFill = UIBuilder.Bar(strip, "RepairBar", new Vector2(0.72f, 0.25f), new Vector2(0.97f, 0.75f),
                Vector2.zero, Vector2.zero, new Color(0.1f, 0.12f, 0.1f), new Color(0.45f, 0.95f, 0.55f));

            // Toast stack: top-center of the play area, below the objective strip.
            toastStack = UIBuilder.Panel(root, "ToastStack",
                new Vector2(0.34f, 0.62f), new Vector2(0.94f, 0.855f), new Color(0f, 0f, 0f, 0f));
            toastStack.GetComponent<Image>().raycastTarget = false;

            // Engineer state echo + hints, bottom right.
            RectTransform bottom = UIBuilder.Panel(root, "BottomHints",
                new Vector2(0.62f, 0f), new Vector2(1f, 0.05f), new Color(0.05f, 0.06f, 0.07f, 0.6f));
            stateLabel = UIBuilder.Label(bottom, "State", "", 17, new Color(0.95f, 0.9f, 0.7f), TextAnchor.MiddleLeft);
            SetOffsets(stateLabel.rectTransform, Vector2.zero, new Vector2(0.55f, 1f), new Vector2(14f, 0f), Vector2.zero);
            var hints = UIBuilder.Label(bottom, "Hints", Loc.BottomHints, 15, new Color(0.6f, 0.65f, 0.6f), TextAnchor.MiddleRight);
            SetOffsets(hints.rectTransform, new Vector2(0.55f, 0f), Vector2.one, Vector2.zero, new Vector2(-14f, 0f));
        }

        Image Panel(Transform root, string name, Color color)
        {
            RectTransform rt = UIBuilder.Panel(root, name, Vector2.zero, Vector2.one, color);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        static void SetOffsets(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = oMin;
            rt.offsetMax = oMax;
        }

        // ---------------- toast notifications ----------------

        /// <summary>Show a short Russian status toast (machine action, warning, hint).</summary>
        public void ShowToast(string message, float duration = 2.6f, bool warning = false)
        {
            if (string.IsNullOrEmpty(message) || toastStack == null) return;
            StartCoroutine(ToastRoutine(message, duration, warning));
        }

        IEnumerator ToastRoutine(string message, float duration, bool warning)
        {
            // Cap the stack at 4 visible toasts.
            while (activeToasts.Count >= 4)
            {
                var oldest = activeToasts[0];
                activeToasts.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }

            var go = new GameObject("Toast");
            go.transform.SetParent(toastStack, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(560f, 34f);
            var bg = go.AddComponent<Image>();
            bg.color = warning ? new Color(0.3f, 0.05f, 0.03f, 0.88f) : new Color(0.03f, 0.1f, 0.06f, 0.88f);
            bg.raycastTarget = false;
            var group = go.AddComponent<CanvasGroup>();
            var text = UIBuilder.Label(go.transform, "Text", message, 18,
                warning ? new Color(1f, 0.55f, 0.4f) : new Color(0.7f, 1f, 0.78f), TextAnchor.MiddleCenter);
            text.raycastTarget = false;

            activeToasts.Add(go);

            float t = 0f;
            while (t < duration && go != null)
            {
                t += Time.unscaledDeltaTime;
                // Restack (older toasts slide down).
                int idx = activeToasts.IndexOf(go);
                if (idx >= 0) rt.anchoredPosition = new Vector2(0f, -idx * 40f);
                group.alpha = t > duration - 0.5f ? (duration - t) / 0.5f : 1f;
                yield return null;
            }
            activeToasts.Remove(go);
            if (go != null) Destroy(go);
        }

        // ---------------- per-frame ----------------

        void Update()
        {
            if (lm == null) return;
            var engineer = lm.Engineer;

            roomLabel.text = lm.RoomName;

            if (engineer != null && engineer.Stats != null)
            {
                float max = engineer.Data != null ? engineer.Data.resolveMax : 100f;
                UIBuilder.SetBar(resolveFill, engineer.Stats.Resolve / max);
                stateLabel.text = Loc.EngineerLabel + (engineer.Fsm != null ? engineer.Fsm.CurrentLabel.ToUpper() : "—");
            }

            if (lm.Pressure != null)
                UIBuilder.SetBar(pressureFill, lm.Pressure.Value / lm.Pressure.Max);

            var obj = lm.CurrentObjectiveForHud;
            if (engineer != null && engineer.IsRetreating)
            {
                objectiveLabel.text = Loc.CurrentObjective + ": " + Loc.ObjectiveReachExit;
                UIBuilder.SetBar(repairFill, 0f);
            }
            else if (obj != null)
            {
                objectiveLabel.text = Loc.CurrentObjective + ": " + obj.objectiveName.ToUpper();
                UIBuilder.SetBar(repairFill, obj.Progress01);
            }
            else
            {
                objectiveLabel.text = Loc.CurrentObjective + ": —";
                UIBuilder.SetBar(repairFill, 0f);
            }

            // Overlays. Fog kept subtle so routes stay readable.
            float esc = lm.Escalated ? 0.09f + 0.07f * Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) : 0f;
            escalationOverlay.color = WithAlpha(escalationOverlay.color, esc);
            float alarm = AlarmMachine.ActiveCount > 0 ? 0.04f + 0.04f * Mathf.PingPong(Time.unscaledTime * 4f, 1f) : 0f;
            alarmOverlay.color = WithAlpha(alarmOverlay.color, alarm);
            fogOverlay.color = WithAlpha(fogOverlay.color, ColdHazard.FogIntensity * 0.13f);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
