using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Machines;

namespace LastShift.UI
{
    /// <summary>One row of the terminal list: marker, label, cooldown bar + time.</summary>
    public class CommandListItemUI : MonoBehaviour
    {
        Image background;
        Text marker;
        Text label;
        Text cooldownText;
        RectTransform cooldownFill;

        static readonly Color ReadyText = new Color(0.65f, 1f, 0.75f);
        static readonly Color SelectedText = new Color(0.9f, 1f, 0.9f);
        static readonly Color CooldownTextColor = new Color(0.45f, 0.55f, 0.48f);
        static readonly Color SelectedBg = new Color(0.10f, 0.28f, 0.16f, 0.95f);
        static readonly Color NormalBg = new Color(0f, 0f, 0f, 0f);

        public static CommandListItemUI Create(Transform parent, int index, float rowHeight)
        {
            var go = new GameObject("Command_" + index);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, rowHeight);
            rt.anchoredPosition = new Vector2(0f, -index * rowHeight);

            var item = go.AddComponent<CommandListItemUI>();
            item.Build();
            return item;
        }

        void Build()
        {
            background = gameObject.AddComponent<Image>();
            background.color = NormalBg;
            background.raycastTarget = false;

            marker = UIBuilder.Label(transform, "Marker", ">", 24, SelectedText, TextAnchor.MiddleCenter);
            SetRect(marker.rectTransform, new Vector2(0f, 0f), new Vector2(0.08f, 1f));

            label = UIBuilder.Label(transform, "Label", "", 20, ReadyText, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.98f, 1f));

            var barBack = UIBuilder.PanelPx(transform, "CdBack",
                new Vector2(0.08f, 0.12f), new Vector2(0.58f, 0.30f),
                Vector2.zero, Vector2.zero, new Color(0.06f, 0.1f, 0.07f));
            cooldownFill = UIBuilder.Panel(barBack, "CdFill", Vector2.zero, Vector2.one,
                new Color(0.35f, 0.6f, 0.4f));
            cooldownFill.offsetMin = new Vector2(1f, 1f);
            cooldownFill.offsetMax = new Vector2(-1f, -1f);

            cooldownText = UIBuilder.Label(transform, "CdText", "", 13, CooldownTextColor, TextAnchor.MiddleRight);
            SetRect(cooldownText.rectTransform, new Vector2(0.59f, 0.05f), new Vector2(0.98f, 0.35f));
        }

        /// <summary>Re-fits the row when the panel's real height differs from the reference.</summary>
        public void SetRowHeight(int index, float height)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -index * height);
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void Refresh(FactoryCommandItem item, bool selected, bool emergency)
        {
            bool ready = item.IsReady;
            background.color = selected ? (emergency ? new Color(0.32f, 0.08f, 0.06f, 0.95f) : SelectedBg) : NormalBg;
            marker.text = selected ? ">" : " ";
            label.text = item.Label;
            label.color = ready ? (selected ? SelectedText : ReadyText) : CooldownTextColor;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;

            if (ready)
            {
                // Three readable states: plain ready, setup opportunity, low value.
                var m = item.machine;
                if (m != null && !m.ActivationAlwaysEffective && m.EngineerInEffectiveZone)
                {
                    cooldownText.text = Loc.StateGoodMoment;
                    cooldownText.color = new Color(0.98f, 0.82f, 0.35f);
                }
                else if (m != null && !m.ActivationAlwaysEffective)
                {
                    cooldownText.text = Loc.StateLowValue;
                    cooldownText.color = new Color(0.42f, 0.52f, 0.46f);
                }
                else
                {
                    cooldownText.text = Loc.Ready;
                    cooldownText.color = new Color(0.5f, 0.85f, 0.55f);
                }
                UIBuilder.SetBar(cooldownFill, 1f);
                cooldownFill.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f);
            }
            else
            {
                var m = item.machine;
                bool active = m != null && m.State == MachineState.Active;
                cooldownText.text = active ? Loc.Active
                    : (m != null ? m.CooldownRemaining.ToString("0.0") + " с" : Loc.Unavailable);
                cooldownText.color = active ? new Color(0.95f, 0.8f, 0.4f) : CooldownTextColor;
                UIBuilder.SetBar(cooldownFill, active ? 1f : 1f - item.CooldownFraction);
                cooldownFill.GetComponent<Image>().color = active
                    ? new Color(0.85f, 0.65f, 0.25f)
                    : new Color(0.25f, 0.4f, 0.3f);
            }
        }
    }
}
