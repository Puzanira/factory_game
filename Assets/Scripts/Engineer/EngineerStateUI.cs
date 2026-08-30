using UnityEngine;
using UnityEngine.UI;
using LastShift.UI;

namespace LastShift.Engineer
{
    /// <summary>World-space label above the engineer showing his current AI state.</summary>
    public class EngineerStateUI : MonoBehaviour
    {
        Text label;
        EngineerController engineer;

        public void Bind(EngineerController controller)
        {
            engineer = controller;
            BuildLabel();
            controller.Fsm.StateChanged += OnStateChanged;
            OnStateChanged(controller.Fsm.CurrentId, controller.Fsm.CurrentLabel);
        }

        void BuildLabel()
        {
            const float canvasScale = 0.02f;
            var canvasGO = new GameObject("StateLabelCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            canvasGO.transform.localScale = Vector3.one * canvasScale;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            if (LastShift.Utilities.Viz.HasSortingLayer("WorldUI"))
                canvas.sortingLayerName = "WorldUI";
            var rt = (RectTransform)canvasGO.transform;
            rt.sizeDelta = new Vector2(200f, 30f);
            UIBuilder.ApplyWorldCanvasDensity(canvas, canvasScale);

            label = UIBuilder.Label(canvasGO.transform, "State", "…", 22,
                new Color(1f, 0.95f, 0.8f), TextAnchor.MiddleCenter);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
        }

        void OnStateChanged(EngineerStateId id, string display)
        {
            if (label != null) label.text = display;
        }

        void OnDestroy()
        {
            if (engineer != null && engineer.Fsm != null)
                engineer.Fsm.StateChanged -= OnStateChanged;
        }
    }
}
