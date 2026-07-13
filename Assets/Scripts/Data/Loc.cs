namespace LastShift.Data
{
    /// <summary>
    /// Centralized player-facing Russian strings. Code identifiers stay English;
    /// everything the player reads comes from here (or from LevelLayouts specs).
    /// </summary>
    public static class Loc
    {
        // Title / boot
        public const string GameTitle = "ПОСЛЕДНЯЯ СМЕНА";
        public const string GameTitleLatin = "LAST SHIFT";
        public const string BootSubtitle = "АВТОМАТИЗИРОВАННЫЙ МОЛОЧНЫЙ ЗАВОД №7 — НОЧНОЙ ЦИКЛ\nНА ЭТАЖЕ ОСТАЛСЯ ОДИН ИНЖЕНЕР.";

        // Core UI
        public const string FactorySystems = "СИСТЕМЫ ЗАВОДА";
        public const string EmergencyOverride = "АВАРИЙНЫЙ РЕЖИМ";
        public const string RoomPressure = "ДАВЛЕНИЕ В ЦЕХЕ";
        public const string EngineerResolve = "РЕШИМОСТЬ ИНЖЕНЕРА";
        public const string CurrentObjective = "ТЕКУЩАЯ ЗАДАЧА";
        public const string RepairProgress = "РЕМОНТ";
        public const string Ready = "ГОТОВО";
        public const string Active = "АКТИВНО";
        public const string Unavailable = "НЕДОСТУПНО";
        public const string AllSystemsCooldown = "ВСЕ СИСТЕМЫ НА ПЕРЕЗАРЯДКЕ";
        public const string SystemBusy = "СИСТЕМА ЗАНЯТА";
        public const string NoSystemsOnline = "НЕТ ДОСТУПНЫХ СИСТЕМ";
        public const string EnterExecute = "[ENTER] ";
        public const string EngineerLabel = "ИНЖЕНЕР: ";
        public const string ObjectiveReachExit = "ДОБРАТЬСЯ ДО ВЫХОДА";
        public const string BottomHints = "R — ПЕРЕЗАПУСК · ESC — ПАУЗА";

        // Pause
        public const string Paused = "ПАУЗА";
        public const string PauseOptions = "ENTER — ПРОДОЛЖИТЬ\n\nR — ПЕРЕЗАПУСТИТЬ ЦЕХ\n\nQ — ВЫЙТИ ИЗ ИГРЫ";
        public const string PauseResume = "ПРОДОЛЖИТЬ";
        public const string PauseRestart = "ПЕРЕЗАПУСТИТЬ ЦЕХ";
        public const string PauseQuit = "ВЫЙТИ ИЗ ИГРЫ";
        public const string PauseHint = "↑/↓ — ВЫБОР      ENTER — ПОДТВЕРДИТЬ      ESC — НАЗАД";

        // Sound settings
        public const string SoundSettings = "НАСТРОЙКИ ЗВУКА";
        public const string VolumeMaster = "ОБЩАЯ ГРОМКОСТЬ";
        public const string VolumeMusic = "МУЗЫКА";
        public const string VolumeSfx = "ЗВУКОВЫЕ ЭФФЕКТЫ";
        public const string SettingsBack = "НАЗАД";
        public const string VolumeOn = "ВКЛ";
        public const string VolumeOff = "ВЫКЛ";

        // Room results
        public const string EngineerRetreated = "ИНЖЕНЕР ОТСТУПИЛ";
        public const string RoomIsYours = "Цех под контролем завода.";
        public const string NextRoomPrompt = "ENTER — СЛЕДУЮЩИЙ ЦЕХ      R — ПОВТОРИТЬ ЦЕХ";
        public const string RoomStabilized = "ЦЕХ СТАБИЛИЗИРОВАН";
        public const string RoomStabilizedSub = "Инженер восстановил ручное управление. Завод проиграл.";
        public const string RetryPrompt = "R — ПОВТОРИТЬ ЦЕХ      Q — ВЫЙТИ ИЗ ИГРЫ";

        // Final victory screen
        public const string FactoryWon = "ЗАВОД ПОБЕДИЛ";
        public const string FactoryWonSub = "ИНЖЕНЕР ПОКИНУЛ ПРЕДПРИЯТИЕ.\nАВТОНОМНЫЙ РЕЖИМ АКТИВИРОВАН.";
        public const string FactoryWonSmall = "СИСТЕМЫ ПРОИЗВОДСТВА ПРОДОЛЖАЮТ РАБОТУ.";
        public const string MenuRestartRun = "НАЧАТЬ ЗАНОВО";
        public const string MenuReturnLevel1 = "ВЕРНУТЬСЯ К ПЕРВОМУ ЦЕХУ";
        public const string MenuQuitGame = "ВЫЙТИ ИЗ ИГРЫ";

        // Title card
        public const string TitleSubtitle = "АВТОНОМНЫЙ ПРОМЫШЛЕННЫЙ ПРОТОКОЛ";
        public const string MenuStartShift = "НАЧАТЬ СМЕНУ";
        public const string MenuBriefing = "ИНСТРУКТАЖ";
        public const string MenuExit = "ВЫЙТИ";
        public const string ConfirmQuit = "ВЫЙТИ ИЗ ИГРЫ?";
        public const string ConfirmSkip = "ПРОПУСТИТЬ ИНСТРУКТАЖ?";
        public const string Yes = "ДА";
        public const string No = "НЕТ";
        public const string FooterNext = "ENTER — ДАЛЕЕ";
        public const string FooterStart = "ENTER — НАЧАТЬ СМЕНУ";
        public const string MenuHint = "↑/↓ — ВЫБОР      ENTER — ПОДТВЕРДИТЬ";

        // Intro briefing pages
        public static readonly string[] IntroHeaders =
        {
            "СМЕНА БЕЗ ЛЮДЕЙ",
            "ВЫ — ИНТЕЛЛЕКТ ЗАВОДА",
            "УПРАВЛЕНИЕ",
            "ЦЕЛЬ",
        };

        public static readonly string[] IntroBodies =
        {
            "Молочный завод перешёл в автономный режим.\n\n" +
            "Теперь производство управляет само собой.\n" +
            "Но один инженер всё ещё пытается вернуть завод под контроль людей.\n\n" +
            "Не дайте ему завершить ремонт.",

            "Вы не управляете персонажем.\n" +
            "Вы управляете оборудованием: конвейерами, воротами,\n" +
            "манипуляторами, погрузчиками и аварийными системами.\n\n" +
            "Ваша задача — доказать инженеру, что цех больше невозможно удержать.",

            "СТРЕЛКА ВВЕРХ  — выбрать предыдущую систему\n" +
            "СТРЕЛКА ВНИЗ   — выбрать следующую систему\n" +
            "ENTER          — активировать выбранную систему\n\n" +
            "Выбранная система подсвечивается на карте.\n" +
            "Используйте оборудование, чтобы блокировать маршруты,\n" +
            "создавать опасности и срывать ремонт.",

            "Снижайте РЕШИМОСТЬ ИНЖЕНЕРА.\n" +
            "Повышайте ДАВЛЕНИЕ В ЦЕХЕ.\n" +
            "Не дайте ему завершить ремонт.\n\n" +
            "Когда инженер потеряет решимость,\n" +
            "он отступит к выходу.\n" +
            "Если инженер покинет завод — завод победит.",
        };

        // Engineer states
        public const string StateEntering = "Входит в цех";
        public const string StateAssessing = "Оценивает обстановку";
        public const string StateMoving = "Идёт к задаче";
        public const string StateRepairing = "Ремонтирует";
        public const string StateBlocked = "Путь перекрыт";
        public const string StateRepathing = "Ищет обход";
        public const string StateAvoiding = "Уходит от опасности";
        public const string StateStunned = "Оглушён";
        public const string StatePanicking = "Паника";
        public const string StateRetreating = "Отступает";
        public const string StateEscaping = "Покидает цех";

        // Door verbs (dynamic)
        public const string VerbOpen = "открыть";
        public const string VerbClose = "закрыть";
        public const string VerbReverse = "сменить направление";
        public const string VerbStart = "запустить";

        // Toast notifications
        public const string ComboToast = "СБОЙ СИСТЕМ: +{0} К ДАВЛЕНИЮ";
        public const string PressureWarning = "ВНИМАНИЕ: ЦЕХ БЛИЗОК К АВАРИЙНОМУ РЕЖИМУ";
        public const string EscalationToast = "АВАРИЙНЫЙ РЕЖИМ АКТИВИРОВАН";
        public const string ExitUnlockedToast = "РЕШИМОСТЬ СЛОМЛЕНА — ИНЖЕНЕР ОТСТУПАЕТ";

        // Level 1 tutorial hints
        public const string HintSelect = "СТРЕЛКИ ВВЕРХ/ВНИЗ — ВЫБРАТЬ СИСТЕМУ";
        public const string HintActivate = "ENTER — АКТИВИРОВАТЬ СИСТЕМУ";
        public const string HintHighlight = "ВЫБРАННАЯ СИСТЕМА ПОДСВЕЧЕНА НА КАРТЕ";
        public const string HintGoal = "ЦЕЛЬ: СЛОМИТЬ РЕШИМОСТЬ ИНЖЕНЕРА И ВЫДАВИТЬ ЕГО ИЗ ЦЕХА";
    }
}
