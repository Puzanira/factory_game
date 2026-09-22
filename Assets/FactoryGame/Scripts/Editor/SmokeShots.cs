using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastShift.EditorTools
{
    /// <summary>
    /// Frames out of a headless run: PNGs of what the player actually sees.
    ///
    /// Why this exists: text and layout passes have to be judged by eye, and the
    /// only eyes available to a batch session are these files. A Game view is not
    /// an option (batch mode has no window), and the founder's editor must not be
    /// borrowed for it.
    ///
    /// How: the play camera gets a 1920×1080 RenderTexture for one frame and the
    /// screen-space-overlay canvases are temporarily reparented onto that camera —
    /// overlay UI is composited straight to the display and would otherwise be
    /// missing from the capture, which is exactly the half that carries the text.
    /// The read happens on the NEXT tick, after the player loop has rendered a
    /// frame into the texture, so no manual Camera.Render() is needed. Everything
    /// is restored immediately afterwards; the run continues untouched.
    ///
    /// Driven by PlayModeSmokeTest: -smokeShots &lt;dir&gt; [-smokeShotAt 3,12,30]
    /// (seconds of play time; default every 6 s). Run Unity WITHOUT -nographics
    /// and with -screen-width 1920 -screen-height 1080, otherwise the CanvasScaler
    /// lays the UI out for the default tiny batch screen.
    /// </summary>
    public static class SmokeShots
    {
        const string DirKey = "LastShift.SmokeShotDir";
        const string TimesKey = "LastShift.SmokeShotTimes";
        const string DoneKey = "LastShift.SmokeShotDone";
        const int Width = 1920;
        const int Height = 1080;

        // Live only between the two halves of a capture (same domain, one frame apart).
        static RenderTexture pending;
        static Camera pendingCam;
        static RenderTexture pendingPrevTarget;
        static string pendingPath;
        static readonly List<Canvas> switched = new List<Canvas>();
        static readonly List<int> switchedOrder = new List<int>();

        // Overlay UI is composited last, above every sprite, whatever its sorting
        // order. Once the canvas renders THROUGH the camera it sorts with the world
        // instead, and effect sprites (the cold room's fog, steam) climb over the
        // result panel — a lie the frame would tell about the game. Lifting every
        // captured canvas by this much restores «UI last» while keeping the
        // canvases' order relative to each other.
        const int OverlayLift = 20000;

        /// <summary>Reads the command line. Call once from the smoke test's Run().</summary>
        public static void Configure(string[] args)
        {
            string dir = null;
            string times = "6,12,18,24,30";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-smokeShots" && i + 1 < args.Length) dir = args[i + 1];
                if (args[i] == "-smokeShotAt" && i + 1 < args.Length) times = args[i + 1];
            }
            SessionState.SetString(DirKey, dir ?? "");
            SessionState.SetString(TimesKey, times);
            SessionState.SetInt(DoneKey, 0);
        }

        /// <summary>Call once per smoke tick while playing.</summary>
        public static void Tick(double elapsed)
        {
            if (pending != null) { Finish(); return; }

            string dir = SessionState.GetString(DirKey, "");
            if (string.IsNullOrEmpty(dir)) return;

            int done = SessionState.GetInt(DoneKey, 0);
            string[] parts = SessionState.GetString(TimesKey, "").Split(',');
            if (done >= parts.Length) return;
            if (!float.TryParse(parts[done], NumberStyles.Float, CultureInfo.InvariantCulture, out float at)) return;
            if (elapsed < at) return;

            SessionState.SetInt(DoneKey, done + 1);
            Begin(Path.Combine(dir, "shot-" + at.ToString("000", CultureInfo.InvariantCulture) + "s.png"));
        }

        static void Begin(string path)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null) { Debug.Log("SMOKE_SHOT_SKIP no camera: " + path); return; }

            switched.Clear();
            switchedOrder.Clear();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                switched.Add(c);
                switchedOrder.Add(c.sortingOrder);
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder += OverlayLift;
            }

            pendingCam = cam;
            pendingPrevTarget = cam.targetTexture;
            pending = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            pendingPath = path;
            cam.targetTexture = pending;
        }

        static void Finish()
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = pending;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath));
            File.WriteAllBytes(pendingPath, tex.EncodeToPNG());
            Debug.Log("SMOKE_SHOT " + pendingPath);

            pendingCam.targetTexture = pendingPrevTarget;
            for (int i = 0; i < switched.Count; i++)
            {
                if (switched[i] == null) continue;
                switched[i].renderMode = RenderMode.ScreenSpaceOverlay;
                switched[i].worldCamera = null;
                switched[i].sortingOrder = switchedOrder[i];
            }
            switched.Clear();
            switchedOrder.Clear();

            Object.DestroyImmediate(tex);
            pending.Release();
            Object.DestroyImmediate(pending);
            pending = null;
            pendingCam = null;
            pendingPath = null;
        }
    }
}
