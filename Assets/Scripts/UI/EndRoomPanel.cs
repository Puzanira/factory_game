using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// Result screens on a fully opaque industrial terminal panel: nothing of the
    /// room shows through. Room won («ИНЖЕНЕР ОТСТУПИЛ» → next room), room lost
    /// («ЦЕХ СТАБИЛИЗИРОВАН») and the final victory («ЗАВОД ПОБЕДИЛ»). Defeat and
    /// final share one two-option menu («ПОВТОРИТЬ ЦЕХ» / «ВЫЙТИ ИЗ ИГРЫ»,
    /// Up/Down/Enter/Esc). Input routing lives in LevelManager; this is pure view.
    /// </summary>
    public class EndRoomPanel : MonoBehaviour
    {
        public const int OptionRepeatRoom = 0;
        public const int OptionQuit = 1;

        GameObject panel;
        Text title;
        Text subtitle;
        Text prompt;
        Text smallLine;
        Image greenWash;

        readonly List<Text> menuTexts = new List<Text>();
        readonly List<Image> menuBgs = new List<Image>();
        readonly List<List<Image>> menuEdges = new List<List<Image>>();

        public bool Visible => panel != null && panel.activeSelf;
        /// <summary>Menu: 0 = «ПОВТОРИТЬ ЦЕХ», 1 = «ВЫЙТИ ИЗ ИГРЫ». -1 when no menu.</summary>
        public int SelectedIndex { get; private set; } = -1;
        public bool HasMenu => menuTexts.Count > 0 && menuRoot != null && menuRoot.gameObject.activeSelf;

        RectTransform menuRoot;
        RectTransform card;
        Coroutine calmRoutine;
        FinalVictoryScreen finalArt;

        static readonly Color Amber = new Color(0.95f, 0.85f, 0.45f);
        static readonly Color Border = new Color(0.34f, 0.7f, 0.45f, 0.85f);

        public void Build(Canvas canvas)
        {
            // Fully opaque backdrop: the result must never read as an overlay on
            // top of live gameplay.
            RectTransform rt = UIBuilder.Panel(canvas.transform, "EndRoomPanel",
                Vector2.zero, Vector2.one, new Color(0.008f, 0.018f, 0.014f, 1f));
            panel = rt.gameObject;

            // Faint schematic so the black plate still reads as a factory terminal.
            var grid = new Color(0.35f, 0.7f, 0.48f, 0.05f);
            for (int i = 1; i < 6; i++)
                UIBuilder.Panel(rt, "GridH" + i, new Vector2(0.06f, i / 6f), new Vector2(0.94f, i / 6f + 0.0012f), grid);

            greenWash = UIBuilder.Panel(rt, "GreenWash", Vector2.zero, Vector2.one,
                new Color(0.3f, 0.9f, 0.5f, 0f)).GetComponent<Image>();
            greenWash.raycastTarget = false;

            // Terminal card with a thin border and corner brackets.
            card = UIBuilder.Panel(rt, "Card", new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f),
                new Color(0.012f, 0.032f, 0.024f, 1f));
            Frame(card, Border, 2.5f);
            Brackets(card, Amber);

            var scan = UIBuilder.Panel(card, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.3f);
            scanImg.raycastTarget = false;

            title = UIBuilder.Label(card, "Title", "", 64, new Color(0.75f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.66f), new Vector2(1f, 0.85f));
            UIBuilder.Panel(card, "TitleLine", new Vector2(0.2f, 0.645f), new Vector2(0.8f, 0.6485f),
                new Color(0.34f, 0.7f, 0.45f, 0.55f));

            subtitle = UIBuilder.Label(card, "Subtitle", "", 27, new Color(0.68f, 0.88f, 0.72f), TextAnchor.UpperCenter);
            SetRect(subtitle.rectTransform, new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.63f));

            smallLine = UIBuilder.Label(card, "SmallLine", "", 18, new Color(0.45f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
            SetRect(smallLine.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 0.495f));

            prompt = UIBuilder.Label(card, "Prompt", "", 24, Amber, TextAnchor.MiddleCenter);
            SetRect(prompt.rectTransform, new Vector2(0f, 0.2f), new Vector2(1f, 0.4f));

            // Reusable restart/quit menu (defeat screen + final victory).
            menuRoot = UIBuilder.Panel(card, "RestartQuitMenu", new Vector2(0.24f, 0.16f), new Vector2(0.76f, 0.4f),
                new Color(0f, 0f, 0f, 0f));
            string[] options = { Loc.MenuRepeatRoom, Loc.MenuQuitGame };
            for (int i = 0; i < options.Length; i++)
            {
                var row = new GameObject("Option" + i);
                row.transform.SetParent(menuRoot, false);
                var rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f - (i + 1f) / options.Length);
                rowRt.anchorMax = new Vector2(1f, 1f - (float)i / options.Length);
                rowRt.offsetMin = new Vector2(0f, 6f);
                rowRt.offsetMax = new Vector2(0f, -6f);
                var bg = row.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0f);
                bg.raycastTarget = false;

                var edges = new List<Image>();
                RectTransform frameHolder = UIBuilder.Panel(row.transform, "SelFrame", Vector2.zero, Vector2.one,
                    new Color(0f, 0f, 0f, 0f));
                Frame(frameHolder, new Color(0f, 0f, 0f, 0f), 2f);
                foreach (Transform child in frameHolder)
                {
                    var img = child.GetComponent<Image>();
                    if (img != null) edges.Add(img);
                }
                menuEdges.Add(edges);

                var label = UIBuilder.Label(row.transform, "Label", options[i], 26,
                    new Color(0.6f, 0.85f, 0.65f), TextAnchor.MiddleCenter);
                menuTexts.Add(label);
                menuBgs.Add(bg);
            }
            var hint = UIBuilder.Label(menuRoot, "MenuHint", Loc.MenuHint, 15,
                new Color(0.4f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, -0.28f), new Vector2(1f, -0.02f));
            menuRoot.gameObject.SetActive(false);

            panel.SetActive(false);
        }

        static void Frame(Transform parent, Color color, float thickness)
        {
            Edge(parent, "EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), color);
            Edge(parent, "EdgeL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), color);
            Edge(parent, "EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), color);
        }

        static void Brackets(Transform parent, Color color)
        {
            const float len = 34f, t = 3.5f;
            Edge(parent, "BrBL_h", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBL_v", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrBR_h", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(len, t), color);
            Edge(parent, "BrBR_v", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(t, len), color);
            Edge(parent, "BrTL_h", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTL_v", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(t, len), color);
            Edge(parent, "BrTR_h", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(len, t), color);
            Edge(parent, "BrTR_v", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(t, len), color);
        }

        static void Edge(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void ShowRoomComplete()
        {
            LastShift.Audio.AudioManager.OnRoomWon();
            Show(Loc.EngineerRetreated, Loc.RoomIsYours, Loc.NextRoomPrompt);
        }

        public void ShowRoomStabilized()
        {
            LastShift.Audio.AudioManager.OnRoomLost();
            Show(Loc.RoomStabilized, Loc.RoomStabilizedSub, "");
            title.color = new Color(1f, 0.55f, 0.42f); // restrained red: the factory lost
            OpenMenu();
        }

        /// <summary>
        /// Final screen after the last room: a perimeter-camera picture of the plant
        /// running itself, with «ЗАВОД ПОБЕДИЛ» and the menu underneath. Cold,
        /// controlled, slightly unsettling — never celebratory.
        /// </summary>
        public void ShowSliceComplete()
        {
            LastShift.Audio.AudioManager.OnFinalVictory();
            Show(Loc.FactoryWon, Loc.FactoryWonSub, "");
            smallLine.text = Loc.FactoryWonSmall;
            title.color = new Color(0.78f, 1f, 0.62f); // stable amber-green

            // The picture takes the upper two thirds; text and menu move below it.
            if (finalArt == null)
                finalArt = FinalVictoryScreen.Create(card, new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.95f));
            title.fontSize = 52;
            SetRect(title.rectTransform, new Vector2(0f, 0.30f), new Vector2(1f, 0.40f));
            SetRect(subtitle.rectTransform, new Vector2(0.06f, 0.205f), new Vector2(0.94f, 0.30f));
            SetRect(smallLine.rectTransform, new Vector2(0f, 0.175f), new Vector2(1f, 0.208f));
            SetRect(menuRoot, new Vector2(0.24f, 0.035f), new Vector2(0.76f, 0.165f));
            OpenMenu();
            if (calmRoutine != null) StopCoroutine(calmRoutine);
            calmRoutine = StartCoroutine(CalmDown());
        }

        /// <summary>Shows the restart/quit menu with «ПОВТОРИТЬ ЦЕХ» pre-selected.</summary>
        void OpenMenu()
        {
            menuRoot.gameObject.SetActive(true);
            SelectedIndex = OptionRepeatRoom;
            RefreshMenu();
        }

        IEnumerator CalmDown()
        {
            // Calm green/amber control light settles in behind the card.
            float t = 0f;
            const float dur = 2.2f;
            while (t < dur && panel != null && panel.activeSelf)
            {
                t += Time.unscaledDeltaTime;
                greenWash.color = new Color(0.3f, 0.9f, 0.5f, Mathf.Clamp01(t / dur) * 0.05f);
                yield return null;
            }
        }

        public void MoveSelection(int delta)
        {
            if (!HasMenu) return;
            SelectedIndex = (SelectedIndex + delta + menuTexts.Count) % menuTexts.Count;
            LastShift.Audio.UiSfx.PauseMove();
            RefreshMenu();
        }

        /// <summary>Escape jumps straight to «ВЫЙТИ ИЗ ИГРЫ».</summary>
        public void SelectQuit()
        {
            if (!HasMenu) return;
            if (SelectedIndex != menuTexts.Count - 1) LastShift.Audio.UiSfx.PauseMove();
            SelectedIndex = menuTexts.Count - 1;
            RefreshMenu();
        }

        void RefreshMenu()
        {
            for (int i = 0; i < menuTexts.Count; i++)
            {
                bool sel = i == SelectedIndex;
                menuTexts[i].text = (sel ? "> " : "") + OptionLabel(i) + (sel ? " <" : "");
                menuTexts[i].color = sel ? new Color(0.95f, 1f, 0.7f) : new Color(0.55f, 0.75f, 0.6f);
                menuTexts[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
                menuBgs[i].color = sel ? new Color(0.1f, 0.3f, 0.16f, 0.95f) : new Color(0f, 0f, 0f, 0f);
                Color edge = sel ? new Color(0.95f, 0.85f, 0.45f, 0.85f) : new Color(0f, 0f, 0f, 0f);
                foreach (var img in menuEdges[i])
                    if (img != null) img.color = edge;
            }
        }

        static string OptionLabel(int i) =>
            i == OptionRepeatRoom ? Loc.MenuRepeatRoom : Loc.MenuQuitGame;

        void Show(string titleText, string subtitleText, string promptText)
        {
            title.text = titleText;
            title.color = new Color(0.85f, 1f, 0.85f);
            subtitle.text = subtitleText;
            prompt.text = promptText;
            smallLine.text = "";
            menuRoot.gameObject.SetActive(false);
            SelectedIndex = -1;
            greenWash.color = new Color(0.3f, 0.9f, 0.5f, 0f);
            panel.SetActive(true);
        }

        public void Hide() { if (panel != null) panel.SetActive(false); }
    }
}
