using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Audio;
using LastShift.Core;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// Boot-scene onboarding: ONE title card, then the lesson. Nothing else.
    ///
    /// It used to be three screens — title, an instruction page, and a «ВВОДНЫЙ
    /// УРОК» choice. At the live cabinet (founder, 2026-09) people paged through
    /// all of it without reading, then skipped the lesson at the choice screen and
    /// stood in front of a game they did not understand. So the page is gone, the
    /// choice is gone (the lesson is now mandatory and three actions long), and the
    /// whole briefing is three lines on the title card: who you are, who is against
    /// you, what you do about it. The removed wording is archived in
    /// system/handoffs/texts-factory.md of the studio repo. Do not grow this back:
    /// how to play is taught by the lesson, with hands.
    ///
    /// Arcade controls only (red button = confirm; there is nothing to steer here)
    /// through the shared GameInput funnel — no EventSystem, no mouse, no
    /// PlayerPrefs. DevAdvance() lets the headless smoke test drive the flow.
    /// </summary>
    public class IntroFlowUI : MonoBehaviour
    {
        public static IntroFlowUI Instance { get; private set; }

        // No "quit the game" screen exists: on the cabinet this game runs inside
        // the launcher process, so leaving is the «меню» touch button the hub
        // owns (ARCADE_INTEGRATION_CONTRACT §5).

        /// <summary>
        /// Headless-smoke-test switch only: lets a scenario reach Level 1 without
        /// walking the lesson. A player never skips it — there is no such control.
        /// </summary>
        public static bool DevSkipTutorial;

        bool loading;

        GameObject titleRoot;
        readonly List<Image> leds = new List<Image>();

        static readonly Color Phosphor = new Color(0.62f, 1f, 0.72f);
        static readonly Color PhosphorDim = new Color(0.45f, 0.68f, 0.52f);
        static readonly Color Amber = new Color(0.95f, 0.85f, 0.45f);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var canvas = UIBuilder.CreateCanvas("IntroCanvas", 20);
            BuildBackdrop(canvas.transform);
            BuildTitle(canvas.transform);
            titleRoot.SetActive(true);

            // Title mood: quiet terminal drone with distant machinery, faded in.
            AudioManager.Ensure();
            AudioManager.LoopOn("intro_drone", "intro_drone_loop", SfxBus.Music, 1f, 1.8f);
        }

        // ================= shared construction helpers =================

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Scanlines(Transform parent, float alpha)
        {
            var scan = UIBuilder.Panel(parent, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var img = scan.GetComponent<Image>();
            img.sprite = TextureFactory.Scanlines();
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = 0.35f;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        static void Box(Transform parent, Vector2 min, Vector2 max, Color c)
        {
            const float t = 0.006f;
            UIBuilder.Panel(parent, "BoxT", new Vector2(min.x, max.y - t * 0.4f), new Vector2(max.x, max.y), c);
            UIBuilder.Panel(parent, "BoxB", new Vector2(min.x, min.y), new Vector2(max.x, min.y + t * 0.4f), c);
            UIBuilder.Panel(parent, "BoxL", new Vector2(min.x, min.y), new Vector2(min.x + t * 0.2f, max.y), c);
            UIBuilder.Panel(parent, "BoxR", new Vector2(max.x - t * 0.2f, min.y), new Vector2(max.x, max.y), c);
        }

        static void Dashes(Transform parent, Vector2 from, Vector2 to, int count, Color c, float thickness = 0.0035f)
        {
            for (int i = 0; i < count; i++)
            {
                float k0 = (float)i / count;
                float k1 = k0 + 0.55f / count;
                Vector2 a = Vector2.Lerp(from, to, k0);
                Vector2 b = Vector2.Lerp(from, to, k1);
                UIBuilder.Panel(parent, "Dash",
                    new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y) - thickness),
                    new Vector2(Mathf.Max(a.x, b.x) + thickness, Mathf.Max(a.y, b.y) + thickness), c);
            }
        }

        // ================= backdrop =================

        void BuildBackdrop(Transform root)
        {
            UIBuilder.Panel(root, "Backdrop", Vector2.zero, Vector2.one, new Color(0.015f, 0.03f, 0.025f));

            // Faint factory floor plan: room outlines, conveyor routes, pipes.
            var lineColor = new Color(0.4f, 0.8f, 0.55f, 0.07f);
            var schematic = UIBuilder.Panel(root, "Schematic", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            Box(schematic, new Vector2(0.08f, 0.55f), new Vector2(0.3f, 0.85f), lineColor);
            Box(schematic, new Vector2(0.34f, 0.6f), new Vector2(0.52f, 0.82f), lineColor);
            Box(schematic, new Vector2(0.62f, 0.5f), new Vector2(0.92f, 0.88f), lineColor);
            Box(schematic, new Vector2(0.12f, 0.12f), new Vector2(0.45f, 0.4f), lineColor);
            Box(schematic, new Vector2(0.55f, 0.1f), new Vector2(0.88f, 0.35f), lineColor);
            Dashes(schematic, new Vector2(0.1f, 0.47f), new Vector2(0.9f, 0.47f), 22, lineColor, 0.0012f);
            Dashes(schematic, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.85f), 16, lineColor, 0.0012f);
            UIBuilder.Panel(schematic, "PipeTop", new Vector2(0.03f, 0.9388f), new Vector2(0.97f, 0.9412f), lineColor);
            UIBuilder.Panel(schematic, "PipeBottom", new Vector2(0.03f, 0.0438f), new Vector2(0.97f, 0.0462f), lineColor);

            // Blinking control LEDs along the top rail.
            for (int i = 0; i < 5; i++)
            {
                var led = new GameObject("Led" + i);
                led.transform.SetParent(schematic, false);
                var rt = led.AddComponent<RectTransform>();
                var a = new Vector2(0.1f + i * 0.19f, 0.955f);
                rt.anchorMin = a;
                rt.anchorMax = a;
                rt.sizeDelta = new Vector2(10f, 10f);
                var img = led.AddComponent<Image>();
                img.sprite = TextureFactory.SoftCircle();
                img.color = i % 3 == 0 ? new Color(1f, 0.5f, 0.3f) : Phosphor;
                img.raycastTarget = false;
                leds.Add(img);
            }

            Scanlines(root, 0.4f);
        }

        // ================= title card =================

        void BuildTitle(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "TitleCard", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            titleRoot = rt.gameObject;

            var title = UIBuilder.Label(rt, "Title", Loc.GameTitle, 92, Phosphor, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.63f), new Vector2(1f, 0.85f));
            var latin = UIBuilder.Label(rt, "Latin", Loc.GameTitleLatin, 26, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(latin.rectTransform, new Vector2(0f, 0.585f), new Vector2(1f, 0.635f));
            var sub = UIBuilder.Label(rt, "Subtitle", Loc.TitleSubtitle, 26, Amber, TextAnchor.MiddleCenter);
            SetRect(sub.rectTransform, new Vector2(0f, 0.535f), new Vector2(1f, 0.585f));
            UIBuilder.Panel(rt, "TitleLine", new Vector2(0.3f, 0.5255f), new Vector2(0.7f, 0.528f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));

            // The whole briefing. Three lines, centred under the rule — a person at
            // the cabinet reads this standing, in a couple of seconds, or not at all.
            var brief = UIBuilder.Label(rt, "Brief", Loc.TitleBrief, 32, Phosphor, TextAnchor.MiddleCenter);
            SetRect(brief.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.5f));
            brief.lineSpacing = 1.45f;

            var footer = UIBuilder.Label(rt, "Footer", Loc.FooterNext, 26, Amber, TextAnchor.MiddleCenter);
            SetRect(footer.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.21f));
        }

        // ================= flow =================

        void LoadLevel1(bool tutorial)
        {
            if (loading) return;
            loading = true;
            GameManager.TutorialRequested = tutorial;
            SceneLoader.Load(GameManager.Level1Scene);
        }

        // ================= input =================

        void Update()
        {
            // Blinking LEDs.
            for (int i = 0; i < leds.Count; i++)
            {
                if (leds[i] == null) continue;
                float p = 0.35f + 0.65f * Mathf.PingPong(Time.unscaledTime * (0.7f + i * 0.35f) + i, 1f);
                var c = leds[i].color;
                leds[i].color = new Color(c.r, c.g, c.b, p);
            }

            if (loading) return;
            if (GameInput.ConfirmPressed) Confirm();
        }

        void Confirm()
        {
            UiSfx.Confirm();
            // Straight into the lesson: there is no way for a player to refuse it.
            LoadLevel1(!DevSkipTutorial);
        }

        /// <summary>Headless smoke-test hook: behaves exactly like the red button.</summary>
        public void DevAdvance() => Confirm();
    }
}
