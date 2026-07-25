using System;
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
    /// The short industrial victory animation that plays the moment the engineer
    /// leaves, before the result menu becomes usable: he steps beyond the exit and
    /// fades, the red emergency light dies down, the hall settles into stable green
    /// and amber, a couple of machines return to calm autonomous idle, and the
    /// terminal types two cold lines before «ЗАВОД ПОБЕДИЛ». No fanfare, no
    /// confetti, no strobe — 3.5 s, then the opaque result panel takes over.
    /// </summary>
    public class VictorySequenceController : MonoBehaviour
    {
        public bool Playing { get; private set; }

        Canvas canvas;
        Image redWash;
        Image calmWash;
        Image blackout;
        Text line1;
        Text line2;
        CanvasGroup titleGroup;

        readonly List<SpriteRenderer> engineerRenderers = new List<SpriteRenderer>();
        readonly List<float> engineerAlphas = new List<float>();
        readonly List<SpriteRenderer> idleGlows = new List<SpriteRenderer>();

        /// <summary>Runs the sequence, then hands control to <paramref name="onComplete"/>.</summary>
        public void Play(LevelManager lm, Action onComplete)
        {
            Playing = true;
            BuildUi();
            StartCoroutine(Routine(lm, onComplete));
        }

        void BuildUi()
        {
            canvas = UIBuilder.CreateCanvas("VictoryCanvas", 15);
            canvas.transform.SetParent(transform, false);

            redWash = UIBuilder.Panel(canvas.transform, "RedWash", Vector2.zero, Vector2.one,
                new Color(1f, 0.12f, 0.06f, 0.16f)).GetComponent<Image>();
            redWash.raycastTarget = false;
            calmWash = UIBuilder.Panel(canvas.transform, "CalmWash", Vector2.zero, Vector2.one,
                new Color(0.35f, 0.95f, 0.55f, 0f)).GetComponent<Image>();
            calmWash.raycastTarget = false;

            // Terminal readout strip, low on the screen so the room stays visible.
            RectTransform strip = UIBuilder.Panel(canvas.transform, "Readout",
                new Vector2(0.32f, 0.1f), new Vector2(0.96f, 0.26f), new Color(0f, 0f, 0f, 0f));
            line1 = UIBuilder.Label(strip, "Line1", "", 28, new Color(0.7f, 1f, 0.78f), TextAnchor.LowerLeft);
            SetRect(line1.rectTransform, new Vector2(0.02f, 0.52f), new Vector2(1f, 1f));
            line2 = UIBuilder.Label(strip, "Line2", "", 28, new Color(0.95f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            SetRect(line2.rectTransform, new Vector2(0.02f, 0f), new Vector2(1f, 0.48f));

            RectTransform titleRow = UIBuilder.Panel(canvas.transform, "BigTitle",
                new Vector2(0.28f, 0.5f), new Vector2(1f, 0.72f), new Color(0f, 0f, 0f, 0f));
            titleGroup = titleRow.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            UIBuilder.Label(titleRow, "Label", Loc.FactoryWon, 76,
                new Color(0.78f, 1f, 0.62f), TextAnchor.MiddleCenter);

            blackout = UIBuilder.Panel(canvas.transform, "Blackout", Vector2.zero, Vector2.one,
                new Color(0.008f, 0.018f, 0.014f, 0f)).GetComponent<Image>();
            blackout.raycastTarget = false;
        }

        static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        IEnumerator Routine(LevelManager lm, Action onComplete)
        {
            var engineer = lm != null ? lm.Engineer : null;
            Vector3 startPos = engineer != null ? engineer.transform.position : Vector3.zero;
            Vector3 exitPos = lm != null && lm.Exit != null ? lm.Exit.transform.position : startPos;
            Vector3 beyond = exitPos + (exitPos - startPos).normalized * 1.6f;
            if (engineer != null)
            {
                engineer.GetComponentsInChildren(true, engineerRenderers);
                engineerAlphas.Clear();
                foreach (var sr in engineerRenderers) engineerAlphas.Add(sr != null ? sr.color.a : 0f);
                foreach (var c in engineer.GetComponentsInChildren<Canvas>(true))
                    c.enabled = false; // his state label leaves with him
            }

            PrepareIdleGlows(lm);

            AudioManager.Play("relay_click", SfxBus.Machines, 0.7f);

            // --- phase 1: the engineer steps beyond the exit and fades out ---
            float t = 0f;
            const float leaveDur = 1.0f;
            while (t < leaveDur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / leaveDur);
                if (engineer != null)
                    engineer.transform.position = Vector3.Lerp(startPos, beyond, Mathf.SmoothStep(0f, 1f, k));
                SetEngineerAlpha(1f - k);
                // Emergency light already starts easing off while he walks out.
                redWash.color = new Color(1f, 0.12f, 0.06f, 0.16f * (1f - k * 0.5f));
                yield return null;
            }
            if (engineer != null) engineer.gameObject.SetActive(false);
            AudioManager.Play("door_close", SfxBus.Machines, 0.5f);

            // --- phase 2: warning state fades, stable green/amber light settles ---
            t = 0f;
            const float calmDur = 1.1f;
            while (t < calmDur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / calmDur);
                redWash.color = new Color(1f, 0.12f, 0.06f, 0.08f * (1f - k));
                calmWash.color = new Color(0.35f, 0.95f, 0.55f, 0.055f * k);
                for (int i = 0; i < idleGlows.Count; i++)
                    if (idleGlows[i] != null)
                        idleGlows[i].color = new Color(0.4f, 1f, 0.55f, 0.22f * k);
                yield return null;
            }
            AudioManager.Play("valve_click", SfxBus.Machines, 0.45f);

            // --- phase 3: two cold terminal lines, typed ---
            yield return TypeLine(line1, Loc.VictoryLineEngineerLeft, 0.5f);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return TypeLine(line2, Loc.VictoryLineAutonomous, 0.5f);

            // --- phase 4: final title + industrial confirmation tone ---
            AudioManager.Play("terminal_ready", SfxBus.UI, 0.7f);
            t = 0f;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                titleGroup.alpha = Mathf.Clamp01(t / 0.45f);
                yield return null;
            }
            titleGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.45f);

            // --- phase 5: fade into the opaque result screen ---
            t = 0f;
            const float fade = 0.45f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                blackout.color = new Color(0.008f, 0.018f, 0.014f, Mathf.Clamp01(t / fade));
                yield return null;
            }

            Playing = false;
            onComplete?.Invoke();
            // The result panel is opaque and covers everything: drop this canvas so
            // no victory text can bleed through the menu.
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        /// <summary>Soft green idle halo on up to two machines: the hall goes back to work.</summary>
        void PrepareIdleGlows(LevelManager lm)
        {
            if (lm == null || lm.Terminal == null) return;
            int added = 0;
            var items = lm.Terminal.Items;
            for (int i = 0; i < items.Count && added < 2; i++)
            {
                var machine = items[i].machine;
                if (machine == null) continue;
                var glow = FxFactory.Glow(machine.transform, new Color(0.4f, 1f, 0.55f, 0f),
                    new Vector2(2.6f, 2.6f), 10);
                glow.name = "AutonomousIdleGlow";
                idleGlows.Add(glow);
                added++;
            }
        }

        /// <summary>Fades him out proportionally, keeping each part's original opacity ratio.</summary>
        void SetEngineerAlpha(float k)
        {
            float f = Mathf.Clamp01(k);
            for (int i = 0; i < engineerRenderers.Count && i < engineerAlphas.Count; i++)
            {
                var sr = engineerRenderers[i];
                if (sr == null) continue;
                var c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, engineerAlphas[i] * f);
            }
        }

        IEnumerator TypeLine(Text target, string text, float duration)
        {
            if (target == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                int chars = Mathf.Clamp(Mathf.RoundToInt(text.Length * (t / duration)), 0, text.Length);
                target.text = text.Substring(0, chars);
                yield return null;
            }
            target.text = text;
        }
    }
}
