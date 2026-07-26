using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// The picture behind «ЗАВОД ПОБЕДИЛ» after the last room: a perimeter-camera
    /// frame of the plant at night, drawn entirely in code like the rest of the game.
    /// It stages itself — the signal locks in, the hall lights its windows one by one
    /// as autonomous control settles, chimneys start breathing steam, a survey beam
    /// sweeps the facade, and the last engineer walks out through the gate and is
    /// gone. Cold and controlled, not celebratory.
    /// </summary>
    public class FinalVictoryScreen : MonoBehaviour
    {
        // Night industrial palette.
        static readonly Color Sky = new Color(0.020f, 0.045f, 0.045f, 1f);
        static readonly Color SkyHigh = new Color(0.010f, 0.022f, 0.026f, 1f);
        static readonly Color HorizonGlow = new Color(0.55f, 0.40f, 0.18f, 0.16f);
        static readonly Color FarBuilding = new Color(0.028f, 0.055f, 0.052f, 1f);
        static readonly Color Building = new Color(0.045f, 0.075f, 0.070f, 1f);
        static readonly Color BuildingEdge = new Color(0.10f, 0.16f, 0.14f, 1f);
        static readonly Color Ground = new Color(0.014f, 0.028f, 0.026f, 1f);
        static readonly Color WindowDark = new Color(0.06f, 0.09f, 0.08f, 1f);
        static readonly Color WindowAmber = new Color(0.95f, 0.72f, 0.30f, 1f);
        static readonly Color WindowGreen = new Color(0.45f, 0.95f, 0.55f, 1f);
        static readonly Color Phosphor = new Color(0.62f, 1f, 0.72f);
        static readonly Color Amber = new Color(0.95f, 0.85f, 0.45f);

        RectTransform viewport;
        CanvasGroup group;

        readonly List<Image> windows = new List<Image>();
        readonly List<Image> stars = new List<Image>();
        readonly List<RectTransform> steam = new List<RectTransform>();
        readonly List<float> steamPhase = new List<float>();
        readonly List<Vector2> steamOrigin = new List<Vector2>();

        RectTransform engineer;
        CanvasGroup engineerGroup;
        RectTransform scanBeam;
        Image recDot;
        Text statusLabel;
        Image lampGlow;

        float bornAt;
        bool beamSweeping;
        float beamStartedAt;

        /// <summary>Builds the camera frame inside the given panel area and starts it.</summary>
        public static FinalVictoryScreen Create(Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("FinalVictoryScreen");
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<FinalVictoryScreen>();
            screen.Build(aMin, aMax);
            return screen;
        }

        void Build(Vector2 aMin, Vector2 aMax)
        {
            viewport = gameObject.AddComponent<RectTransform>();
            viewport.anchorMin = aMin;
            viewport.anchorMax = aMax;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            gameObject.AddComponent<Image>().color = SkyHigh;
            gameObject.AddComponent<RectMask2D>();  // nothing spills out of the frame
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            BuildSky();
            BuildFarSkyline();
            BuildWaterTower();
            BuildMainHall();
            BuildChimneys();
            BuildTank();
            BuildFence();
            BuildEngineer();
            BuildBeam();
            BuildFrameOverlay();

            bornAt = Time.unscaledTime;
            StartCoroutine(Reveal());
        }

        // ================= construction =================

        RectTransform Rect(string name, Vector2 min, Vector2 max, Color c) =>
            UIBuilder.Panel(viewport, name, min, max, c);

        void BuildSky()
        {
            // Stacked bands instead of a texture gradient: cheap and reads well in the dark.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 5f;
                Rect("SkyBand" + i, new Vector2(0f, 0.30f + t * 0.14f), new Vector2(1f, 1f),
                    new Color(Sky.r, Sky.g, Sky.b, 0.35f));
            }
            Rect("HorizonGlow", new Vector2(0f, 0.28f), new Vector2(1f, 0.40f), HorizonGlow);

            // Faint stars, unevenly spread.
            for (int i = 0; i < 26; i++)
            {
                float x = Frac(i * 0.6180339f);
                float y = 0.58f + Frac(i * 0.3819f) * 0.40f;
                float s = 1.6f + Frac(i * 0.271f) * 1.8f;
                var star = new GameObject("Star" + i);
                star.transform.SetParent(viewport, false);
                var rt = star.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(x, y);
                rt.anchorMax = new Vector2(x, y);
                rt.sizeDelta = new Vector2(s, s);
                var img = star.AddComponent<Image>();
                img.sprite = TextureFactory.SoftCircle();
                img.color = new Color(0.7f, 0.85f, 0.8f, 0.15f + Frac(i * 0.77f) * 0.3f);
                img.raycastTarget = false;
                stars.Add(img);
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        void BuildFarSkyline()
        {
            // Distant halls: flatter and darker, so the main plant reads in front.
            float[] xs = { 0.00f, 0.09f, 0.21f, 0.34f, 0.62f, 0.78f, 0.92f };
            float[] hs = { 0.09f, 0.13f, 0.07f, 0.11f, 0.08f, 0.12f, 0.09f };
            for (int i = 0; i < xs.Length; i++)
                Rect("Far" + i, new Vector2(xs[i], 0.30f), new Vector2(xs[i] + 0.11f, 0.30f + hs[i]), FarBuilding);
            Rect("Ground", new Vector2(0f, 0f), new Vector2(1f, 0.30f), Ground);
            Rect("GroundEdge", new Vector2(0f, 0.298f), new Vector2(1f, 0.303f), new Color(0.12f, 0.2f, 0.17f, 1f));
        }

        void BuildWaterTower()
        {
            Rect("TowerLegL", new Vector2(0.055f, 0.30f), new Vector2(0.063f, 0.60f), Building);
            Rect("TowerLegR", new Vector2(0.105f, 0.30f), new Vector2(0.113f, 0.60f), Building);
            Rect("TowerBrace", new Vector2(0.055f, 0.44f), new Vector2(0.113f, 0.447f), Building);
            Rect("TowerTank", new Vector2(0.046f, 0.60f), new Vector2(0.122f, 0.68f), Building);
            Rect("TowerTop", new Vector2(0.058f, 0.68f), new Vector2(0.110f, 0.70f), BuildingEdge);
            var lamp = Rect("TowerLamp", new Vector2(0.080f, 0.705f), new Vector2(0.088f, 0.713f), new Color(1f, 0.35f, 0.25f, 1f));
            lampGlow = UIBuilder.Panel(viewport, "TowerLampGlow", new Vector2(0.066f, 0.692f), new Vector2(0.102f, 0.728f),
                new Color(1f, 0.35f, 0.25f, 0.18f)).GetComponent<Image>();
            lampGlow.sprite = TextureFactory.SoftCircle();
            lampGlow.raycastTarget = false;
            lamp.GetComponent<Image>().raycastTarget = false;
        }

        void BuildMainHall()
        {
            const float x0 = 0.17f, x1 = 0.57f, y0 = 0.30f, y1 = 0.555f;
            Rect("Hall", new Vector2(x0, y0), new Vector2(x1, y1), Building);
            Rect("HallEdge", new Vector2(x0, y1 - 0.006f), new Vector2(x1, y1), BuildingEdge);

            // Saw-tooth roof lights — the silhouette that says "factory hall".
            int teeth = 6;
            float w = (x1 - x0) / teeth;
            for (int i = 0; i < teeth; i++)
            {
                float tx = x0 + i * w;
                Rect("Tooth" + i, new Vector2(tx + w * 0.12f, y1), new Vector2(tx + w * 0.88f, y1 + 0.035f), Building);
                Rect("ToothGlass" + i, new Vector2(tx + w * 0.18f, y1 + 0.008f), new Vector2(tx + w * 0.52f, y1 + 0.030f),
                    new Color(0.10f, 0.16f, 0.15f, 1f));
            }

            // Window grid: dark at first, lit one by one as the plant takes over.
            const int cols = 7, rows = 3;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float wx = Mathf.Lerp(x0 + 0.018f, x1 - 0.018f, cols == 1 ? 0f : c / (float)(cols - 1));
                    float wy = Mathf.Lerp(y0 + 0.030f, y1 - 0.060f, rows == 1 ? 0f : r / (float)(rows - 1));
                    var win = Rect("Win" + r + "_" + c, new Vector2(wx, wy), new Vector2(wx + 0.030f, wy + 0.038f), WindowDark);
                    var img = win.GetComponent<Image>();
                    img.raycastTarget = false;
                    windows.Add(img);
                }
            }

            // Pipe bridge from the hall to the tank.
            Rect("Bridge", new Vector2(x1, 0.475f), new Vector2(0.735f, 0.492f), Building);
            Rect("BridgeRail", new Vector2(x1, 0.492f), new Vector2(0.735f, 0.496f), BuildingEdge);
            Rect("BridgeLegA", new Vector2(0.615f, 0.30f), new Vector2(0.622f, 0.475f), Building);
            Rect("BridgeLegB", new Vector2(0.690f, 0.30f), new Vector2(0.697f, 0.475f), Building);
        }

        void BuildChimneys()
        {
            MakeChimney("ChimneyA", 0.600f, 0.622f, 0.86f);
            MakeChimney("ChimneyB", 0.648f, 0.664f, 0.76f);
        }

        void MakeChimney(string name, float x0, float x1, float top)
        {
            Rect(name, new Vector2(x0, 0.30f), new Vector2(x1, top), Building);
            Rect(name + "Band", new Vector2(x0 - 0.003f, top - 0.03f), new Vector2(x1 + 0.003f, top - 0.018f),
                new Color(0.5f, 0.22f, 0.16f, 1f));
            Rect(name + "Cap", new Vector2(x0 - 0.004f, top), new Vector2(x1 + 0.004f, top + 0.008f), BuildingEdge);

            // Slow steam puffs, recycled forever.
            float cx = (x0 + x1) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(name + "Steam" + i);
                go.transform.SetParent(viewport, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(cx, top);
                rt.anchorMax = new Vector2(cx, top);
                rt.sizeDelta = new Vector2(26f, 26f);
                var img = go.AddComponent<Image>();
                img.sprite = TextureFactory.SoftCircle();
                img.color = new Color(0.75f, 0.85f, 0.82f, 0f);
                img.raycastTarget = false;
                steam.Add(rt);
                steamPhase.Add(i * 0.25f);
                steamOrigin.Add(new Vector2(cx, top));
            }
        }

        void BuildTank()
        {
            const float x0 = 0.745f, x1 = 0.885f, y0 = 0.30f, y1 = 0.60f;
            Rect("Tank", new Vector2(x0, y0), new Vector2(x1, y1), Building);
            var dome = UIBuilder.Panel(viewport, "TankDome", new Vector2(x0, y1 - 0.035f), new Vector2(x1, y1 + 0.035f), Building);
            var domeImg = dome.GetComponent<Image>();
            domeImg.sprite = SpriteFactory.Get(PlaceholderShape.Circle);
            domeImg.raycastTarget = false;
            Rect("TankBandA", new Vector2(x0, 0.40f), new Vector2(x1, 0.406f), BuildingEdge);
            Rect("TankBandB", new Vector2(x0, 0.50f), new Vector2(x1, 0.506f), BuildingEdge);
            var num = UIBuilder.Label(viewport, "TankNumber", "№7", 30, new Color(0.30f, 0.45f, 0.40f, 1f), TextAnchor.MiddleCenter);
            num.rectTransform.anchorMin = new Vector2(x0, 0.42f);
            num.rectTransform.anchorMax = new Vector2(x1, 0.49f);
            num.rectTransform.offsetMin = Vector2.zero;
            num.rectTransform.offsetMax = Vector2.zero;
            num.raycastTarget = false;
        }

        void BuildFence()
        {
            Rect("FenceRail", new Vector2(0f, 0.245f), new Vector2(1f, 0.250f), new Color(0.09f, 0.15f, 0.13f, 1f));
            for (int i = 0; i < 26; i++)
            {
                float x = i / 26f;
                Rect("Post" + i, new Vector2(x, 0.19f), new Vector2(x + 0.004f, 0.252f), new Color(0.08f, 0.13f, 0.12f, 1f));
            }
            // The gate the engineer leaves through: a lit gap in the fence line.
            Rect("GateFrameL", new Vector2(0.885f, 0.19f), new Vector2(0.892f, 0.285f), BuildingEdge);
            Rect("GateFrameR", new Vector2(0.962f, 0.19f), new Vector2(0.969f, 0.285f), BuildingEdge);
            Rect("GateLight", new Vector2(0.885f, 0.19f), new Vector2(0.969f, 0.255f), new Color(0.95f, 0.72f, 0.30f, 0.05f));
        }

        void BuildEngineer()
        {
            var go = new GameObject("Engineer");
            go.transform.SetParent(viewport, false);
            engineer = go.AddComponent<RectTransform>();
            engineer.anchorMin = new Vector2(0.86f, 0.19f);
            engineer.anchorMax = new Vector2(0.90f, 0.245f);
            engineer.offsetMin = Vector2.zero;
            engineer.offsetMax = Vector2.zero;
            engineerGroup = go.AddComponent<CanvasGroup>();
            engineerGroup.alpha = 1f;

            var silhouette = new Color(0.03f, 0.05f, 0.05f, 1f);
            UIBuilder.Panel(engineer, "Head", new Vector2(0.36f, 0.74f), new Vector2(0.64f, 1f), silhouette);
            UIBuilder.Panel(engineer, "Torso", new Vector2(0.28f, 0.32f), new Vector2(0.72f, 0.76f), silhouette);
            UIBuilder.Panel(engineer, "LegL", new Vector2(0.32f, 0f), new Vector2(0.46f, 0.34f), silhouette);
            UIBuilder.Panel(engineer, "LegR", new Vector2(0.54f, 0f), new Vector2(0.68f, 0.34f), silhouette);
        }

        void BuildBeam()
        {
            var go = new GameObject("SurveyBeam");
            go.transform.SetParent(viewport, false);
            scanBeam = go.AddComponent<RectTransform>();
            scanBeam.anchorMin = new Vector2(0f, 0f);
            scanBeam.anchorMax = new Vector2(0f, 1f);
            scanBeam.pivot = new Vector2(0.5f, 0.5f);
            scanBeam.sizeDelta = new Vector2(70f, 0f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 1f, 0.7f, 0f);
            img.raycastTarget = false;
        }

        void BuildFrameOverlay()
        {
            // Camera-frame furniture: it is a terminal feed, not a photograph.
            var scan = UIBuilder.Panel(viewport, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.30f);
            scanImg.raycastTarget = false;

            Rect("EdgeTop", new Vector2(0f, 0.94f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.35f));
            Rect("EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0.06f), new Color(0f, 0f, 0f, 0.35f));

            var cam = UIBuilder.Label(viewport, "CamLabel", Loc.FinalCameraLabel, 15, Phosphor, TextAnchor.MiddleLeft);
            SetRect(cam.rectTransform, new Vector2(0.02f, 0.925f), new Vector2(0.5f, 0.985f));
            cam.raycastTarget = false;

            var rec = new GameObject("RecDot");
            rec.transform.SetParent(viewport, false);
            var recRt = rec.AddComponent<RectTransform>();
            recRt.anchorMin = new Vector2(0.945f, 0.955f);
            recRt.anchorMax = new Vector2(0.945f, 0.955f);
            recRt.sizeDelta = new Vector2(9f, 9f);
            recDot = rec.AddComponent<Image>();
            recDot.sprite = TextureFactory.SoftCircle();
            recDot.color = new Color(0.95f, 0.35f, 0.25f, 1f);
            recDot.raycastTarget = false;
            var recLabel = UIBuilder.Label(viewport, "RecLabel", Loc.FinalRecLabel, 14,
                new Color(0.9f, 0.55f, 0.45f), TextAnchor.MiddleRight);
            SetRect(recLabel.rectTransform, new Vector2(0.6f, 0.925f), new Vector2(0.925f, 0.985f));
            recLabel.raycastTarget = false;

            statusLabel = UIBuilder.Label(viewport, "Status", "", 16, WindowGreen, TextAnchor.MiddleLeft);
            SetRect(statusLabel.rectTransform, new Vector2(0.02f, 0.015f), new Vector2(0.98f, 0.075f));
            statusLabel.raycastTarget = false;

            // Thin frame + corner brackets, same language as every other terminal panel.
            Edge("FrameT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
            Edge("FrameB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
            Edge("FrameL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            Edge("FrameR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));
            Edge("BrTL_h", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, 3f));
            Edge("BrTL_v", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, 28f));
            Edge("BrTR_h", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(28f, 3f));
            Edge("BrTR_v", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(3f, 28f));
            Edge("BrBL_h", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 3f));
            Edge("BrBL_v", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(3f, 28f));
            Edge("BrBR_h", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(28f, 3f));
            Edge("BrBR_v", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(3f, 28f));
        }

        void Edge(string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(viewport, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = name.StartsWith("Br") ? Amber : new Color(0.34f, 0.7f, 0.45f, 0.85f);
            img.raycastTarget = false;
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================= staged reveal =================

        IEnumerator Reveal()
        {
            // 1. The feed comes up (a short settle, never a strobe).
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(t / 0.6f);
                yield return null;
            }
            group.alpha = 1f;

            // 2. The hall lights its windows one by one: autonomous control settling in.
            yield return new WaitForSecondsRealtime(0.25f);
            var order = new List<int>();
            for (int i = 0; i < windows.Count; i++) order.Add(i);
            // Deterministic shuffle so the sequence looks organic but never random-jarring.
            for (int i = 0; i < order.Count; i++)
            {
                int j = Mathf.Abs((i * 73 + 29) % order.Count);
                (order[i], order[j]) = (order[j], order[i]);
            }
            for (int i = 0; i < order.Count; i++)
            {
                var img = windows[order[i]];
                if (img != null) StartCoroutine(LightWindow(img, i < order.Count - 5 ? WindowAmber : WindowGreen));
                yield return new WaitForSecondsRealtime(0.055f);
            }

            // 3. The last engineer walks out through the gate and is gone.
            yield return new WaitForSecondsRealtime(0.2f);
            float walk = 0f;
            const float walkDur = 1.8f;
            Vector2 from = engineer.anchorMin, fromMax = engineer.anchorMax;
            while (walk < walkDur)
            {
                walk += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(walk / walkDur);
                float dx = k * 0.16f;
                engineer.anchorMin = new Vector2(from.x + dx, from.y);
                engineer.anchorMax = new Vector2(fromMax.x + dx, fromMax.y);
                engineerGroup.alpha = 1f - Mathf.Clamp01((k - 0.45f) / 0.55f);
                yield return null;
            }
            engineerGroup.alpha = 0f;

            // 4. Survey beam sweeps the facade once, then the status line settles.
            beamSweeping = true;
            beamStartedAt = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(1.4f);
            statusLabel.text = Loc.FinalStatusAutonomous;
        }

        IEnumerator LightWindow(Image img, Color target)
        {
            // A window flickers once as its circuit closes, then holds steady.
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.35f;
                float flicker = k < 0.5f ? (Mathf.PingPong(t * 24f, 1f) > 0.5f ? 1f : 0.25f) : 1f;
                img.color = Color.Lerp(WindowDark, target, Mathf.Clamp01(k * 1.6f) * flicker);
                yield return null;
            }
            img.color = target;
        }

        // ================= continuous life =================

        void Update()
        {
            float now = Time.unscaledTime;
            float age = now - bornAt;

            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] == null) continue;
                var c = stars[i].color;
                float tw = 0.5f + 0.5f * Mathf.Sin(now * (0.6f + i * 0.07f) + i);
                stars[i].color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.10f, 0.42f, tw));
            }

            for (int i = 0; i < steam.Count; i++)
            {
                if (steam[i] == null) continue;
                float phase = Frac((age * 0.22f) + steamPhase[i]);
                Vector2 o = steamOrigin[i];
                float y = o.y + phase * 0.12f;
                float drift = phase * 0.018f;
                steam[i].anchorMin = new Vector2(o.x + drift, y);
                steam[i].anchorMax = new Vector2(o.x + drift, y);
                steam[i].sizeDelta = new Vector2(20f + phase * 42f, 20f + phase * 42f);
                var img = steam[i].GetComponent<Image>();
                if (img != null)
                {
                    float a = Mathf.Sin(phase * Mathf.PI) * 0.16f;
                    img.color = new Color(0.75f, 0.85f, 0.82f, a);
                }
            }

            if (lampGlow != null)
            {
                float p = 0.10f + 0.10f * Mathf.PingPong(now * 0.8f, 1f);
                lampGlow.color = new Color(1f, 0.35f, 0.25f, p);
            }

            if (recDot != null)
                recDot.color = new Color(0.95f, 0.35f, 0.25f, Mathf.PingPong(now * 0.9f, 1f) > 0.45f ? 0.95f : 0.15f);

            if (beamSweeping && scanBeam != null)
            {
                float k = (now - beamStartedAt) / 2.6f;
                var img = scanBeam.GetComponent<Image>();
                if (k >= 1f)
                {
                    beamSweeping = false;
                    if (img != null) img.color = new Color(0.5f, 1f, 0.7f, 0f);
                }
                else
                {
                    scanBeam.anchorMin = new Vector2(k, 0f);
                    scanBeam.anchorMax = new Vector2(k, 1f);
                    if (img != null)
                        img.color = new Color(0.5f, 1f, 0.7f, 0.10f * Mathf.Sin(k * Mathf.PI));
                }
            }
        }
    }
}
