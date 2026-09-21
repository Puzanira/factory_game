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
    /// Boot-scene onboarding flow, in this exact order on every new game:
    /// title card «ПОСЛЕДНЯЯ СМЕНА» → three mandatory Russian instruction pages →
    /// «ВВОДНЫЙ УРОК» choice → interactive tutorial or Level 1.
    /// Arcade controls only (joystick up/down = navigate, Red = confirm; no
    /// back/cancel action) through the shared GameInput funnel — no EventSystem,
    /// no mouse, no PlayerPrefs. DevAdvance() lets the headless smoke test drive
    /// the flow.
    /// </summary>
    public class IntroFlowUI : MonoBehaviour
    {
        public static IntroFlowUI Instance { get; private set; }

        // No "quit the game" screen exists: on the cabinet this game runs inside
        // the launcher process, so leaving is the «меню» touch button the hub
        // owns (ARCADE_INTEGRATION_CONTRACT §5).
        enum Screen { Title, Instructions, TutorialChoice }

        Screen current = Screen.Title;
        int pageIndex;
        int choiceIndex;
        bool loading;

        GameObject titleRoot;
        GameObject choiceRoot;
        GameObject instructionRoot;
        readonly List<Text> choiceMenu = new List<Text>();
        readonly List<Image> choiceMenuBgs = new List<Image>();
        readonly List<List<Image>> choiceMenuEdges = new List<List<Image>>();
        readonly List<GameObject> pageDiagrams = new List<GameObject>();
        Text pageHeader;
        Text pageSubheader;
        Text pageBody;
        Text pageFooter;
        Text pageCounter;
        CanvasGroup pageGroup;
        readonly List<Image> leds = new List<Image>();

        static readonly Color Phosphor = new Color(0.62f, 1f, 0.72f);
        static readonly Color PhosphorDim = new Color(0.45f, 0.68f, 0.52f);
        static readonly Color Amber = new Color(0.95f, 0.85f, 0.45f);
        static readonly Color Warn = new Color(0.9f, 0.42f, 0.3f);
        static readonly Color PanelFill = new Color(0.017f, 0.045f, 0.032f, 1f);
        static readonly Color Border = new Color(0.35f, 0.7f, 0.45f, 0.75f);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var canvas = UIBuilder.CreateCanvas("IntroCanvas", 20);
            BuildBackdrop(canvas.transform);
            BuildTitle(canvas.transform);
            BuildInstructions(canvas.transform);
            BuildTutorialChoice(canvas.transform);

            ShowScreen(Screen.Title);

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

        /// <summary>Thin technical border around a panel (four hairlines, no rounding).</summary>
        static void Frame(Transform parent, Color color, float t = 0.004f)
        {
            UIBuilder.Panel(parent, "EdgeT", new Vector2(0f, 1f - t), new Vector2(1f, 1f), color);
            UIBuilder.Panel(parent, "EdgeB", new Vector2(0f, 0f), new Vector2(1f, t), color);
            UIBuilder.Panel(parent, "EdgeL", new Vector2(0f, 0f), new Vector2(t * 0.45f, 1f), color);
            UIBuilder.Panel(parent, "EdgeR", new Vector2(1f - t * 0.45f, 0f), new Vector2(1f, 1f), color);
        }

        /// <summary>Terminal corner brackets on a panel (industrial readout look).</summary>
        static void Brackets(Transform parent, Color color)
        {
            const float len = 0.06f, thick = 0.006f, tw = 0.0028f;
            // bottom-left / bottom-right / top-left / top-right
            UIBuilder.Panel(parent, "BrBL_h", new Vector2(0f, 0f), new Vector2(len, thick), color);
            UIBuilder.Panel(parent, "BrBL_v", new Vector2(0f, 0f), new Vector2(tw, len * 1.6f), color);
            UIBuilder.Panel(parent, "BrBR_h", new Vector2(1f - len, 0f), new Vector2(1f, thick), color);
            UIBuilder.Panel(parent, "BrBR_v", new Vector2(1f - tw, 0f), new Vector2(1f, len * 1.6f), color);
            UIBuilder.Panel(parent, "BrTL_h", new Vector2(0f, 1f - thick), new Vector2(len, 1f), color);
            UIBuilder.Panel(parent, "BrTL_v", new Vector2(0f, 1f - len * 1.6f), new Vector2(tw, 1f), color);
            UIBuilder.Panel(parent, "BrTR_h", new Vector2(1f - len, 1f - thick), new Vector2(1f, 1f), color);
            UIBuilder.Panel(parent, "BrTR_v", new Vector2(1f - tw, 1f - len * 1.6f), new Vector2(1f, 1f), color);
        }

        /// <summary>Selection frame for a menu row; returns the holder whose children are the edges.</summary>
        static Transform BuildRowFrame(Transform row)
        {
            RectTransform holder = UIBuilder.Panel(row, "SelFrame", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            Frame(holder, new Color(0f, 0f, 0f, 0f), 0.02f);
            return holder;
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
            SetRect(title.rectTransform, new Vector2(0f, 0.56f), new Vector2(1f, 0.8f));
            var latin = UIBuilder.Label(rt, "Latin", Loc.GameTitleLatin, 26, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(latin.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 0.575f));
            var sub = UIBuilder.Label(rt, "Subtitle", Loc.TitleSubtitle, 26, Amber, TextAnchor.MiddleCenter);
            SetRect(sub.rectTransform, new Vector2(0f, 0.465f), new Vector2(1f, 0.52f));
            UIBuilder.Panel(rt, "TitleLine", new Vector2(0.3f, 0.458f), new Vector2(0.7f, 0.4605f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));

            var footer = UIBuilder.Label(rt, "Footer", Loc.FooterContinue, 26, Amber, TextAnchor.MiddleCenter);
            SetRect(footer.rectTransform, new Vector2(0f, 0.16f), new Vector2(1f, 0.22f));
        }

        // ================= instruction pages =================

        void BuildInstructions(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "Instructions", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            instructionRoot = rt.gameObject;

            // Opaque industrial terminal card: nothing shows through it.
            RectTransform card = UIBuilder.Panel(rt, "Card", new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), PanelFill);
            pageGroup = card.gameObject.AddComponent<CanvasGroup>();
            Frame(card, Border);
            Brackets(card, Amber);
            Scanlines(card, 0.22f);

            pageHeader = UIBuilder.Label(card, "Header", "", 40, Amber, TextAnchor.MiddleLeft);
            SetRect(pageHeader.rectTransform, new Vector2(0.05f, 0.87f), new Vector2(0.72f, 0.97f));
            pageSubheader = UIBuilder.Label(card, "Subheader", "", 20, PhosphorDim, TextAnchor.MiddleLeft);
            SetRect(pageSubheader.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.72f, 0.87f));
            pageCounter = UIBuilder.Label(card, "Counter", "", 17, PhosphorDim, TextAnchor.MiddleRight);
            SetRect(pageCounter.rectTransform, new Vector2(0.62f, 0.88f), new Vector2(0.95f, 0.96f));
            UIBuilder.Panel(card, "HeaderLine", new Vector2(0.05f, 0.805f), new Vector2(0.95f, 0.8075f),
                new Color(0.35f, 0.7f, 0.45f, 0.55f));

            pageBody = UIBuilder.Label(card, "Body", "", 23, Phosphor, TextAnchor.UpperLeft);
            SetRect(pageBody.rectTransform, new Vector2(0.05f, 0.2f), new Vector2(0.55f, 0.78f));

            // Right-hand technical diagram column (one per page, toggled).
            var diagramArea = UIBuilder.Panel(card, "DiagramArea", new Vector2(0.58f, 0.18f), new Vector2(0.95f, 0.78f),
                new Color(0.01f, 0.03f, 0.022f, 1f));
            Frame(diagramArea, new Color(0.3f, 0.6f, 0.4f, 0.45f), 0.006f);
            pageDiagrams.Add(BuildDiagramRole(diagramArea));
            pageDiagrams.Add(BuildDiagramGoal(diagramArea));
            pageDiagrams.Add(BuildDiagramControls(diagramArea));

            UIBuilder.Panel(card, "FooterLine", new Vector2(0.05f, 0.135f), new Vector2(0.95f, 0.1375f),
                new Color(0.35f, 0.7f, 0.45f, 0.5f));
            pageFooter = UIBuilder.Label(card, "Footer", "", 24, Amber, TextAnchor.MiddleCenter);
            SetRect(pageFooter.rectTransform, new Vector2(0f, 0.045f), new Vector2(1f, 0.125f));
        }

        /// <summary>Page 1 diagram: factory AI core, pipes/conveyor, engineer silhouette.</summary>
        GameObject BuildDiagramRole(Transform area)
        {
            RectTransform rt = UIBuilder.Panel(area, "DiagramRole", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            var line = new Color(0.4f, 0.85f, 0.58f, 0.5f);

            // AI core: nested squares with a pulse dot.
            Box(rt, new Vector2(0.3f, 0.66f), new Vector2(0.7f, 0.92f), line);
            Box(rt, new Vector2(0.36f, 0.7f), new Vector2(0.64f, 0.88f), new Color(0.4f, 0.85f, 0.58f, 0.28f));
            var core = UIBuilder.Label(rt, "CoreLabel", "ИНТЕЛЛЕКТ\nЗАВОДА", 15, Amber, TextAnchor.MiddleCenter);
            SetRect(core.rectTransform, new Vector2(0.3f, 0.66f), new Vector2(0.7f, 0.92f));

            // Bus lines from the core down to the equipment row.
            Dashes(rt, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), 5, line);
            UIBuilder.Panel(rt, "Bus", new Vector2(0.16f, 0.492f), new Vector2(0.84f, 0.5f), line);
            Dashes(rt, new Vector2(0.2f, 0.49f), new Vector2(0.2f, 0.42f), 2, line);
            Dashes(rt, new Vector2(0.5f, 0.49f), new Vector2(0.5f, 0.42f), 2, line);
            Dashes(rt, new Vector2(0.8f, 0.49f), new Vector2(0.8f, 0.42f), 2, line);

            // Equipment row: conveyor strip, gate, arm.
            Box(rt, new Vector2(0.08f, 0.33f), new Vector2(0.32f, 0.42f), line);
            var l1 = UIBuilder.Label(rt, "L1", "ЛЕНТА", 12, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(l1.rectTransform, new Vector2(0.08f, 0.33f), new Vector2(0.32f, 0.42f));
            Box(rt, new Vector2(0.38f, 0.33f), new Vector2(0.62f, 0.42f), line);
            var l2 = UIBuilder.Label(rt, "L2", "ВОРОТА", 12, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(l2.rectTransform, new Vector2(0.38f, 0.33f), new Vector2(0.62f, 0.42f));
            Box(rt, new Vector2(0.68f, 0.33f), new Vector2(0.92f, 0.42f), line);
            var l3 = UIBuilder.Label(rt, "L3", "МАНИПУЛ.", 12, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(l3.rectTransform, new Vector2(0.68f, 0.33f), new Vector2(0.92f, 0.42f));

            // Engineer silhouette inside a restrained warning frame.
            Box(rt, new Vector2(0.38f, 0.06f), new Vector2(0.62f, 0.26f), new Color(0.9f, 0.42f, 0.3f, 0.55f));
            var head = UIBuilder.Panel(rt, "Head", new Vector2(0.475f, 0.2f), new Vector2(0.525f, 0.235f), Warn);
            head.GetComponent<Image>().sprite = TextureFactory.SoftCircle();
            UIBuilder.Panel(rt, "Torso", new Vector2(0.465f, 0.115f), new Vector2(0.535f, 0.195f), Warn);
            UIBuilder.Panel(rt, "LegL", new Vector2(0.472f, 0.08f), new Vector2(0.492f, 0.118f), Warn);
            UIBuilder.Panel(rt, "LegR", new Vector2(0.508f, 0.08f), new Vector2(0.528f, 0.118f), Warn);
            var eng = UIBuilder.Label(rt, "EngLabel", "ДЕЖУРНЫЙ ИНЖЕНЕР", 12, Warn, TextAnchor.MiddleCenter);
            SetRect(eng.rectTransform, new Vector2(0.02f, 0.0f), new Vector2(0.98f, 0.055f));

            return rt.gameObject;
        }

        /// <summary>Page 2 diagram: repair console → route → exit, with system symbols.</summary>
        GameObject BuildDiagramGoal(Transform area)
        {
            RectTransform rt = UIBuilder.Panel(area, "DiagramGoal", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            var line = new Color(0.4f, 0.85f, 0.58f, 0.5f);

            // Repair console (bottom) with a warning frame.
            Box(rt, new Vector2(0.08f, 0.12f), new Vector2(0.36f, 0.28f), new Color(0.9f, 0.42f, 0.3f, 0.6f));
            var cons = UIBuilder.Label(rt, "Console", "ПУЛЬТ", 13, Warn, TextAnchor.MiddleCenter);
            SetRect(cons.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.36f, 0.28f));
            var lbl1 = UIBuilder.Label(rt, "LabelEngineer", Loc.InstructionLabelEngineer, 12, Warn, TextAnchor.MiddleLeft);
            SetRect(lbl1.rectTransform, new Vector2(0.06f, 0.03f), new Vector2(0.98f, 0.1f));

            // Exit (top right).
            Box(rt, new Vector2(0.64f, 0.72f), new Vector2(0.92f, 0.88f), new Color(0.4f, 0.95f, 0.55f, 0.7f));
            var ex = UIBuilder.Label(rt, "Exit", "ВЫХОД", 13, new Color(0.55f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(ex.rectTransform, new Vector2(0.64f, 0.72f), new Vector2(0.92f, 0.88f));

            // Engineer route: console → mid → exit (dashed, with a blocked segment).
            Dashes(rt, new Vector2(0.22f, 0.3f), new Vector2(0.22f, 0.55f), 5, line);
            Dashes(rt, new Vector2(0.22f, 0.55f), new Vector2(0.78f, 0.55f), 11, line);
            Dashes(rt, new Vector2(0.78f, 0.57f), new Vector2(0.78f, 0.7f), 3, line);

            // Factory systems pressing on the route.
            Box(rt, new Vector2(0.42f, 0.62f), new Vector2(0.6f, 0.72f), Amber);
            var g = UIBuilder.Label(rt, "Gate", "ВОРОТА", 11, Amber, TextAnchor.MiddleCenter);
            SetRect(g.rectTransform, new Vector2(0.42f, 0.62f), new Vector2(0.6f, 0.72f));
            UIBuilder.Panel(rt, "GateDrop", new Vector2(0.505f, 0.56f), new Vector2(0.513f, 0.62f), Amber);

            Box(rt, new Vector2(0.06f, 0.62f), new Vector2(0.28f, 0.72f), Amber);
            var b = UIBuilder.Label(rt, "Belt", "КОНВЕЙЕР", 11, Amber, TextAnchor.MiddleCenter);
            SetRect(b.rectTransform, new Vector2(0.06f, 0.62f), new Vector2(0.28f, 0.72f));

            var lbl2 = UIBuilder.Label(rt, "LabelFactory", Loc.InstructionLabelFactory, 12, Amber, TextAnchor.MiddleLeft);
            SetRect(lbl2.rectTransform, new Vector2(0.06f, 0.9f), new Vector2(0.98f, 0.98f));

            return rt.gameObject;
        }

        /// <summary>Page 3 diagram: the cabinet's joystick and the red button.</summary>
        GameObject BuildDiagramControls(Transform area)
        {
            RectTransform rt = UIBuilder.Panel(area, "DiagramControls", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            var line = new Color(0.4f, 0.85f, 0.58f, 0.55f);
            var red = new Color(0.85f, 0.28f, 0.22f);

            // Joystick: gate ring with four direction stubs.
            var ring = UIBuilder.Panel(rt, "JoyRing", new Vector2(0.3f, 0.5f), new Vector2(0.56f, 0.72f),
                new Color(0.4f, 0.85f, 0.58f, 0.22f));
            ring.GetComponent<Image>().sprite = SpriteFactory.Get(PlaceholderShape.Ring);
            var knob = UIBuilder.Panel(rt, "JoyKnob", new Vector2(0.39f, 0.57f), new Vector2(0.47f, 0.65f), Amber);
            knob.GetComponent<Image>().sprite = TextureFactory.SoftCircle();
            UIBuilder.Panel(rt, "JoyUp", new Vector2(0.42f, 0.72f), new Vector2(0.44f, 0.76f), line);
            UIBuilder.Panel(rt, "JoyDn", new Vector2(0.42f, 0.46f), new Vector2(0.44f, 0.5f), line);
            UIBuilder.Panel(rt, "JoyL", new Vector2(0.26f, 0.6f), new Vector2(0.3f, 0.62f), line);
            UIBuilder.Panel(rt, "JoyR", new Vector2(0.56f, 0.6f), new Vector2(0.6f, 0.62f), line);
            var jl = UIBuilder.Label(rt, "JoyLabel", "ДЖОЙСТИК", 11, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(jl.rectTransform, new Vector2(0.24f, 0.39f), new Vector2(0.62f, 0.46f));

            var btn = UIBuilder.Panel(rt, "Button", new Vector2(0.7f, 0.54f), new Vector2(0.9f, 0.68f), red);
            btn.GetComponent<Image>().sprite = TextureFactory.SoftCircle();
            var bl = UIBuilder.Label(rt, "BtnLabel", "КРАСНАЯ КНОПКА", 11, PhosphorDim, TextAnchor.MiddleCenter);
            SetRect(bl.rectTransform, new Vector2(0.6f, 0.39f), new Vector2(1f, 0.46f));

            var note = UIBuilder.Label(rt, "Note", Loc.InstructionTacticalNote, 14, Phosphor, TextAnchor.UpperCenter);
            SetRect(note.rectTransform, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.13f));

            return rt.gameObject;
        }

        // ================= tutorial choice =================

        void BuildTutorialChoice(Transform root)
        {
            RectTransform rt = UIBuilder.Panel(root, "TutorialChoice", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            choiceRoot = rt.gameObject;

            RectTransform card = UIBuilder.Panel(rt, "Card", new Vector2(0.26f, 0.16f), new Vector2(0.74f, 0.84f), PanelFill);
            Frame(card, Border);
            Brackets(card, Amber);
            Scanlines(card, 0.22f);

            var header = UIBuilder.Label(card, "Header", Loc.TutorialChoiceHeader, 40, Amber, TextAnchor.MiddleCenter);
            SetRect(header.rectTransform, new Vector2(0f, 0.82f), new Vector2(1f, 0.94f));
            UIBuilder.Panel(card, "HeaderLine", new Vector2(0.08f, 0.805f), new Vector2(0.92f, 0.8075f),
                new Color(0.35f, 0.7f, 0.45f, 0.55f));

            var body = UIBuilder.Label(card, "Body", Loc.TutorialChoiceBody, 24, Phosphor, TextAnchor.UpperCenter);
            SetRect(body.rectTransform, new Vector2(0.06f, 0.55f), new Vector2(0.94f, 0.78f));

            string[] options = { Loc.TutorialChoiceYes, Loc.TutorialChoiceNo };
            for (int i = 0; i < options.Length; i++)
            {
                RectTransform row = UIBuilder.Panel(card, "Option" + i,
                    new Vector2(0.16f, 0.38f - i * 0.12f), new Vector2(0.84f, 0.48f - i * 0.12f),
                    new Color(0f, 0f, 0f, 0f));
                choiceMenuBgs.Add(row.GetComponent<Image>());
                // Thin technical border, lit only for the selected row.
                var edges = new List<Image>();
                foreach (Transform child in BuildRowFrame(row)) edges.Add(child.GetComponent<Image>());
                choiceMenuEdges.Add(edges);
                choiceMenu.Add(UIBuilder.Label(row, "Label", options[i], 27, PhosphorDim, TextAnchor.MiddleCenter));
            }

            var footer = UIBuilder.Label(card, "Footer", Loc.TutorialChoiceFooter, 16,
                new Color(0.45f, 0.62f, 0.5f), TextAnchor.MiddleCenter);
            SetRect(footer.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.16f));
        }

        // ================= flow =================

        void ShowScreen(Screen screen)
        {
            current = screen;
            titleRoot.SetActive(screen == Screen.Title);
            instructionRoot.SetActive(screen == Screen.Instructions);
            choiceRoot.SetActive(screen == Screen.TutorialChoice);

            if (screen == Screen.TutorialChoice)
            {
                choiceIndex = 0; // default: «ПРОЙТИ УРОК»
                RefreshChoiceMenu();
            }
        }

        void StartInstructions()
        {
            pageIndex = 0;
            ShowScreen(Screen.Instructions);
            ApplyPage();
        }

        void ApplyPage()
        {
            int total = Loc.InstructionHeaders.Length;
            pageIndex = Mathf.Clamp(pageIndex, 0, total - 1);
            pageHeader.text = Loc.InstructionHeaders[pageIndex];
            pageSubheader.text = Loc.InstructionSubheaders[pageIndex];
            pageBody.text = Loc.InstructionBodies[pageIndex];
            pageFooter.text = pageIndex == total - 1 ? Loc.FooterContinue : Loc.FooterNext;
            pageCounter.text = string.Format(Loc.InstructionCounter, pageIndex + 1, total);
            for (int i = 0; i < pageDiagrams.Count; i++)
                if (pageDiagrams[i] != null) pageDiagrams[i].SetActive(i == pageIndex);
            StartCoroutine(FadePage());
        }

        IEnumerator FadePage()
        {
            if (pageGroup == null) yield break;
            float t = 0f;
            const float dur = 0.3f;
            pageGroup.alpha = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                pageGroup.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            pageGroup.alpha = 1f;
        }

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

            switch (current)
            {
                case Screen.Title:
                    if (GameInput.ConfirmPressed) Confirm();
                    break;

                case Screen.Instructions:
                    // Red only: no Up/Down needed on the instruction pages.
                    if (GameInput.ConfirmPressed) Confirm();
                    break;

                case Screen.TutorialChoice:
                    if (GameInput.UpPressed || GameInput.DownPressed)
                    {
                        choiceIndex = 1 - choiceIndex; // two options: navigation wraps
                        UiSfx.TerminalMove();
                        RefreshChoiceMenu();
                    }
                    if (GameInput.ConfirmPressed) Confirm();
                    break;
            }
        }

        void Confirm()
        {
            switch (current)
            {
                case Screen.Title:
                    UiSfx.Confirm();
                    StartInstructions();
                    break;

                case Screen.Instructions:
                    if (pageIndex < Loc.InstructionHeaders.Length - 1)
                    {
                        UiSfx.PageFlip();
                        pageIndex++;
                        ApplyPage();
                    }
                    else
                    {
                        // The tutorial choice always comes after ALL instruction pages.
                        UiSfx.Confirm();
                        ShowScreen(Screen.TutorialChoice);
                    }
                    break;

                case Screen.TutorialChoice:
                    UiSfx.Confirm();
                    // 0 = «ПРОЙТИ УРОК» (interactive lesson first), 1 = «НАЧАТЬ СМЕНУ».
                    LoadLevel1(choiceIndex == 0);
                    break;
            }
        }

        /// <summary>Headless smoke-test hook: behaves exactly like pressing Enter.</summary>
        public void DevAdvance() => Confirm();

        /// <summary>Smoke-test introspection: is the tutorial choice on screen?</summary>
        public bool DevAtTutorialChoice => current == Screen.TutorialChoice;
        /// <summary>Currently selected tutorial-choice row (0 = «ПРОЙТИ УРОК»).</summary>
        public int DevTutorialChoiceIndex => choiceIndex;

        /// <summary>Smoke-test hook: behaves exactly like pressing Down.</summary>
        public void DevNavigateNext()
        {
            if (current == Screen.TutorialChoice)
            {
                choiceIndex = 1 - choiceIndex;
                RefreshChoiceMenu();
            }
        }

        void RefreshChoiceMenu()
        {
            for (int i = 0; i < choiceMenu.Count; i++)
            {
                bool sel = i == choiceIndex;
                string baseLabel = i == 0 ? Loc.TutorialChoiceYes : Loc.TutorialChoiceNo;
                choiceMenu[i].text = (sel ? "> " : "") + baseLabel + (sel ? " <" : "");
                choiceMenu[i].color = sel ? new Color(0.95f, 1f, 0.7f) : PhosphorDim;
                choiceMenu[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
                choiceMenuBgs[i].color = sel ? new Color(0.1f, 0.3f, 0.16f, 0.95f) : new Color(0f, 0f, 0f, 0f);
                Color edge = sel ? new Color(0.95f, 0.85f, 0.45f, 0.85f) : new Color(0f, 0f, 0f, 0f);
                foreach (var img in choiceMenuEdges[i])
                    if (img != null) img.color = edge;
            }
        }

    }
}
