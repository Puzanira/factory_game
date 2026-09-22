using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using LastShift.Data;

namespace LastShift.Tests
{
    /// <summary>
    /// Guards the 2026-09 onboarding rewrite.
    ///
    /// The live-cabinet playtest said it plainly: «игрок не успевает вообще
    /// понимать, что происходит», and «наши люди на плейтесте вообще туда не
    /// смотрели». The cure was to say less, name the control every time, and let
    /// the player do the thing instead of reading about it. Text grows back by
    /// itself — through an upstream merge, through a well-meant clarification —
    /// so the rules are nailed down here:
    ///
    ///   · the red button has exactly two verbs: «ДАЛЕЕ» on a screen,
    ///     «ВКЛЮЧИТЬ» in the room. It used to have five.
    ///   · every lesson prompt names the control to press. It used to say
    ///     «ВЫПОЛНИТЕ ДЕЙСТВИЕ» to a person who did not know what to press.
    ///   · a lesson step is two short lines, not a paragraph.
    ///   · the intro is one screen: no instruction pages, no «пройти урок /
    ///     начать смену» choice (the lesson is mandatory).
    ///
    /// See also LocVocabularyTests, which guards what the controls are CALLED.
    /// </summary>
    public class OnboardingTextTests
    {
        static IEnumerable<KeyValuePair<string, string>> PlayerStrings()
        {
            foreach (var f in typeof(Loc).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(string)) continue;
                string v = (string)f.GetValue(null);
                if (!string.IsNullOrEmpty(v)) yield return new KeyValuePair<string, string>(f.Name, v);
            }
        }

        static string Value(string field)
        {
            var f = typeof(Loc).GetField(field, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(f, "Loc." + field + " исчезла — подсказка урока без неё не соберётся");
            return (string)f.GetValue(null);
        }

        [Test]
        public void TheRedButtonSpeaksTwoVerbsAndNoMore()
        {
            var allowed = new[] { "ДАЛЕЕ", "ВКЛЮЧИТЬ" };
            var offenders = new List<string>();

            foreach (var s in PlayerStrings())
                foreach (Match m in Regex.Matches(s.Value, @"КРАСНАЯ КНОПКА\s*—\s*([А-ЯЁ]+)"))
                    if (!allowed.Contains(m.Groups[1].Value))
                        offenders.Add(s.Key + ": «" + m.Value + "»");

            Assert.IsEmpty(offenders,
                "У красной кнопки снова больше двух глаголов:\n  " + string.Join("\n  ", offenders) +
                "\nНа экране — «КРАСНАЯ КНОПКА — ДАЛЕЕ», в цехе — «КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ». " +
                "Раньше их было пять («ПРОДОЛЖИТЬ», «ПОДТВЕРДИТЬ», «СЛЕДУЮЩИЙ ЦЕХ», " +
                "«АКТИВИРОВАТЬ СИСТЕМУ»), и человек у стойки каждый раз читал заново.");
        }

        [Test]
        public void EveryLessonPromptNamesTheControlToPress()
        {
            var prompts = new[] { "TutStep1Footer", "TutActFooter", "TutStep3Footer" };
            var offenders = new List<string>();

            foreach (string name in prompts)
            {
                string v = Value(name);
                if (!Regex.IsMatch(v, "ДЖОЙСТИК|КРАСНАЯ КНОПКА"))
                    offenders.Add(name + ": «" + v + "»");
            }

            // The wording that made this test necessary must never come back.
            foreach (var s in PlayerStrings())
                if (Regex.IsMatch(s.Value, @"^ВЫПОЛНИТЕ ДЕЙСТВИЕ\.?$"))
                    offenders.Add(s.Key + ": «" + s.Value + "» — не сказано, что нажимать");

            Assert.IsEmpty(offenders,
                "Подсказка урока не называет орган:\n  " + string.Join("\n  ", offenders) +
                "\nНа этом шаге игрок как раз и не знает, что нажать: подсказка обязана " +
                "назвать джойстик или красную кнопку.");
        }

        [Test]
        public void ALessonStepIsTwoShortLinesNotAParagraph()
        {
            const int MaxLines = 2, MaxChars = 90;
            var offenders = new List<string>();

            foreach (string name in new[] { "TutStep1Body", "TutStep2Body", "TutStep3Body" })
            {
                string[] lines = Value(name).Split('\n');
                if (lines.Length > MaxLines)
                    offenders.Add(name + ": строк " + lines.Length + ", можно " + MaxLines);
                foreach (string line in lines)
                    if (line.Length > MaxChars)
                        offenders.Add(name + ": строка длиной " + line.Length + " — «" + line + "»");
            }

            Assert.IsEmpty(offenders,
                "Шаг урока снова вырос в документацию:\n  " + string.Join("\n  ", offenders) +
                "\nУрок проходят стоя у автомата и не вчитываясь: две короткие строки на шаг.");
        }

        [Test]
        public void TheIntroIsOneScreenAndTheLessonIsMandatory()
        {
            var gone = new Dictionary<string, string>
            {
                { "InstructionBodies",   "страница-инструкция" },
                { "InstructionHeaders",  "страница-инструкция" },
                { "InstructionCounter",  "счётчик страниц инструкции" },
                { "TutorialChoiceYes",   "экран выбора «пройти урок / начать смену»" },
                { "TutorialChoiceNo",    "экран выбора «пройти урок / начать смену»" },
                { "TutorialChoiceBody",  "экран выбора «пройти урок / начать смену»" },
            };

            var offenders = (from kv in gone
                             where typeof(Loc).GetField(kv.Key, BindingFlags.Public | BindingFlags.Static) != null
                             select kv.Key + " (" + kv.Value + ")").ToList();

            Assert.IsEmpty(offenders,
                "В игру вернулись экраны, вырезанные после плейтеста: " + string.Join(", ", offenders) + ".\n" +
                "До игры один экран чтения — титр. Урок обязателен: выбирать «начать смену» " +
                "в обход урока игроку больше не предлагают, потому что так делали все.");

            Assert.IsNotNull(typeof(Loc).GetField("TitleBrief", BindingFlags.Public | BindingFlags.Static),
                "Loc.TitleBrief исчезла — на титре не осталось ориентировки, а другого " +
                "места, где игра говорит «кто вы», больше нет.");
        }
    }
}
