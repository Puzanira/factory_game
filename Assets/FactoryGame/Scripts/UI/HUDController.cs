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
    /// Screen effects and transient messages only. Resolve and the control resource
    /// are a thin permanent strip along the top row of the play area, built by
    /// CommandTerminalUI (TacticalDetailPanelController); repair progress lives at
    /// the repair console, and the engineer's state stays above the engineer. What
    /// is left here: full-screen overlays (escalation, alarm, fog), the Russian
    /// toast stack, and a room title that fades away at room start — both of them
    /// kept below that strip so nothing ever overlaps it.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        LevelManager lm;

        Image escalationOverlay;
        Image alarmOverlay;
        Image fogOverlay;
        RectTransform toastStack;
        Text roomTitle;
        CanvasGroup roomTitleGroup;
        readonly List<GameObject> activeToasts = new List<GameObject>();

        public void Bind(LevelManager levelManager, Canvas canvas)
        {
            lm = levelManager;
            Build(canvas.transform);
        }

        void Build(Transform root)
        {
            // Overlays sit under everything else so text stays readable.
            fogOverlay = Panel(root, "FogOverlay", new Color(0.5f, 0.7f, 1f, 0f));
            alarmOverlay = Panel(root, "AlarmOverlay", new Color(1f, 0.15f, 0.1f, 0f));
            escalationOverlay = Panel(root, "EscalationOverlay", new Color(1f, 0.1f, 0.05f, 0f));

            // Room title: shown briefly at room start, then gone — never a header.
            // It sits just under the permanent gauge strip (which owns the top row).
            RectTransform titleRow = UIBuilder.Panel(root, "RoomTitle",
                new Vector2(0.3f, 0.815f), new Vector2(0.98f, 0.895f), new Color(0f, 0f, 0f, 0f));
            roomTitleGroup = titleRow.gameObject.AddComponent<CanvasGroup>();
            roomTitleGroup.alpha = 0f;
            roomTitleGroup.blocksRaycasts = false;
            roomTitle = UIBuilder.Label(titleRow, "Label", "", 30, new Color(0.95f, 0.9f, 0.75f), TextAnchor.MiddleCenter);
            roomTitle.raycastTarget = false;

            // Toast stack: top of the play area, under the (temporary) room title.
            toastStack = UIBuilder.Panel(root, "ToastStack",
                new Vector2(0.34f, 0.55f), new Vector2(0.96f, 0.805f), new Color(0f, 0f, 0f, 0f));
            toastStack.GetComponent<Image>().raycastTarget = false;
            // No corner hint strip: the cabinet has no pause and nothing to announce here.
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

        // ---------------- room title ----------------

        /// <summary>Announce the room, then fade out — the top of the screen is gameplay space.</summary>
        public void ShowRoomTitle(string title, string goal)
        {
            if (roomTitle == null) return;
            roomTitle.text = string.IsNullOrEmpty(goal) ? title : title + "   —   " + goal.ToUpper();
            StartCoroutine(RoomTitleRoutine());
        }

        IEnumerator RoomTitleRoutine()
        {
            const float fadeIn = 0.5f, hold = 3.2f, fadeOut = 1.1f;
            float t = 0f;
            while (t < fadeIn) { t += Time.unscaledDeltaTime; roomTitleGroup.alpha = t / fadeIn; yield return null; }
            roomTitleGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            t = 0f;
            while (t < fadeOut) { t += Time.unscaledDeltaTime; roomTitleGroup.alpha = 1f - t / fadeOut; yield return null; }
            roomTitleGroup.alpha = 0f;
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
            bg.color = warning ? new Color(0.3f, 0.05f, 0.03f, 0.92f) : new Color(0.02f, 0.09f, 0.055f, 0.92f);
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
