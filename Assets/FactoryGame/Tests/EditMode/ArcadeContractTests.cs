using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using LastShift.Core;
using LastShift.UI;

namespace LastShift.Tests
{
    /// <summary>
    /// Guards ARCADE_INTEGRATION_CONTRACT §5 — «жизненный цикл в автомате».
    ///
    /// On the cabinet «Последняя смена» does not run as its own application: the
    /// arcade hub loads it inside the launcher process. So anything the game does
    /// to end that process ends the whole machine — the launcher, the other games,
    /// the attract loop — and the hub cannot intercept it. Leaving the game is the
    /// cabinet's «меню» touch button, which the hub polls
    /// (ArcadeInput.MenuButton → LauncherReturn); the game neither knows about it
    /// nor needs to.
    ///
    /// The upstream author keeps sending updates, and Application.Quit() has
    /// already arrived in one of them. These tests are the tripwire.
    /// </summary>
    public class ArcadeContractTests
    {
        static string GameRoot => Path.Combine(Application.dataPath, "FactoryGame");

        /// <summary>Source with comments removed: a comment that merely *talks* about
        /// quitting is documentation, not a call, and must not trip the guard.</summary>
        static string CodeOf(string file)
        {
            string text = File.ReadAllText(file);
            text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*?$", "", RegexOptions.Multiline);
        }

        static IEnumerable<string> GameScripts()
        {
            Assert.IsTrue(Directory.Exists(GameRoot), "game scripts live under Assets/FactoryGame");
            return Directory.GetFiles(GameRoot, "*.cs", SearchOption.AllDirectories)
                // These tests are the only place allowed to spell the forbidden call.
                .Where(f => !f.Replace('\\', '/').Contains("/Tests/"));
        }

        [Test]
        public void GameCode_NeverCallsApplicationQuit()
        {
            var offenders = GameScripts()
                .Where(f => CodeOf(f).Contains("Application.Quit("))
                .Select(f => Path.GetFileName(f))
                .ToList();

            Assert.IsEmpty(offenders,
                "Application.Quit() is back in: " + string.Join(", ", offenders) + ".\n" +
                "Внутри автомата игра НЕ владеет процессом: она запускается хабом в " +
                "процессе лаунчера, и Application.Quit() гасит весь автомат вместе с " +
                "лаунчером — перехватить это хаб не может.\n" +
                "Выход из игры один — сенсорная кнопка «меню» автомата, её обрабатывает " +
                "хаб (ArcadeInput.MenuButton → LauncherReturn). Игра про неё не знает.\n" +
                "Уберите вызов; чтобы перезапустить комнату, есть SceneLoader.Reload(). " +
                "См. ARCADE_INTEGRATION_CONTRACT §5.");
        }

        [Test]
        public void SceneLoader_ExposesNoQuit()
        {
            Assert.IsNull(typeof(SceneLoader).GetMethod("Quit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                "SceneLoader.Quit() вернулся. Игре нечего завершать: процессом владеет " +
                "лаунчер автомата, выход — только кнопка «меню» (контракт §5). " +
                "Переходы между сценами — Load()/Reload().");
        }

        [Test]
        public void EndRoomMenu_OffersNoWayOutOfTheGame()
        {
            Assert.IsNull(typeof(EndRoomPanel).GetField("OptionQuit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                "Пункт «выйти» вернулся в меню финальной комнаты. На стойке такого пункта " +
                "у игрока нет — выйти можно только кнопкой «меню» автомата (контракт §5). " +
                "В меню остаётся один пункт — «ПОВТОРИТЬ ЦЕХ».");
        }

        [Test]
        public void IntroFlow_HasNoQuitScreen()
        {
            var screen = typeof(IntroFlowUI).GetNestedType("Screen",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(screen, "IntroFlowUI still drives its flow through a Screen enum");
            CollectionAssert.DoesNotContain(System.Enum.GetNames(screen), "ConfirmQuit",
                "Вступление снова предлагает выйти из игры. Этого пункта на стойке нет: " +
                "процессом владеет лаунчер, выход — кнопка «меню» (контракт §5).");
        }

        /// <summary>
        /// The same class of self-governance as Application.Quit: the game is one
        /// window inside the cabinet's session and must not resize, re-flag or
        /// re-mode the display the launcher set up. Reading Screen is fine (the
        /// canvas has to scale to it) — only writing to it is the offence.
        /// </summary>
        [Test]
        public void GameCode_DoesNotOwnTheDisplay()
        {
            var forbidden = new Dictionary<string, string>
            {
                { @"Screen\.SetResolution\s*\(", "Screen.SetResolution(...)" },
                { @"Screen\.fullScreen(Mode)?\s*=(?!=)", "assignment to Screen.fullScreen" },
            };
            var offenders = new List<string>();
            foreach (string file in GameScripts())
            {
                string code = CodeOf(file);
                foreach (var rule in forbidden)
                    if (Regex.IsMatch(code, rule.Key)) offenders.Add(Path.GetFileName(file) + " → " + rule.Value);
            }

            Assert.IsEmpty(offenders,
                "Игра сама лезет в настройки экрана: " + string.Join(", ", offenders) + ".\n" +
                "Режим и разрешение окна на стойке задаёт лаунчер (1920×1080); игра — " +
                "гость в его процессе (контракт §5).");
        }
    }
}
