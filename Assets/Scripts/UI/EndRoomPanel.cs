using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// End-of-room / room-stabilized overlay, plus the final victory screen
    /// «ЗАВОД ПОБЕДИЛ» with a keyboard menu (Up/Down/Enter/Esc). Emergency red
    /// light fades into calm amber-green control light; scanlines keep the
    /// terminal feel. Input routing lives in LevelManager; this is pure view.
    /// </summary>
    public class EndRoomPanel : MonoBehaviour
    {
        GameObject panel;
        Text title;
        Text subtitle;
        Text prompt;
        Text smallLine;
        Image redWash;
        Image greenWash;

        readonly List<Text> menuTexts = new List<Text>();
        readonly List<Image> menuBgs = new List<Image>();

        public bool Visible => panel != null && panel.activeSelf;
        /// <summary>Final menu: 0 = restart run, 1 = level 1, 2 = quit. -1 when no menu.</summary>
        public int SelectedIndex { get; private set; } = -1;
        public bool HasMenu => menuTexts.Count > 0 && menuRoot != null && menuRoot.gameObject.activeSelf;

        RectTransform menuRoot;
        Coroutine calmRoutine;

        public void Build(Canvas canvas)
        {
            RectTransform rt = UIBuilder.Panel(canvas.transform, "EndRoomPanel",
                Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.015f, 0.94f));
            panel = rt.gameObject;

            // Calm-down light washes (final screen only).
            redWash = UIBuilder.Panel(rt, "RedWash", Vector2.zero, Vector2.one, new Color(1f, 0.12f, 0.06f, 0f)).GetComponent<Image>();
            redWash.raycastTarget = false;
            greenWash = UIBuilder.Panel(rt, "GreenWash", Vector2.zero, Vector2.one, new Color(0.3f, 0.9f, 0.5f, 0f)).GetComponent<Image>();
            greenWash.raycastTarget = false;

            // Terminal scanlines over the whole card.
            var scan = UIBuilder.Panel(rt, "Scanlines", Vector2.zero, Vector2.one, Color.white);
            var scanImg = scan.GetComponent<Image>();
            scanImg.sprite = TextureFactory.Scanlines();
            scanImg.type = Image.Type.Tiled;
            scanImg.pixelsPerUnitMultiplier = 0.35f;
            scanImg.color = new Color(1f, 1f, 1f, 0.35f);
            scanImg.raycastTarget = false;

            title = UIBuilder.Label(rt, "Title", "", 66, new Color(0.75f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.6f), new Vector2(1f, 0.8f));

            subtitle = UIBuilder.Label(rt, "Subtitle", "", 28, new Color(0.65f, 0.85f, 0.7f), TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 0.6f));

            smallLine = UIBuilder.Label(rt, "SmallLine", "", 18, new Color(0.45f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
            SetRect(smallLine.rectTransform, new Vector2(0f, 0.415f), new Vector2(1f, 0.455f));

            prompt = UIBuilder.Label(rt, "Prompt", "", 24, new Color(0.95f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(prompt.rectTransform, new Vector2(0f, 0.2f), new Vector2(1f, 0.38f));

            // Final keyboard menu.
            menuRoot = UIBuilder.Panel(rt, "FinalMenu", new Vector2(0.3f, 0.14f), new Vector2(0.7f, 0.4f), new Color(0f, 0f, 0f, 0f));
            string[] options = { Loc.MenuRestartRun, Loc.MenuReturnLevel1, Loc.MenuQuitGame };
            for (int i = 0; i < options.Length; i++)
            {
                var row = new GameObject("Option" + i);
                row.transform.SetParent(menuRoot, false);
                var rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f - (i + 1) / 3f);
                rowRt.anchorMax = new Vector2(1f, 1f - i / 3f);
                rowRt.offsetMin = new Vector2(0f, 4f);
                rowRt.offsetMax = new Vector2(0f, -4f);
                var bg = row.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0f);
                bg.raycastTarget = false;
                var label = UIBuilder.Label(row.transform, "Label", options[i], 26,
                    new Color(0.6f, 0.85f, 0.65f), TextAnchor.MiddleCenter);
                menuTexts.Add(label);
                menuBgs.Add(bg);
            }
            var hint = UIBuilder.Label(menuRoot, "MenuHint", Loc.MenuHint, 15,
                new Color(0.4f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, -0.22f), new Vector2(1f, 0f));
            menuRoot.gameObject.SetActive(false);

            panel.SetActive(false);
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
            Show(Loc.RoomStabilized, Loc.RoomStabilizedSub, Loc.RetryPrompt);
        }

        /// <summary>Final screen: «ЗАВОД ПОБЕДИЛ» — cold, controlled, slightly unsettling.</summary>
        public void ShowSliceComplete()
        {
            LastShift.Audio.AudioManager.OnFinalVictory();
            Show(Loc.FactoryWon, Loc.FactoryWonSub, "");
            smallLine.text = Loc.FactoryWonSmall;
            title.color = new Color(0.78f, 1f, 0.62f); // stable amber-green
            menuRoot.gameObject.SetActive(true);
            SelectedIndex = 0;
            RefreshMenu();
            if (calmRoutine != null) StopCoroutine(calmRoutine);
            calmRoutine = StartCoroutine(CalmDown());
        }

        IEnumerator CalmDown()
        {
            // Red emergency light fades out; calm green/amber control light settles in.
            float t = 0f;
            const float dur = 2.6f;
            while (t < dur && panel != null && panel.activeSelf)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float red = (1f - k) * (0.16f + 0.08f * Mathf.PingPong(t * 5f, 1f));
                redWash.color = new Color(1f, 0.12f, 0.06f, red);
                greenWash.color = new Color(0.3f, 0.9f, 0.5f, k * 0.05f);
                yield return null;
            }
            if (redWash != null) redWash.color = new Color(1f, 0.12f, 0.06f, 0f);
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
                menuBgs[i].color = sel ? new Color(0.2f, 0.45f, 0.25f, 0.35f) : new Color(0f, 0f, 0f, 0f);
            }
        }

        static string OptionLabel(int i) =>
            i == 0 ? Loc.MenuRestartRun : i == 1 ? Loc.MenuReturnLevel1 : Loc.MenuQuitGame;

        void Show(string titleText, string subtitleText, string promptText)
        {
            title.text = titleText;
            title.color = new Color(0.85f, 1f, 0.85f);
            subtitle.text = subtitleText;
            prompt.text = promptText;
            smallLine.text = "";
            menuRoot.gameObject.SetActive(false);
            SelectedIndex = -1;
            redWash.color = new Color(1f, 0.12f, 0.06f, 0f);
            greenWash.color = new Color(0.3f, 0.9f, 0.5f, 0f);
            panel.SetActive(true);
        }

        public void Hide() { if (panel != null) panel.SetActive(false); }
    }
}
