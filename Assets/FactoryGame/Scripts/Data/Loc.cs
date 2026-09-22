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
        public const string EnterExecute = "[КРАСНАЯ КНОПКА] ";
        public const string EngineerLabel = "ИНЖЕНЕР: ";
        public const string ObjectiveReachExit = "ДОБРАТЬСЯ ДО ВЫХОДА";

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
        public const string NextRoomPrompt = "КРАСНАЯ КНОПКА — СЛЕДУЮЩИЙ ЦЕХ";
        public const string RoomStabilized = "ЦЕХ СТАБИЛИЗИРОВАН";
        public const string RoomStabilizedSub = "Инженер восстановил ручное управление.\nЗавод проиграл.";

        // Final victory screen
        public const string FactoryWon = "ЗАВОД ПОБЕДИЛ";
        public const string FactoryWonSub = "Инженер покинул предприятие.\nАвтономный режим сохранён.";
        public const string FactoryWonSmall = "СИСТЕМЫ ПРОИЗВОДСТВА ПРОДОЛЖАЮТ РАБОТУ.";
        public const string MenuRepeatRoom = "ПОВТОРИТЬ ЦЕХ";
        // No "leave the game" label exists on purpose: on the cabinet the only way
        // out is the «меню» touch button, which the launcher owns (contract §5).

        // Final victory picture (perimeter-camera frame behind «ЗАВОД ПОБЕДИЛ»)
        public const string FinalCameraLabel = "КАМЕРА 01 · ПЕРИМЕТР";
        public const string FinalRecLabel = "ЗАПИСЬ";
        public const string FinalStatusAutonomous = "АВТОНОМНЫЙ РЕЖИМ · ВСЕ ЦЕХА ПОД КОНТРОЛЕМ ЗАВОДА";

        // Victory animation (terminal lines typed out before the result panel)
        public const string VictoryLineEngineerLeft = "ИНЖЕНЕР ПОКИНУЛ ПРЕДПРИЯТИЕ";
        public const string VictoryLineAutonomous = "АВТОНОМНЫЙ РЕЖИМ СОХРАНЁН";

        // Title card
        public const string TitleSubtitle = "АВТОНОМНЫЙ ПРОМЫШЛЕННЫЙ ПРОТОКОЛ";
        public const string MenuStartShift = "НАЧАТЬ СМЕНУ";
        public const string FooterNext = "КРАСНАЯ КНОПКА — ДАЛЕЕ";
        public const string FooterContinue = "КРАСНАЯ КНОПКА — ПРОДОЛЖИТЬ";
        /// <summary>Hint under the end-room menu. It names only the Red button:
        /// the menu has a single option, so there is nothing to steer with the
        /// joystick.</summary>
        public const string MenuHint = "КРАСНАЯ КНОПКА — ПОДТВЕРДИТЬ";

        // ---------------- instruction pages (one, every new game) ----------------
        //
        // Live-cabinet playtest (founder, 2026-09): players could not keep up with
        // the intro — too many screens before the game starts. What is left is the
        // title card plus ONE page that says who the player is; the «ЦЕЛЬ СМЕНЫ»
        // and «УПРАВЛЕНИЕ» pages (how to play) are gone, and so are the two
        // paragraphs of this page that framed them. The removed wording is archived
        // in system/handoffs/texts-factory.md for the upcoming text rewrite.

        public const string InstructionCounter = "ИНСТРУКЦИЯ {0} / {1}";

        public static readonly string[] InstructionHeaders =
        {
            GameTitle,
        };

        /// <summary>Second line under the header; empty when a page has none.</summary>
        public static readonly string[] InstructionSubheaders =
        {
            TitleSubtitle,
        };

        public static readonly string[] InstructionBodies =
        {
            "Вы — интеллект предприятия.\n" +
            "Вы управляете системами цеха, а не человеком.",
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
        public const string EscalationAutoToast = "СИСТЕМЫ ЦЕХА ДЕЙСТВУЮТ САМОСТОЯТЕЛЬНО";
        public const string ExitUnlockedToast = "РЕШИМОСТЬ СЛОМЛЕНА — ИНЖЕНЕР ОТСТУПАЕТ";

        // Level 1 tutorial hints
        public const string HintSelect = "ДЖОЙСТИК ВВЕРХ/ВНИЗ — ВЫБРАТЬ СИСТЕМУ";
        public const string HintActivate = "КРАСНАЯ КНОПКА — АКТИВИРОВАТЬ СИСТЕМУ";
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
        public const string PurposeAlarmHint = "МЕШАЕТ РЕМОНТУ И ПРИБЛИЖАЕТ ПАНИКУ";
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

        // Tutorial choice screen (always the last intro screen)
        public const string TutorialChoiceHeader = "ВВОДНЫЙ УРОК";
        public const string TutorialChoiceBody =
            "В уроке вы познакомитесь с интерфейсом,\n" +
            "зонами воздействия и комбинациями систем.\n\n" +
            "Вы можете пройти его сейчас\n" +
            "или сразу перейти к основной смене.";
        public const string TutorialChoiceYes = "ПРОЙТИ УРОК";
        public const string TutorialChoiceNo = "НАЧАТЬ СМЕНУ";
        public const string TutorialChoiceFooter = "ДЖОЙСТИК ВВЕРХ / ВНИЗ — ВЫБОР\nКРАСНАЯ КНОПКА — ПОДТВЕРДИТЬ";

        // ---------------- interactive tutorial ----------------

        public const string TutorialRoomName = "УЧЕБНЫЙ ЦЕХ";
        public const string TutorialStepLabel = "ШАГ {0} / {1}";
        public const string TutorialFooterAck = "КРАСНАЯ КНОПКА — ДАЛЕЕ";
        public const string TutorialFooterAction = "ВЫПОЛНИТЕ ДЕЙСТВИЕ";
        public const string TutorialWrongMachine = "ЭТА СИСТЕМА НЕ НУЖНА НА ЭТОМ ШАГЕ";
        public const string TutorialRetry = "СИТУАЦИЯ ПОВТОРЯЕТСЯ — ПОПРОБУЙТЕ ЕЩЁ РАЗ";

        // Step 1 — engineer
        public const string TutStep1Header = "ИНЖЕНЕР";
        public const string TutStep1Body =
            "Это дежурный инженер.\n\n" +
            "Он пытается восстановить ручное управление.\n" +
            "Следите за его маршрутом и состоянием.\n\n" +
            "Если он завершит ремонт всех пультов,\n" +
            "цех будет стабилизирован.";

        // Step 2 — repair console
        public const string TutStep2Header = "РЕМОНТНЫЙ ПУЛЬТ";
        public const string TutStep2Body =
            "Инженер ремонтирует эти пульты.\n\n" +
            "Шкала рядом с пультом показывает\n" +
            "текущий прогресс ремонта.\n\n" +
            "Не дайте инженеру завершить работу.";

        // Step 3 — system list
        public const string TutStep3Header = "СИСТЕМЫ ЗАВОДА";
        public const string TutStep3Body =
            "Здесь находятся доступные системы цеха.\n\n" +
            "Джойстиком вверх и вниз выберите оборудование.\n" +
            "Нажмите красную кнопку, чтобы задействовать выбранную систему.";

        // Step 4 — machine information panel
        public const string TutStep4Header = "НАЗНАЧЕНИЕ СИСТЕМЫ";
        public const string TutStep4Body =
            "Здесь указано, что делает выбранная система.\n\n" +
            "Проверяйте её зону воздействия.\n" +
            "Не каждая система полезна в любой момент.";

        // Step 5 — control resource
        public const string TutStep5Header = "РЕСУРС УПРАВЛЕНИЯ";
        public const string TutStep5Body =
            "Активация систем расходует ресурс управления.\n\n" +
            "Не включайте всё подряд.\n" +
            "Точное воздействие восстанавливает ресурс быстрее.";

        // Step 6 — engineer resolve
        public const string TutStep6Header = "РЕШИМОСТЬ ИНЖЕНЕРА";
        public const string TutStep6Body =
            "Эффективные действия снижают решимость инженера.\n\n" +
            "Когда решимость иссякнет,\n" +
            "он отступит к выходу.";

        // Step 7 — effective zone
        public const string TutStep7Header = "ЗОНА ВОЗДЕЙСТВИЯ";
        public const string TutStep7Body =
            "Оорудование воздействует сильнее, когда инженер находится в его зоне.\n\n" +
            "Дождитесь подходящего момента.\n" +
            "Если цель вне зоны, воздействие будет слабым.";

        // Step 8 — first activation
        public const string TutStep8Header = "АКТИВАЦИЯ СИСТЕМЫ";
        public const string TutStep8Body =
            "Выберите манипулятор.\n\n" +
            "Нажмите красную кнопку, когда инженер окажется\n" +
            "в зоне воздействия.";
        public const string TutWaitOutOfZone = "ЦЕЛЬ ВНЕ ЗОНЫ — ПОДОЖДИТЕ";
        public const string TutGoodMoment = "ПОДХОДЯЩИЙ МОМЕНТ";
        public const string TutEarlyActivation = "ЦЕЛЬ ВНЕ ЗОНЫ. ДОЖДИТЕСЬ ПОДХОДЯЩЕГО МОМЕНТА.";

        // Step 9 — simple machine combination
        public const string TutStep9Header = "КОМБИНАЦИЯ СИСТЕМ";
        public const string TutStep9Body =
            "Системы эффективнее работают вместе.\n\n" +
            "Сначала перекройте маршрут воротами.\n" +
            "Затем, когда инженер выйдет на ленту,\n" +
            "смените направление конвейера.";
        public const string TutComboGateHeader = "СИСТЕМА: ВОРОТА";
        public const string TutComboGateBody = "ПЕРЕКРЫВАЕТ МАРШРУТ И ЗАСТАВЛЯЕТ ИНЖЕНЕРА ИСКАТЬ ОБХОД.";
        public const string TutComboConveyorHeader = "СИСТЕМА: КОНВЕЙЕР";
        public const string TutComboConveyorBody = "СМЕЩАЕТ ИНЖЕНЕРА ПО ЛИНИИ.";
        public const string TutSequenceReset = "ПОСЛЕДОВАТЕЛЬНОСТЬ СБРОШЕНА. ПОВТОРИТЕ ТЕКУЩИЙ ШАГ.";

        // Step 10 — completion
        public const string TutorialDoneHeader = "УПРАВЛЕНИЕ ОСВОЕНО";
        public const string TutorialDoneBody =
            "Наблюдайте за инженером.\n" +
            "Перекрывайте маршруты.\n" +
            "Создавайте комбинации.\n" +
            "Активируйте системы в подходящий момент.";
        public const string TutorialStartShift = "НАЧАТЬ СМЕНУ";
        public const string TutorialRepeat = "ПОВТОРИТЬ УРОК";
    }
}
