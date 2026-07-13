using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastShift.Audio;
using LastShift.Data;
using LastShift.Utilities;

namespace LastShift.UI
{
    /// <summary>
    /// Keyboard-only pause overlay with two screens: the main menu
    /// (ПРОДОЛЖИТЬ / НАСТРОЙКИ ЗВУКА / ПЕРЕЗАПУСТИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ) and the
    /// Russian sound settings (master / music / SFX in 25% steps, ENTER cycles).
    /// LevelManager calls HandleInput() while paused and acts on the returned action;
    /// the legacy R / Q shortcuts keep working on the main screen.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        public enum PauseAction { None, Resume, Restart, Quit }

        GameObject panel;
        GameObject mainRoot;
        GameObject settingsRoot;
        bool settingsOpen;
        int mainIndex;
        int settingsIndex;

        readonly List<Text> mainTexts = new List<Text>();
        readonly List<Image> mainBgs = new List<Image>();
        readonly List<Text> settingsTexts = new List<Text>();
        readonly List<Image> settingsBgs = new List<Image>();

        static readonly Color Selected = new Color(0.95f, 1f, 0.7f);
        static readonly Color Idle = new Color(0.6f, 0.85f, 0.65f);
        static readonly Color RowBg = new Color(0.2f, 0.45f, 0.25f, 0.35f);
        static readonly Color RowBgOff = new Color(0f, 0f, 0f, 0f);

        public bool Visible => panel != null && panel.activeSelf;

        public void Build(Canvas canvas)
        {
            RectTransform rt = UIBuilder.Panel(canvas.transform, "PausePanel",
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.78f));
            panel = rt.gameObject;

            var title = UIBuilder.Label(rt, "Title", Loc.Paused, 64,
                new Color(0.85f, 1f, 0.85f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, 0.66f), new Vector2(1f, 0.84f));

            // ---- main menu ----
            mainRoot = new GameObject("MainMenu");
            mainRoot.transform.SetParent(rt, false);
            var mainRt = mainRoot.AddComponent<RectTransform>();
            SetRect(mainRt, new Vector2(0.32f, 0.24f), new Vector2(0.68f, 0.62f));
            string[] mainOptions = { Loc.PauseResume, Loc.SoundSettings, Loc.PauseRestart, Loc.PauseQuit };
            BuildRows(mainRt, mainOptions.Length, mainTexts, mainBgs);

