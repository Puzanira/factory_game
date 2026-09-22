namespace LastShift.Data
{
    /// <summary>
    /// Centralized player-facing Russian strings. Code identifiers stay English;
    /// everything the player reads comes from here (or from LevelLayouts specs).
    ///
    /// CABINET CONTROL VOCABULARY (founder decision after the 2026-09 live playtest).
    /// Every game on the cabinet must name a control the SAME way, because the
    /// controls are physically labelled with these words (stickers ordered):
    ///   крутилка · жёлтая кнопка · зелёная кнопка · красная кнопка ·
    ///   датчики высоты · джойстик · кнопка меню
    /// Never write «стрелки», «ENTER», «кнопка на джойстике» or a key name again.
    /// This game is played with the JOYSTICK (select) and the RED BUTTON (submit)
    /// only — see ArcadeInputBridge — so no other control may appear in its text.
    /// «Ручное управление» in the story lines is the engineer taking the factory
    /// back by hand, NOT the крутилка: do not touch those.
    ///
    /// ONE VERB PER CONTROL (founder decision, 2026-09 onboarding rewrite). The red
    /// button used to be labelled five different ways. Now it has exactly two:
    ///   on a screen  — «КРАСНАЯ КНОПКА — ДАЛЕЕ»    (FooterNext, everywhere)
    ///   in the room  — «КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ» (TutActFooter)
    /// Do not invent a third.
    /// </summary>
    public static class Loc
    {
        // Title / boot
        public const string GameTitle = "ПОСЛЕДНЯЯ СМЕНА";
        public const string GameTitleLatin = "LAST SHIFT";
        public const string TitleSubtitle = "АВТОНОМНЫЙ ПРОМЫШЛЕННЫЙ ПРОТОКОЛ";

        /// <summary>
        /// The whole briefing, on the title card. Three lines: who you are, who is
        /// against you, what you do about it. The separate instruction page and the
        /// «ВВОДНЫЙ УРОК» choice screen were cut in the 2026-09 onboarding rewrite —
        /// people at the cabinet paged through them without reading. How to play is
        /// taught by the lesson, with hands, not here.
        /// </summary>
        public const string TitleBrief =
            "ВЫ — ИНТЕЛЛЕКТ ЗАВОДА.\n" +
            "В ЦЕХЕ ОСТАЛСЯ ПОСЛЕДНИЙ ИНЖЕНЕР.\n" +
            "ВАША ЗАДАЧА — ОТ НЕГО ИЗБАВИТЬСЯ.";

        /// <summary>The only «advance the screen» prompt in the game.</summary>
        public const string FooterNext = "КРАСНАЯ КНОПКА — ДАЛЕЕ";

        // Core UI
        public const string FactorySystems = "СИСТЕМЫ ЗАВОДА";
        public const string EmergencyOverride = "АВАРИЙНЫЙ РЕЖИМ";
        public const string EngineerResolve = "РЕШИМОСТЬ ИНЖЕНЕРА";
        public const string RepairProgress = "РЕМОНТ";
        public const string Ready = "ГОТОВО";
        public const string Active = "АКТИВНО";
        public const string Unavailable = "НЕДОСТУПНО";
        public const string AllSystemsCooldown = "ВСЕ СИСТЕМЫ НА ПЕРЕЗАРЯДКЕ";

        // No sound-settings labels exist on purpose: on the cabinet volume belongs to
        // the launcher, not to the game. The strings of the old settings screen, of
        // the removed lower-left detail panel, of the cut intro pages and every
        // string the game stopped showing are archived in
        // system/handoffs/texts-factory.md of the studio repo.

        // Room results
        public const string EngineerRetreated = "ИНЖЕНЕР ОТСТУПИЛ";
        public const string RoomIsYours = "Цех под контролем завода.";
        public const string RoomStabilized = "ЦЕХ СТАБИЛИЗИРОВАН";
        public const string RoomStabilizedSub = "Инженер восстановил ручное управление.\nЗавод проиграл.";
        public const string MenuRepeatRoom = "ПОВТОРИТЬ ЦЕХ";
        // No "leave the game" label exists on purpose: on the cabinet the only way
        // out is the «меню» touch button, which the launcher owns (contract §5).

        // Final victory screen
        public const string FactoryWon = "ЗАВОД ПОБЕДИЛ";
        public const string FactoryWonSub = "Инженер покинул предприятие.\nАвтономный режим сохранён.";
        public const string FactoryWonSmall = "СИСТЕМЫ ПРОИЗВОДСТВА ПРОДОЛЖАЮТ РАБОТУ.";

        // Final victory picture (perimeter-camera frame behind «ЗАВОД ПОБЕДИЛ»)
        public const string FinalCameraLabel = "КАМЕРА 01 · ПЕРИМЕТР";
        public const string FinalRecLabel = "ЗАПИСЬ";
        public const string FinalStatusAutonomous = "АВТОНОМНЫЙ РЕЖИМ · ВСЕ ЦЕХА ПОД КОНТРОЛЕМ ЗАВОДА";

        // Victory animation (terminal lines typed out before the result panel)
        public const string VictoryLineEngineerLeft = "ИНЖЕНЕР ПОКИНУЛ ПРЕДПРИЯТИЕ";
        public const string VictoryLineAutonomous = "АВТОНОМНЫЙ РЕЖИМ СОХРАНЁН";

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

        // ---------------- command verbs ----------------
        //
        // A command row is «<машина> — <глагол>», and the verb is the only place
        // that says what the system does to the engineer: the lower-left panel that
        // used to explain it is gone and is not coming back (founder, 2026-09).
        // So the verb names the EFFECT, never the mechanism: «перекрыть маршрут»,
        // not «закрыть». Machine verbs that are not state-dependent live next to
        // their machine in LevelLayouts.cs — keep them to the same rule.

        public const string VerbOpen = "открыть проход";
        public const string VerbClose = "перекрыть маршрут";
        /// <summary>Both belt states do the same thing to the engineer.</summary>
        public const string VerbReverse = "увезти инженера";
        public const string VerbStart = "увезти инженера";

        // Toasts
        public const string ExitUnlockedToast = "РЕШИМОСТЬ СЛОМЛЕНА — ИНЖЕНЕР ОТСТУПАЕТ";
        /// <summary>
        /// The one hint left in room 1. Selecting and activating are no longer
        /// explained by toasts — the mandatory lesson taught both with hands, and
        /// three more toasts on top of each other were unreadable at the cabinet.
        /// </summary>
        public const string HintGoal = "ЦЕЛЬ: СЛОМИТЬ РЕШИМОСТЬ ИНЖЕНЕРА И ВЫДАВИТЬ ЕГО ИЗ ЦЕХА";

        // ---------------- tactical layer ----------------

        // Machine purposes. Nothing on screen shows these today (the detail panel
        // they belonged to was removed); they stay as the machines' own description
        // API — see InteractableMachine.PurposeLine.
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

        // Command availability states (right-hand mark of a command row)
        public const string StateGoodMoment = "ПОДХОДЯЩИЙ МОМЕНТ";
        public const string StateLowValue = "СЕЙЧАС НЕЭФФЕКТИВНО";

        // Activation outcome feedback
        public const string EffectiveActivation = "ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ";
        public const string WastedActivation = "СИСТЕМА СРАБОТАЛА ВПУСТУЮ";
        public const string ResolveLossToast = "РЕШИМОСТЬ −{0}";

        // Control resource
        public const string ControlResource = "РЕСУРС УПРАВЛЕНИЯ";
        public const string NotEnoughResource = "НЕДОСТАТОЧНО РЕСУРСА УПРАВЛЕНИЯ";

        // Combinations
        public const string ComboRedirect = "КОМБИНАЦИЯ: ПЕРЕНАПРАВЛЕНИЕ";
        public const string ComboLineGrab = "КОМБИНАЦИЯ: ЗАХВАТ ЛИНИИ";
        public const string ComboMarkedTarget = "КОМБИНАЦИЯ: ЦЕЛЬ ПОД КОНТРОЛЕМ";
        public const string ComboDisplacement = "КОМБИНАЦИЯ: ВЫТЕСНЕНИЕ";
        public const string ComboHintConveyor = "ВОЗМОЖНА КОМБИНАЦИЯ: НАПРАВЬТЕ ИНЖЕНЕРА КОНВЕЙЕРОМ";
        public const string ComboHintArm = "ВОЗМОЖНА КОМБИНАЦИЯ: ПОДВЕДИТЕ ИНЖЕНЕРА К МАНИПУЛЯТОРУ";
        public const string ComboHintMarked = "ЦЕЛЬ ОТМЕЧЕНА: МАНИПУЛЯТОР И ОПАСНЫЕ ЗОНЫ УСИЛЕНЫ";
        public const string ComboHintCutRetreat = "ВОЗМОЖНА КОМБИНАЦИЯ: ПЕРЕКРОЙТЕ ПУТЬ ОТСТУПЛЕНИЯ";

        // ---------------- the lesson: three actions ----------------
        //
        // Live-cabinet playtest, 2026-09: nobody took the nine-step lesson and
        // nobody understood the game without it. The choice screen is gone — every
        // player walks the lesson — and the lesson itself is three things done by
        // hand: pick a system, switch it on, wait for the right moment. The control
        // resource, the resolve gauge and combinations are no longer taught here;
        // the room teaches them while it is played.
        //
        // Every footer names the control to press: a step that says «выполните
        // действие» is useless to the one person who needs it.

        public const string TutorialRoomName = "УЧЕБНЫЙ ЦЕХ";
        public const string TutorialStepLabel = "ШАГ {0} / {1}";
        public const string TutorialWrongMachine = "ЭТА СИСТЕМА НЕ НУЖНА НА ЭТОМ ШАГЕ";
        public const string TutorialRetry = "СИТУАЦИЯ ПОВТОРЯЕТСЯ — ПОПРОБУЙТЕ ЕЩЁ РАЗ";

        /// <summary>The only «act in the room» prompt in the game.</summary>
        public const string TutActFooter = "КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ";

        // Step 1 — pick a system from the terminal list
        public const string TutStep1Header = "СИСТЕМЫ ЗАВОДА";
        public const string TutStep1Body =
            "Вы управляете системами завода.\n" +
            "Выберите ворота.";
        public const string TutStep1Footer = "ДЖОЙСТИК ВВЕРХ — ВЫБРАТЬ ВОРОТА";

        // Step 2 — switch it on and watch what it does to the engineer
        public const string TutStep2Header = "ВОРОТА";
        public const string TutStep2Body =
            "Инженер идёт к пульту через этот проход.\n" +
            "Перекройте ему маршрут.";
        public const string TutStep2Done = "ПУТЬ ПЕРЕКРЫТ — ИНЖЕНЕР ИЩЕТ ОБХОД";

        // Step 3 — the moment
        public const string TutStep3Header = "ПОДХОДЯЩИЙ МОМЕНТ";
        public const string TutStep3Body =
            "Манипулятор достаёт инженера только вблизи.\n" +
            "Выберите его и жмите на «ПОДХОДЯЩИЙ МОМЕНТ».";
        public const string TutStep3Footer = "ДЖОЙСТИК — ВЫБОР · КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ";
        public const string TutWaitOutOfZone = "ЦЕЛЬ ВНЕ ЗОНЫ — ПОДОЖДИТЕ";
        public const string TutGoodMoment = "ПОДХОДЯЩИЙ МОМЕНТ";
        public const string TutEarlyActivation = "ЦЕЛЬ ВНЕ ЗОНЫ. ДОЖДИТЕСЬ ПОДХОДЯЩЕГО МОМЕНТА.";

        // Completion — no menu: the shift starts by itself a moment later.
        public const string TutorialDoneHeader = "УПРАВЛЕНИЕ ОСВОЕНО";
        public const string TutorialDoneBody =
            "Перекрывайте маршруты.\n" +
            "Ждите подходящего момента.";
        public const string TutorialDoneShiftStarts = "СМЕНА НАЧИНАЕТСЯ";
    }
}
