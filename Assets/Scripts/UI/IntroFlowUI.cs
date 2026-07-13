using System.Collections;
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
    /// Boot-scene flow: title card «ПОСЛЕДНЯЯ СМЕНА» → four-page Russian terminal
    /// briefing → Level 1. Keyboard only (Up/Down/Enter/Esc), no EventSystem needed.
    /// Runs every new game (no PlayerPrefs). DevAdvance() lets the headless smoke
    /// test drive the flow.
    /// </summary>
    public class IntroFlowUI : MonoBehaviour
    {
        public static IntroFlowUI Instance { get; private set; }

        enum Screen { Title, Briefing, ConfirmQuit, ConfirmSkip }

        Screen current = Screen.Title;
        int menuIndex;
        int pageIndex;
        int modalIndex;
        bool loading;

        GameObject titleRoot;
        GameObject briefingRoot;
        GameObject modalRoot;
        Text modalQuestion;
        readonly List<Text> titleMenu = new List<Text>();
        readonly List<Image> titleMenuBgs = new List<Image>();
        readonly List<Text> modalMenu = new List<Text>();
        Text pageHeader;
        Text pageBody;
        Text pageFooter;
        Text pageCounter;
        CanvasGroup pageGroup;
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
            BuildBriefing(canvas.transform);
            BuildModal(canvas.transform);
            ShowScreen(Screen.Title);

            // Title mood: quiet terminal drone with distant machinery, faded in.
            AudioManager.Ensure();
            AudioManager.LoopOn("intro_drone", "intro_drone_loop", SfxBus.Music, 1f, 1.8f);
        }

        // ================= construction =================

        void BuildBackdrop(Transform root)
        {
            UIBuilder.Panel(root, "Backdrop", Vector2.zero, Vector2.one, new Color(0.015f, 0.03f, 0.025f));

            // Faint factory schematic: room outlines, conveyor routes, pipes.
            var lineColor = new Color(0.4f, 0.8f, 0.55f, 0.07f);
            var schematic = UIBuilder.Panel(root, "Schematic", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            Box(schematic, new Vector2(0.08f, 0.55f), new Vector2(0.3f, 0.85f), lineColor);
            Box(schematic, new Vector2(0.34f, 0.6f), new Vector2(0.52f, 0.82f), lineColor);
            Box(schematic, new Vector2(0.62f, 0.5f), new Vector2(0.92f, 0.88f), lineColor);
            Box(schematic, new Vector2(0.12f, 0.12f), new Vector2(0.45f, 0.4f), lineColor);
            Box(schematic, new Vector2(0.55f, 0.1f), new Vector2(0.88f, 0.35f), lineColor);
            // Conveyor routes (dashed).
            Dashes(schematic, new Vector2(0.1f, 0.47f), new Vector2(0.9f, 0.47f), 22, lineColor);
            Dashes(schematic, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.85f), 16, lineColor);
            // Pipes.
            HLine(schematic, 0.94f, lineColor);
            HLine(schematic, 0.045f, lineColor);

            // Blinking control LEDs.
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

            // CRT scanlines above everything in the backdrop.
            var scan = UIBuilder.Panel(root, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.4f);
            scanImg.raycastTarget = false;
        }

        void Box(Transform parent, Vector2 min, Vector2 max, Color c)
        {
            const float t = 0.0016f;
            UIBuilder.Panel(parent, "BoxT", new Vector2(min.x, max.y - t), new Vector2(max.x, max.y), c);
            UIBuilder.Panel(parent, "BoxB", new Vector2(min.x, min.y), new Vector2(max.x, min.y + t), c);
            UIBuilder.Panel(parent, "BoxL", new Vector2(min.x, min.y), new Vector2(min.x + t * 0.6f, max.y), c);
            UIBuilder.Panel(parent, "BoxR", new Vector2(max.x - t * 0.6f, min.y), new Vector2(max.x, max.y), c);
        }

        void Dashes(Transform parent, Vector2 from, Vector2 to, int count, Color c)
        {
            for (int i = 0; i < count; i++)
            {
                float k0 = (float)i / count;
                float k1 = k0 + 0.5f / count;
                Vector2 a = Vector2.Lerp(from, to, k0);
                Vector2 b = Vector2.Lerp(from, to, k1);
                UIBuilder.Panel(parent, "Dash",
                    new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y) - 0.0012f),
                    new Vector2(Mathf.Max(a.x, b.x) + 0.0012f, Mathf.Max(a.y, b.y) + 0.0012f), c);
            }
        }

        void HLine(Transform parent, float y, Color c) =>
            UIBuilder.Panel(parent, "HLine", new Vector2(0.03f, y - 0.0012f), new Vector2(0.97f, y + 0.0012f), c);

        void BuildTitle(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "TitleCard", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            titleRoot = rt.gameObject;

            var title = UIBuilder.Label(rt, "Title", Loc.GameTitle, 92, Phosphor, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.58f), new Vector2(1f, 0.82f));
            var latin = UIBuilder.Label(rt, "Latin", Loc.GameTitleLatin, 26, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(latin.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 0.595f));
            var sub = UIBuilder.Label(rt, "Subtitle", Loc.TitleSubtitle, 26, Amber, TextAnchor.MiddleCenter);
            SetRect(sub.rectTransform, new Vector2(0f, 0.485f), new Vector2(1f, 0.54f));
            UIBuilder.Panel(rt, "TitleLine", new Vector2(0.3f, 0.478f), new Vector2(0.7f, 0.4805f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));

            // Menu.
            string[] options = { Loc.MenuStartShift, Loc.MenuBriefing, Loc.MenuExit };
            for (int i = 0; i < options.Length; i++)
            {
                RectTransform row = UIBuilder.Panel(rt, "Option" + i,
                    new Vector2(0.36f, 0.36f - i * 0.07f), new Vector2(0.64f, 0.42f - i * 0.07f),
                    new Color(0f, 0f, 0f, 0f));
                titleMenuBgs.Add(row.GetComponent<Image>());
                var label = UIBuilder.Label(row, "Label", options[i], 27, PhosphorDim, TextAnchor.MiddleCenter);
                titleMenu.Add(label);
            }
            var hint = UIBuilder.Label(rt, "Hint", Loc.MenuHint, 16, new Color(0.4f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.12f));
        }

        void BuildBriefing(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "Briefing", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            briefingRoot = rt.gameObject;

            // Terminal card.
            RectTransform card = UIBuilder.Panel(rt, "Card", new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f),
                new Color(0.02f, 0.05f, 0.035f, 0.97f));
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.6f);
            outline.effectDistance = new Vector2(2f, 2f);
            pageGroup = card.gameObject.AddComponent<CanvasGroup>();

            pageHeader = UIBuilder.Label(card, "Header", "", 40, Amber, TextAnchor.MiddleCenter);
            SetRect(pageHeader.rectTransform, new Vector2(0f, 0.84f), new Vector2(1f, 0.97f));
            UIBuilder.Panel(card, "HeaderLine", new Vector2(0.08f, 0.835f), new Vector2(0.92f, 0.838f),
                new Color(0.9f, 0.4f, 0.3f, 0.45f)); // subtle red warning accent

            pageBody = UIBuilder.Label(card, "Body", "", 24, Phosphor, TextAnchor.UpperLeft);
            SetRect(pageBody.rectTransform, new Vector2(0.09f, 0.2f), new Vector2(0.91f, 0.8f));

            UIBuilder.Panel(card, "FooterLine", new Vector2(0.08f, 0.155f), new Vector2(0.92f, 0.158f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));
            pageFooter = UIBuilder.Label(card, "Footer", "", 24, Amber, TextAnchor.MiddleCenter);
            SetRect(pageFooter.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.15f));
            pageCounter = UIBuilder.Label(card, "Counter", "", 16, PhosphorDim, TextAnchor.LowerRight);
            SetRect(pageCounter.rectTransform, new Vector2(0.6f, 0.02f), new Vector2(0.95f, 0.09f));
        }

        void BuildModal(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "Modal", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f));
            modalRoot = rt.gameObject;
            RectTransform box = UIBuilder.Panel(rt, "Box", new Vector2(0.34f, 0.38f), new Vector2(0.66f, 0.62f),
                new Color(0.03f, 0.07f, 0.05f, 0.98f));
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.7f, 0.45f, 0.7f);
            outline.effectDistance = new Vector2(2f, 2f);

            modalQuestion = UIBuilder.Label(box, "Question", "", 26, Phosphor, TextAnchor.MiddleCenter);
            SetRect(modalQuestion.rectTransform, new Vector2(0f, 0.62f), new Vector2(1f, 0.95f));

            string[] options = { Loc.Yes, Loc.No };
            for (int i = 0; i < 2; i++)
            {
                var label = UIBuilder.Label(box, "Opt" + i, options[i], 24, PhosphorDim, TextAnchor.MiddleCenter);
                SetRect(label.rectTransform, new Vector2(0f, 0.36f - i * 0.26f), new Vector2(1f, 0.58f - i * 0.26f));
                modalMenu.Add(label);
            }
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================= flow =================

        void ShowScreen(Screen screen)
        {
            current = screen;
            titleRoot.SetActive(screen == Screen.Title);
            briefingRoot.SetActive(screen == Screen.Briefing);
            modalRoot.SetActive(screen == Screen.ConfirmQuit || screen == Screen.ConfirmSkip);
            if (screen == Screen.Title) { menuIndex = 0; RefreshTitleMenu(); }
            if (screen == Screen.ConfirmQuit || screen == Screen.ConfirmSkip)
            {
                modalQuestion.text = screen == Screen.ConfirmQuit ? Loc.ConfirmQuit : Loc.ConfirmSkip;
                modalIndex = 1; // default to «НЕТ» — safe choice
                RefreshModal();
            }
        }

        void StartBriefing()
        {
            pageIndex = 0;
            ShowScreen(Screen.Briefing);
            ApplyPage(instant: false);
        }

        void ApplyPage(bool instant)
        {
            pageHeader.text = Loc.IntroHeaders[pageIndex];
            pageBody.text = Loc.IntroBodies[pageIndex];
            pageFooter.text = pageIndex == Loc.IntroHeaders.Length - 1 ? Loc.FooterStart : Loc.FooterNext;
            pageCounter.text = (pageIndex + 1) + " / " + Loc.IntroHeaders.Length;
            if (!instant) StartCoroutine(FadePage());
        }

        IEnumerator FadePage()
        {
            if (pageGroup == null) yield break;
            float t = 0f;
            const float dur = 0.35f; // spec: max 0.5s
            pageGroup.alpha = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                pageGroup.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            pageGroup.alpha = 1f;
        }

        void LoadLevel1()
        {
            if (loading) return;
            loading = true;
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

            switch (current)
            {
                case Screen.Title:
                    if (GameInput.UpPressed) { menuIndex = (menuIndex + titleMenu.Count - 1) % titleMenu.Count; UiSfx.TerminalMove(); RefreshTitleMenu(); }
                    if (GameInput.DownPressed) { menuIndex = (menuIndex + 1) % titleMenu.Count; UiSfx.TerminalMove(); RefreshTitleMenu(); }
                    if (GameInput.ConfirmPressed) Confirm();
                    if (GameInput.EscapePressed) { UiSfx.PauseMove(); ShowScreen(Screen.ConfirmQuit); }
                    break;

                case Screen.Briefing:
                    if (GameInput.ConfirmPressed) Confirm();
                    if (GameInput.EscapePressed) { UiSfx.PauseMove(); ShowScreen(Screen.ConfirmSkip); }
                    break;

                case Screen.ConfirmQuit:
                case Screen.ConfirmSkip:
                    if (GameInput.UpPressed || GameInput.DownPressed) { modalIndex = 1 - modalIndex; UiSfx.TerminalMove(); RefreshModal(); }
                    if (GameInput.ConfirmPressed) Confirm();
                    if (GameInput.EscapePressed) { UiSfx.PauseMove(); ShowScreen(current == Screen.ConfirmSkip ? Screen.Briefing : Screen.Title); }
                    break;
            }
        }

        void Confirm()
        {
            switch (current)
            {
                case Screen.Title:
                    UiSfx.Confirm();
                    if (menuIndex == 2) ShowScreen(Screen.ConfirmQuit);
                    else StartBriefing(); // «НАЧАТЬ СМЕНУ» and «ИНСТРУКТАЖ» both open the briefing
                    break;

                case Screen.Briefing:
                    if (pageIndex < Loc.IntroHeaders.Length - 1)
                    {
                        UiSfx.PageFlip();
                        pageIndex++;
                        ApplyPage(instant: false);
                    }
                    else { UiSfx.Confirm(); LoadLevel1(); }
                    break;

                case Screen.ConfirmQuit:
                    UiSfx.Confirm();
                    if (modalIndex == 0) SceneLoader.Quit();
                    else ShowScreen(Screen.Title);
                    break;

                case Screen.ConfirmSkip:
                    UiSfx.Confirm();
                    if (modalIndex == 0) LoadLevel1();
                    else ShowScreen(Screen.Briefing);
                    break;
            }
        }

        /// <summary>Headless smoke-test hook: behaves exactly like pressing Enter.</summary>
        public void DevAdvance() => Confirm();

        void RefreshTitleMenu()
        {
            for (int i = 0; i < titleMenu.Count; i++)
            {
                bool sel = i == menuIndex;
                string baseLabel = i == 0 ? Loc.MenuStartShift : i == 1 ? Loc.MenuBriefing : Loc.MenuExit;
                titleMenu[i].text = (sel ? "> " : "") + baseLabel + (sel ? " <" : "");
                titleMenu[i].color = sel ? new Color(0.95f, 1f, 0.7f) : PhosphorDim;
                titleMenu[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
                titleMenuBgs[i].color = sel ? new Color(0.2f, 0.45f, 0.25f, 0.35f) : new Color(0f, 0f, 0f, 0f);
            }
        }

        void RefreshModal()
        {
            for (int i = 0; i < modalMenu.Count; i++)
            {
                bool sel = i == modalIndex;
                string baseLabel = i == 0 ? Loc.Yes : Loc.No;
                modalMenu[i].text = (sel ? "> " : "") + baseLabel + (sel ? " <" : "");
                modalMenu[i].color = sel ? new Color(0.95f, 1f, 0.7f) : PhosphorDim;
                modalMenu[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }
}