            // ---- sound settings ----
            settingsRoot = new GameObject("SoundSettings");
            settingsRoot.transform.SetParent(rt, false);
            var setRt = settingsRoot.AddComponent<RectTransform>();
            SetRect(setRt, new Vector2(0.28f, 0.24f), new Vector2(0.72f, 0.62f));
            var setTitle = UIBuilder.Label(setRt, "SettingsTitle", Loc.SoundSettings, 30,
                new Color(0.95f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
            SetRect(setTitle.rectTransform, new Vector2(0f, 1.02f), new Vector2(1f, 1.24f));
            BuildRows(setRt, 4, settingsTexts, settingsBgs);

            var hint = UIBuilder.Label(rt, "Hint", Loc.PauseHint, 17,
                new Color(0.45f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
            SetRect(hint.rectTransform, new Vector2(0f, 0.14f), new Vector2(1f, 0.19f));

            panel.SetActive(false);
        }

        void BuildRows(RectTransform root, int count, List<Text> texts, List<Image> bgs)
        {
            for (int i = 0; i < count; i++)
            {
                var row = new GameObject("Row" + i);
                row.transform.SetParent(root, false);
                var rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f - (i + 1f) / count);
                rowRt.anchorMax = new Vector2(1f, 1f - (float)i / count);
                rowRt.offsetMin = new Vector2(0f, 4f);
                rowRt.offsetMax = new Vector2(0f, -4f);
                var bg = row.AddComponent<Image>();
                bg.color = RowBgOff;
                bg.raycastTarget = false;
                texts.Add(UIBuilder.Label(row.transform, "Label", "", 26, Idle, TextAnchor.MiddleCenter));
                bgs.Add(bg);
            }
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void Show()
        {
            if (panel == null) return;
            settingsOpen = false;
            mainIndex = 0;
            settingsIndex = 0;
            panel.SetActive(true);
            RefreshAll();
        }

        public void Hide() { if (panel != null) panel.SetActive(false); }

        // ---------------- input (called by LevelManager while paused) ----------------

        public PauseAction HandleInput()
        {
            if (panel == null || !panel.activeSelf) return PauseAction.Resume;
            return settingsOpen ? HandleSettingsInput() : HandleMainInput();
        }

        PauseAction HandleMainInput()
        {
            if (GameInput.EscapePressed) return PauseAction.Resume;
            if (GameInput.RestartPressed) return PauseAction.Restart;
            if (GameInput.QuitPressed) return PauseAction.Quit;

            if (GameInput.UpPressed) { mainIndex = (mainIndex + 3) % 4; UiSfx.PauseMove(); RefreshAll(); }
            if (GameInput.DownPressed) { mainIndex = (mainIndex + 1) % 4; UiSfx.PauseMove(); RefreshAll(); }

            if (GameInput.ConfirmPressed)
            {
                switch (mainIndex)
                {
                    case 0: UiSfx.PauseConfirm(); return PauseAction.Resume;
                    case 1:
                        UiSfx.PauseConfirm();
                        settingsOpen = true;
                        settingsIndex = 0;
                        RefreshAll();
                        break;
                    case 2: UiSfx.PauseConfirm(); return PauseAction.Restart;
                    default: UiSfx.PauseConfirm(); return PauseAction.Quit;
                }
            }
            return PauseAction.None;
        }

        PauseAction HandleSettingsInput()
        {
            if (GameInput.EscapePressed)
            {
                UiSfx.PauseConfirm();
                settingsOpen = false;
                RefreshAll();
                return PauseAction.None;
            }

            if (GameInput.UpPressed) { settingsIndex = (settingsIndex + 3) % 4; UiSfx.PauseMove(); RefreshAll(); }
            if (GameInput.DownPressed) { settingsIndex = (settingsIndex + 1) % 4; UiSfx.PauseMove(); RefreshAll(); }

            if (GameInput.ConfirmPressed)
            {
                switch (settingsIndex)
                {
                    case 0: AudioManager.SetMasterVolume(NextStep(AudioManager.MasterVolume01)); break;
                    case 1: AudioManager.SetMusicVolume(NextStep(AudioManager.MusicVolume01)); break;
                    case 2: AudioManager.SetSfxVolume(NextStep(AudioManager.SfxVolume01)); break;
                    default:
                        UiSfx.PauseConfirm();
                        settingsOpen = false;
                        RefreshAll();
                        return PauseAction.None;
                }
                UiSfx.PauseConfirm(); // audible at the freshly chosen volume
                RefreshAll();
            }
            return PauseAction.None;
        }

        /// <summary>Cycles 0% → 25% → 50% → 75% → 100% → 0%.</summary>
        static float NextStep(float v)
        {
            int step = Mathf.RoundToInt(Mathf.Clamp01(v) * 4f);
            return ((step + 1) % 5) / 4f;
        }

        static string Percent(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";

        // ---------------- view ----------------

        void RefreshAll()
        {
            if (mainRoot == null) return;
            mainRoot.SetActive(!settingsOpen);
            settingsRoot.SetActive(settingsOpen);

            string[] mainLabels = { Loc.PauseResume, Loc.SoundSettings, Loc.PauseRestart, Loc.PauseQuit };
            for (int i = 0; i < mainTexts.Count; i++)
                StyleRow(mainTexts[i], mainBgs[i], mainLabels[i], i == mainIndex && !settingsOpen);

            string[] settingsLabels =
            {
                Loc.VolumeMaster + ": " + Percent(AudioManager.MasterVolume01),
                Loc.VolumeMusic + ": " + Percent(AudioManager.MusicVolume01),
                Loc.VolumeSfx + ": " + Percent(AudioManager.SfxVolume01),
                Loc.SettingsBack,
            };
            for (int i = 0; i < settingsTexts.Count; i++)
                StyleRow(settingsTexts[i], settingsBgs[i], settingsLabels[i], i == settingsIndex && settingsOpen);
        }

        static void StyleRow(Text label, Image bg, string text, bool selected)
        {
            label.text = (selected ? "> " : "") + text + (selected ? " <" : "");
            label.color = selected ? Selected : Idle;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            bg.color = selected ? RowBg : RowBgOff;
        }
    }
}
