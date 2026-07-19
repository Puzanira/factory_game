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
        public const string BottomHints = "ESC — ПАУЗА";

        // Pause
        public const string Paused = "ПАУЗА";
        public const string PauseResume = "ПРОДОЛЖИТЬ";
        public const string PauseRestart = "ПОВТОРИТЬ ЦЕХ";
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
        public const string NextRoomPrompt = "ENTER — СЛЕДУЮЩИЙ ЦЕХ";
        public const string RoomStabilized = "ЦЕХ СТАБИЛИЗИРОВАН";
        public const string RoomStabilizedSub = "Инженер восстановил ручное управление. Завод проиграл.";

        // Final victory screen
        public const string FactoryWon = "ЗАВОД ПОБЕДИЛ";
        public const string FactoryWonSub = "ИНЖЕНЕР ПОКИНУЛ ПРЕДПРИЯТИЕ.\nАВТОНОМНЫЙ РЕЖИМ АКТИВИРОВАН.";
        public const string FactoryWonSmall = "СИСТЕМЫ ПРОИЗВОДСТВА ПРОДОЛЖАЮТ РАБОТУ.";
        public const string MenuRepeatRoom = "ПОВТОРИТЬ ЦЕХ";
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
        public const string StateBackingOff = "Отходит";
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

        // ---------------- tactical layer ----------------

        // Machine purposes (main line + tactical hint)
        public const string PurposeDoorClose = "ПЕРЕКРЫВАЕТ МАРШРУТ";
        public const string PurposeDoorCloseHint = "ЗАСТАВЛЯЕТ ИНЖЕНЕРА ИСКАТЬ ОБХОД";
        public const string PurposeDoorOpen = "ОТКРЫВАЕТ ПРОХОД";
        public const string PurposeDoorOpenHint = "ВОЗВРАЩАЕТ МАРШРУТ ИНЖЕНЕРУ";
        public const string PurposeConveyor = "СМЕЩАЕТ ИНЖЕНЕРА ПО ЛИНИИ";
        public const string PurposeConveyorHint = "ПОДВОДИТ К ОПАСНОЙ ЗОНЕ";
        public const string PurposeArm = "ОГЛУШАЕТ ИНЖЕНЕРА В РАДИУСЕ ДЕЙСТВИЯ";
        public const string PurposeArmHint = "ЭФФЕКТИВЕН, КОГДА ЦЕЛЬ РЯДОМ";
        public const string PurposeMobileUnit = "ЗАНИМАЕТ ПРОХОД";
        public const string PurposeMobileUnitHint = "ВЫТЕСНЯЕТ ИНЖЕНЕРА ИЗ ЗОНЫ";
        public const string PurposeDrone = "ОТМЕЧАЕТ ИНЖЕНЕРА";
        public const string PurposeDroneHint = "УСИЛИВАЕТ СЛЕДУЮЩЕЕ ВОЗДЕЙСТВИЕ";
        public const string PurposeSteam = "ВЫНУЖДАЕТ ИНЖЕНЕРА ОТСТУПИТЬ";
        public const string PurposeSteamHint = "ВРЕМЕННО ЗАКРЫВАЕТ ЗОНУ";
        public const string PurposeCold = "ЗАМЕДЛЯЕТ ИНЖЕНЕРА";
        public const string PurposeColdHint = "ДЕЛАЕТ ЕГО УЯЗВИМЕЕ ДЛЯ ДРУГИХ СИСТЕМ";
        public const string PurposeSlippery = "ЗАМЕДЛЯЕТ ИНЖЕНЕРА";
        public const string PurposeSlipperyHint = "МОКРЫЙ ПОЛ СНИЖАЕТ СКОРОСТЬ";
        public const string PurposeAlarm = "ДАВИТ НА НЕРВЫ ВО ВСЁМ ЦЕХЕ";
        public const string PurposeAlarmHint = "УСКОРЯЕТ ПАНИКУ ИНЖЕНЕРА";
        public const string PurposeScanner = "ПОМЕЧАЕТ ЦЕЛЬ В ЗОНЕ СКАНИРОВАНИЯ";
        public const string PurposeScannerHint = "ЭФФЕКТИВЕН, КОГДА ЦЕЛЬ В ПРОХОДЕ";

        // Effective-zone status under the selected command
        public const string TargetInZone = "ЦЕЛЬ В ЗОНЕ ВОЗДЕЙСТВИЯ";
        public const string TargetOutOfZone = "ЦЕЛЬ ВНЕ ЗОНЫ — ЭФФЕКТ БУДЕТ СЛАБЫМ";
        public const string InsightDoorBlock = "ПЕРЕКРЫТИЕ МАРШРУТА: ИНЖЕНЕРУ ПРИДЁТСЯ ИСКАТЬ ОБХОД";

        // Command availability states
        public const string StateGoodMoment = "ПОДХОДЯЩИЙ МОМЕНТ";
        public const string StateLowValue = "СЕЙЧАС НЕЭФФЕКТИВНО";

        // Activation outcome feedback
        public const string EffectiveActivation = "ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ";
        public const string WastedActivation = "СИСТЕМА СРАБОТАЛА ВПУСТУЮ";
        public const string WastedActivationSub = "ЭФФЕКТ НЕ ДОСТИГ ЦЕЛИ";
        public const string PressureGainToast = "ДАВЛЕНИЕ +{0}";
        public const string ResolveLossToast = "РЕШИМОСТЬ −{0}";

        // Control resource
        public const string ControlResource = "РЕСУРС УПРАВЛЕНИЯ";
        public const string NotEnoughResource = "НЕДОСТАТОЧНО РЕСУРСА УПРАВЛЕНИЯ";
        public const string ResourceRestored = "ЭФФЕКТИВНОЕ УПРАВЛЕНИЕ ВОССТАНАВЛИВАЕТ РЕСУРС";

        // Combinations
        public const string ComboRedirect = "КОМБИНАЦИЯ: ПЕРЕНАПРАВЛЕНИЕ";
        public const string ComboLineGrab = "КОМБИНАЦИЯ: ЗАХВАТ ЛИНИИ";
        public const string ComboMarkedTarget = "КОМБИНАЦИЯ: ЦЕЛЬ ПОД КОНТРОЛЕМ";
        public const string ComboDisplacement = "КОМБИНАЦИЯ: ВЫТЕСНЕНИЕ";
        public const string ComboBonusToast = "КОМБИНАЦИЯ +{0}";
        public const string ComboHintConveyor = "ВОЗМОЖНА КОМБИНАЦИЯ: НАПРАВЬТЕ ИНЖЕНЕРА КОНВЕЙЕРОМ";
        public const string ComboHintArm = "ВОЗМОЖНА КОМБИНАЦИЯ: ПОДВЕДИТЕ ИНЖЕНЕРА К МАНИПУЛЯТОРУ";
        public const string ComboHintMarked = "ЦЕЛЬ ОТМЕЧЕНА: МАНИПУЛЯТОР И ОПАСНЫЕ ЗОНЫ УСИЛЕНЫ";
        public const string ComboHintCutRetreat = "ВОЗМОЖНА КОМБИНАЦИЯ: ПЕРЕКРОЙТЕ ПУТЬ ОТСТУПЛЕНИЯ";

        // Tutorial choice screen
        public const string TutorialChoiceHeader = "ПЕРВЫЙ ЗАПУСК";
        public const string TutorialChoiceBody = "Нужен вводный урок по управлению заводом?";
        public const string TutorialChoiceYes = "ПРОЙТИ УРОК";
        public const string TutorialChoiceNo = "СРАЗУ К СМЕНЕ";
        public const string TutorialChoiceFooter = "СТРЕЛКА ВВЕРХ / ВНИЗ — ВЫБОР\nENTER — ПОДТВЕРДИТЬ";

        // Interactive tutorial
        public const string TutorialRoomName = "УЧЕБНЫЙ ЦЕХ";
        public const string TutorialStep1Header = "1. ВЫБОР СИСТЕМЫ";
        public const string TutorialStep1Body =
            "Инженер чинит ремонтные пульты (красные панели). Если он завершит\n" +
            "все ремонты — цех стабилизирован, это победа инженера.\n" +
            "Стрелками выберите систему. Нажмите ENTER, чтобы активировать её.";
        public const string TutorialStep1Done = "СИСТЕМА АКТИВИРОВАНА";
        public const string TutorialStep2Header = "2. НЕ ТРАТЬТЕ СИСТЕМЫ ВПУСТУЮ";
        public const string TutorialStep2Body =
            "У инженера есть РЕШИМОСТЬ: удары и опасности снижают её.\n" +
            "Упадёт до нуля — инженер отступит, и цех останется заводу.\n" +
            "Дождитесь, пока инженер войдёт в зону, и только затем активируйте манипулятор.";
        public const string TutorialStep2Early = "ЦЕЛЬ ВНЕ ЗОНЫ — ПОДОЖДИТЕ";
        public const string TutorialRetry = "СИТУАЦИЯ ПОВТОРЯЕТСЯ — ПОПРОБУЙТЕ ЕЩЁ РАЗ";
        public const string TutorialStep3Header = "3. ПОДГОТОВЬТЕ ЛОВУШКУ";
        public const string TutorialStep3Body =
            "Закройте ворота — инженеру придётся идти в обход через конвейер.\n" +
            "Пока инженер на ленте, смените направление конвейера —\n" +
            "лента собьёт его и унесёт в опасную зону.";
        public const string TutorialStep3Hint = "СНАЧАЛА — ВОРОТА. ЗАТЕМ — КОНВЕЙЕР.";
        public const string TutorialStep3Reset = "ПОРЯДОК НАРУШЕН — ПОПРОБУЙТЕ СНОВА";
        public const string TutorialStep4Header = "4. РЕСУРС УПРАВЛЕНИЯ";
        public const string TutorialStep4Body =
            "«РЕСУРС УПРАВЛЕНИЯ» — энергия завода: каждая активация тратит заряд.\n" +
            "Заряды восстанавливаются сами, быстрее — после точных действий.\n" +
            "Не включайте всё сразу — выбирайте подходящий момент.";
        public const string TutorialDoneHeader = "УПРАВЛЕНИЕ ОСВОЕНО";
        public const string TutorialDoneBody =
            "Сломите РЕШИМОСТЬ инженера — он отступит, и цех останется заводу.\n" +
            "Не дайте ему завершить все ремонты — это победа инженера.\n" +
            "Наблюдайте, создавайте ситуации, действуйте в нужный момент.";
        public const string TutorialStartShift = "НАЧАТЬ СМЕНУ";
        public const string TutorialRepeat = "ПОВТОРИТЬ УРОК";
        public const string TutorialWrongMachine = "ЭТА СИСТЕМА НЕ НУЖНА НА ЭТОМ ШАГЕ";
        public const string TutorialStepLabel = "ШАГ {0} / 4";
    }
}
