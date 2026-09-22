using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using LastShift.Data;

namespace LastShift.Tests
{
    /// <summary>
    /// Guards the cabinet's single control vocabulary (founder decision after the
    /// 2026-09 live playtest).
    ///
    /// Every game on the machine must name a control with the SAME word, because
    /// the controls are physically labelled with those words — the stickers are
    /// already ordered. The canon:
    ///   крутилка · жёлтая кнопка · зелёная кнопка · красная кнопка ·
    ///   датчики высоты · джойстик · кнопка меню
    /// («датчики высоты» is the group name; a hint about one sensor says
    /// «датчик высоты» — the number follows the fact.)
    ///
    /// «Последняя смена» is played with the joystick and the red button only
    /// (ArcadeInputBridge), so its text may name nothing else. Earlier builds said
    /// «ENTER — ДАЛЕЕ» and «СТРЕЛКИ ВВЕРХ/ВНИЗ»; the founder pressed the green
    /// button at the cabinet and got stuck. These tests are the tripwire against
    /// that wording coming back — through an upstream merge or a new screen.
    /// </summary>
    public class LocVocabularyTests
    {
        /// <summary>Every player-facing string of Loc, as «field → value».</summary>
        static IEnumerable<KeyValuePair<string, string>> PlayerStrings()
        {
            foreach (var f in typeof(Loc).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType == typeof(string))
                {
                    string v = (string)f.GetValue(null);
                    if (!string.IsNullOrEmpty(v)) yield return new KeyValuePair<string, string>(f.Name, v);
                }
                else if (f.FieldType == typeof(string[]))
                {
                    var arr = (string[])f.GetValue(null);
                    if (arr == null) continue;
                    for (int i = 0; i < arr.Length; i++)
                        if (!string.IsNullOrEmpty(arr[i]))
                            yield return new KeyValuePair<string, string>(f.Name + "[" + i + "]", arr[i]);
                }
            }
        }

        static string[] Words(string value) =>
            Regex.Matches(value, @"\p{L}+").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToArray();

        [Test]
        public void NoStringNamesAKeyboardOrALegacyControl()
        {
            // \b works on Cyrillic in .NET, so «СТРЕЛКИ» trips and «ПЕРЕСТРЕЛКА» would not.
            var forbidden = new Dictionary<string, string>
            {
                { @"\bСТРЕЛК", "«СТРЕЛКИ» — орган называется ДЖОЙСТИК" },
                { @"\bENTER\b", "«ENTER» — на стойке это КРАСНАЯ КНОПКА" },
                { @"\bESC\b", "«ESC» — на стойке клавиш нет; выход — КНОПКА МЕНЮ лаунчера" },
                { @"\bКЛАВИ", "клавиатуры на стойке нет" },
                { @"\bДИНАМО", "«динамо-машина» — орган называется КРУТИЛКА" },
                { @"\bПОТЕНЦИОМЕТР", "потенциометра на стойке нет" },
                { @"КНОПКА НА ДЖОЙСТИКЕ", "такой кнопки нет; на стойке это КРАСНАЯ КНОПКА" },
            };

            var offenders = new List<string>();
            foreach (var s in PlayerStrings())
                foreach (var rule in forbidden)
                    if (Regex.IsMatch(s.Value, rule.Key, RegexOptions.IgnoreCase))
                        offenders.Add(s.Key + ": " + rule.Value);

            Assert.IsEmpty(offenders,
                "Строки называют орган не по словарю автомата:\n  " + string.Join("\n  ", offenders) +
                "\nСловарь (наклейки на стойке): крутилка · жёлтая кнопка · зелёная кнопка · " +
                "красная кнопка · датчики высоты · джойстик · кнопка меню.");
        }

        [Test]
        public void EveryButtonIsNamedByItsColourOrIsTheMenuButton()
        {
            var qualifiers = new[] { "красн", "жёлт", "желт", "зелён", "зелен" };
            var offenders = new List<string>();

            foreach (var s in PlayerStrings())
            {
                string[] w = Words(s.Value);
                for (int i = 0; i < w.Length; i++)
                {
                    if (!w[i].StartsWith("кнопк")) continue;
                    bool colour = i > 0 && qualifiers.Any(q => w[i - 1].StartsWith(q));
                    bool menu = i + 1 < w.Length && w[i + 1].StartsWith("меню");
                    if (!colour && !menu) offenders.Add(s.Key + ": «" + s.Value + "»");
                }
            }

            Assert.IsEmpty(offenders,
                "Кнопка названа без цвета:\n  " + string.Join("\n  ", offenders) +
                "\nИгрок у стойки выбирает кнопку глазами: «красная кнопка», «жёлтая кнопка», " +
                "«зелёная кнопка» — или «кнопка меню». Просто «кнопка» не говорит, какая.");
        }

        [Test]
        public void JoystickAndSensorsUseTheirCanonNames()
        {
            var offenders = new List<string>();
            foreach (var s in PlayerStrings())
            {
                string[] w = Words(s.Value);
                for (int i = 0; i < w.Length; i++)
                {
                    // «стик» alone is the old name; the canon word is «джойстик».
                    if (w[i].Contains("стик") && !w[i].StartsWith("джойстик"))
                        offenders.Add(s.Key + ": «" + w[i] + "» — орган называется ДЖОЙСТИК");

                    // Sensors are «датчик(и) высоты»: the group name on the sticker is
                    // plural, a hint about one sensor is singular — but «высоты» always.
                    if (w[i].StartsWith("датчик") &&
                        !(i + 1 < w.Length && w[i + 1].StartsWith("высот")))
                        offenders.Add(s.Key + ": «" + w[i] + "» без «высоты» — орган называется ДАТЧИК(И) ВЫСОТЫ");
                }
            }

            Assert.IsEmpty(offenders, "Органы названы не по словарю:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The two controls this game actually reads must stay named in the hints —
        /// a silent screen is how the founder got stuck at the cabinet.
        /// </summary>
        [Test]
        public void TheTwoControlsThisGameUsesAreNamedInTheHints()
        {
            var all = PlayerStrings().Select(s => s.Value).ToList();
            Assert.IsTrue(all.Any(v => Regex.IsMatch(v, "КРАСНАЯ КНОПКА", RegexOptions.IgnoreCase)),
                "Ни одна строка не называет красную кнопку — а игра играется только ею и джойстиком.");
            Assert.IsTrue(all.Any(v => Regex.IsMatch(v, "ДЖОЙСТИК", RegexOptions.IgnoreCase)),
                "Ни одна строка не называет джойстик — а выбор системы делается только им.");
        }
    }
}
