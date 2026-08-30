using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastShift.EditorTools
{
    /// <summary>
    /// Reports what the running view actually gives the game: resolution, canvas scale
    /// factor and the resulting physical text size.
    ///
    /// Why this exists: uGUI bakes dynamic-font glyphs at fontSize * canvas.scaleFactor,
    /// and CanvasScaler derives that factor from Screen against the 1920x1080 reference.
    /// A Game view is almost never 1920x1080 — "Free Aspect" renders at the panel size,
    /// and a fixed resolution larger than the panel is rendered in full and then shrunk
    /// for display. Either way the text on screen is not what a fullscreen player sees,
    /// which makes the Game view a poor place to judge font quality. Run this in Play
    /// Mode to see which case you are in.
    /// </summary>
    public static class ScreenDiagnostics
    {
        [MenuItem("Tools/Last Shift/Log Screen Info")]
        public static void LogScreenInfo()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("SCREEN_INFO: enter Play Mode first — Screen and canvas " +
                                 "scale factors are only meaningful while the game runs.");
                return;
            }

            float dpi = Screen.dpi > 1f ? Screen.dpi : 0f;
            Debug.Log($"SCREEN_INFO resolution={Screen.width}x{Screen.height} " +
                      $"dpi={(dpi > 0f ? dpi.ToString("0.#") : "unknown")} " +
                      $"fullScreen={Screen.fullScreen}");

            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                var scaler = canvas.GetComponent<CanvasScaler>();
                string reference = scaler != null ? scaler.referenceResolution.ToString() : "no scaler";
                Debug.Log($"  CANVAS {canvas.name} mode={canvas.renderMode} " +
                          $"scaleFactor={canvas.scaleFactor:0.###} reference={reference}");

                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // A 14 px label is a common size in the terminal panel.
                    float px = 14f * canvas.scaleFactor;
                    string mm = dpi > 0f ? $" ({px / dpi * 25.4f:0.0} mm)" : "";
                    Debug.Log($"    a fontSize 14 label is baked and drawn at {px:0.#} px{mm}" +
                              (Mathf.Abs(canvas.scaleFactor - 1f) < 0.01f
                                  ? " — 1:1, the fullscreen 1080p case"
                                  : " — NOT the 1080p case; judge fonts fullscreen instead"));
                }
            }

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                Debug.Log($"  CAMERA ortho={cam.orthographicSize:0.###} " +
                          $"viewport={cam.pixelWidth}x{cam.pixelHeight} " +
                          $"pixelsPerWorldUnit={cam.pixelHeight / (2f * cam.orthographicSize):0.0}");
            }
        }
    }
}
