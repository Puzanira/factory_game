# LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА — Vertical Slice

> **Pass 11 (2026-09-22): обучение и стартовые экраны переписаны под автомат.**
> Жалоба с живого плейтеста: «игрок не успевает вообще понимать, что
> происходит», урок не проходил никто. Теперь до игры **один экран чтения** —
> титр, на нём же три строки вводной (`Loc.TitleBrief`); страница-инструкция и
> экран выбора «ПРОЙТИ УРОК / НАЧАТЬ СМЕНУ» убраны. **Урок обязателен и состоит
> из трёх действий руками** (выбрать систему джойстиком → включить красной
> кнопкой → дождаться «ПОДХОДЯЩЕГО МОМЕНТА»); ресурс управления, решимость и
> комбинации из урока убраны — им учит сама игра. Каждая подсказка урока
> называет орган: общей строки «ВЫПОЛНИТЕ ДЕЙСТВИЕ» больше нет. У красной кнопки
> ровно два глагола: «ДАЛЕЕ» на экране, «ВКЛЮЧИТЬ» в цехе. Команды систем
> переименованы так, что глагол называет действие на инженера («Ворота —
> перекрыть маршрут»), потому что панели с назначением системы нет и не будет.
> Закреплено тестами
> `Assets/FactoryGame/Tests/EditMode/OnboardingTextTests.cs`; все тексты и архив
> убранного — `system/handoffs/texts-factory.md` репозитория студии.

> **Pass 10 (2026-09-21): игра больше не выходит из себя сама.** На стойке
> «Последняя смена» работает ВНУТРИ процесса лаунчера автомата, поэтому
> `Application.Quit()` гасил весь автомат вместе с хабом. Пункт «ВЫЙТИ ИЗ ИГРЫ»
> убран из меню финальной комнаты (осталась одна строка — «ПОВТОРИТЬ ЦЕХ»),
> мёртвое модальное окно «ВЫЙТИ ИЗ ИГРЫ?» убрано из вступления, метод
> `SceneLoader.Quit()` удалён. Выход из игры один — сенсорная кнопка **«меню»**
> автомата, её обрабатывает хаб; игра про неё не знает
> (ARCADE_INTEGRATION_CONTRACT §5). Закреплено тестами
> `Assets/FactoryGame/Tests/EditMode/ArcadeContractTests.cs`.

> **Pass 9 (2026-07-26): onboarding, HUD, tutorial and result screens redesigned.**
> Boot flow is now **Title Card → three mandatory instruction pages → «ВВОДНЫЙ УРОК»
> choice → lesson or Level 1** (see «ПОСЛЕДОВАТЕЛЬНОСТЬ ЗАПУСКА»). The permanent top
> HUD strip is gone: «РЕСУРС УПРАВЛЕНИЯ» and «РЕШИМОСТЬ ИНЖЕНЕРА» moved into the
> lower-left detail panel, repair progress moved to a local «РЕМОНТ» plate at the
> console being repaired, and the room title only flashes at room start
> (see «ИНТЕРФЕЙС»). The interactive lesson is now **10 steps, one target at a time**:
> everything else dims, the callout is an opaque industrial terminal card (no speech
> bubbles, no cartoon arrows), rectangular targets get rectangular frames, and each
> informational step waits for Enter (see «ВВОДНЫЙ УРОК»). Result screens are fully
> opaque, and a factory victory plays a short industrial animation before the menu
> accepts input (see «ПОБЕДА И ПОРАЖЕНИЕ»).

> **Pass 8.1 (2026-07-19): Room Pressure removed.** Playtest verdict: давление и
> аварийный режим (машины, стреляющие сами) убраны совсем. Цех решается только
> РЕШИМОСТЬЮ против ремонтов; HUD показывает одну широкую полосу решимости;
> награды комбинаций — прямые потери решимости; оборудование срабатывает только
> по команде игрока. Конвейеры теперь по-настоящему уносят стоящего на ленте
> инженера к её концу (плохая опора — своя скорость падает), но при отступлении
> он просто перешагивает ленты — заблокировать уже выигранный цех невозможно.
> Спящие зоны пара/тумана больше не выглядят включёнными (частицы глушатся).

> **Pass 8 (2026-07-19): tactical gameplay + interactive tutorial.** Machines now
> have visible purposes and effective zones; activating at the wrong moment is a
> «СИСТЕМА СРАБОТАЛА ВПУСТУЮ» with a longer cooldown, at the right moment an
> «ЭФФЕКТИВНОЕ ВОЗДЕЙСТВИЕ» with bonuses. New «РЕСУРС УПРАВЛЕНИЯ» (3 charges,
> slow recharge, faster after effective actions) prevents blind machine spam.
> Setup→payoff combinations («ПЕРЕНАПРАВЛЕНИЕ», «ЗАХВАТ ЛИНИИ», «ЦЕЛЬ ПОД
> КОНТРОЛЕМ», «ВЫТЕСНЕНИЕ») reward planned sequences. Boot flow gained a
> «ПЕРВЫЙ ЗАПУСК» tutorial choice and a 4-step interactive lesson in the
> «УЧЕБНЫЙ ЦЕХ» room (see «ТАКТИЧЕСКИЙ ГЕЙМПЛЕЙ» and «ВВОДНЫЙ УРОК» below).
> Tuning lives in `TacticsData` (optional asset `Resources/TacticsData`).

> **Pass 7 (2026-07-18): unified restart/quit menu.** The defeat screen
> «ЦЕХ СТАБИЛИЗИРОВАН» and the final «ЗАВОД ПОБЕДИЛ» screen now show the same
> two-option terminal menu — **«ПОВТОРИТЬ ЦЕХ» / «ВЫЙТИ ИЗ ИГРЫ»** — below the
> result text, driven by the shared Up/Down/Enter logical actions (keyboard and
> Arduino). «ПОВТОРИТЬ ЦЕХ» always reloads the *current* room; the pause-menu
> restart entry was renamed to «ПОВТОРИТЬ ЦЕХ». The old final-screen options
> «НАЧАТЬ ЗАНОВО» / «ВЕРНУТЬСЯ К ПЕРВОМУ ЦЕХУ» and the Enter-to-retry defeat
> prompt were removed. **Клавиши Q и R не используются.**

> **Pass 6 (2026-07-18): input simplified to three actions.** The whole game is now
> driven by exactly NavigatePrevious / NavigateNext / Submit (keyboard Up / Down /
> Enter; Arduino joystick + two buttons), plus Escape for pause/back.
> **Потенциометр, а также клавиши Q и R удалены из схемы управления.**
> An outdated Arduino sketch still sending POT messages is safely
> ignored (one console warning). See «УПРАВЛЕНИЕ» below.

> **Pass 5 (2026-07-18): Arduino controller input.** The game now *additionally* accepts
> a custom Arduino Nano controller (2-axis joystick + two buttons) over USB serial.
> The keyboard keeps working everywhere and at all
> times — the controller is optional and hot-pluggable. All sources merge into the same
> logical actions in `Scripts/Utilities/GameInput.cs` via the new `Scripts/Input/` layer
> (background-thread serial reader, CSV protocol parser, deadzones/hysteresis/repeat,
> shared submit debounce). See «ПОДКЛЮЧЕНИЕ ARDUINO-КОНТРОЛЛЕРА» below.

> **Pass 2 (2026-07-12):** full Russian localization (all player-facing text centralized
> in `Scripts/Data/Loc.cs` and `LevelLayouts.cs`), easier balance (repair times +40%,
> cooldowns −25%, slower engineer, diminishing returns, pressure combos «СБОЙ СИСТЕМ»,
> cooldown-recovery boost, 75% pressure warning, Level-1 tutorial toasts), and a major
> visual upgrade: procedural floor/wall/belt/hazard-stripe textures, steam/cold-fog/spark
> particles, URP **2D lights** (global mood per room + point lights on objectives, exits,
> alarms, corner lamps), layered machine silhouettes (forklift with wheels/forks, jointed
> arms, CRT terminal with scanlines and LEDs), toast notifications. UI stays keyboard-only.

Top-down 2D arcade sabotage game. **You are the factory.** The last on-duty engineer
is trying to repair the automated dairy plant and take back manual control. You win a
room not by killing him, but by making it impossible to hold: block routes, spring
hazards, wear his **Resolve** down to zero and force him to retreat to the exit.
When he evacuates the third room alive, the vertical slice is complete —
*the factory is learning.*

---

> **Pass 4 (2026-07-13): first-pass audio.** Fully procedural industrial soundscape —
> no imported/paid assets. `SfxSynth` (Audio/Scripts) synthesizes all 54 sounds; the
> editor tool **Tools ▸ Last Shift ▸ Build Audio** (`AudioProjectBuilder`) exports them
> as WAVs to `Assets/Audio/Generated/`, creates `Assets/Audio/Mixers/LastShiftMixer.mixer`
> (groups Master → Music/Ambient/Machines/Hazards/UI/Engineer/VoiceOrNarrative/Alerts,
> exposed params `MasterVolume`…`AlertsVolume`) and the `AudioLibrary` asset in
> `Assets/Audio/Resources/`. Runtime: persistent `AudioManager` (one-shot pool, id-keyed
> fading loops that can never stack, pause ducking, stereo pan by screen position; a
> missing clip/mixer degrades safely to runtime synthesis and plain volume scaling),
> `RoomAudioDirector` (per-level ambience beds, low music drone, tension layer at 75%
> Pressure, escalation pulse, thinner mix on retreat, sparse room flavour),
> `EngineerAudio` (footsteps/repair ticks/panic breathing/stun zap), `HazardAudio`
> (steam hiss, cold fan, spray, sparks), machine hooks in every machine class, and full
> terminal/menu feedback (move tick, confirm, denied buzz, cooldown click, ready blip).
> The pause menu is now a real Up/Down/Enter menu with Russian sound settings
> («НАСТРОЙКИ ЗВУКА»: ОБЩАЯ ГРОМКОСТЬ / МУЗЫКА / ЗВУКОВЫЕ ЭФФЕКТЫ, 25% steps,
> Enter cycles, Esc back). (The old R/Q pause shortcuts were removed in Pass 6.)

> **Pass 3 (2026-07-12):** Boot flow is now **Title Card «ПОСЛЕДНЯЯ СМЕНА» → 4-page
> Russian intro briefing → Level 1** (Enter advances pages, Esc opens «ПРОПУСТИТЬ
> ИНСТРУКТАЖ?», all keyboard-only, shown on every new game). The final Level-3 screen
> is now **«ЗАВОД ПОБЕДИЛ»** with a menu (since Pass 10 a single option,
> «ПОВТОРИТЬ ЦЕХ») and a red-to-green calm-down light transition.
> The engineer is a readable top-down human worker: helmet with lamp + brim, head,
> orange jacket with hi-vis stripes (torso larger than head), dark trousers with
> alternating legs and boots, soft shadow, warm readability rim, helmet point light;
> state effects (repair lean + tool flash, panic red rim, stun yellow flicker + shake,
> cold/steam rim tints, slippery wobble, brief green evacuation arrow on retreat).
> Engineer renders on the **Characters** sorting layer (fog/decals can never hide him);
> state label on **WorldUI**. Sorting layers Floor…ScreenUI are created by ProjectBuilder.

## Controls (arcade cabinet — joystick + Red button, no mouse needed)

Every screen is driven by the same two logical actions; the mouse is never used.
Input comes from the shared **arcade-controls** package (`ArcadeInput`): the
`ArcadeInputBridge` publishes it into `Scripts/Input/UnifiedGameInput.cs`, and all
polling is centralized in `Scripts/Utilities/GameInput.cs`. In a standalone build
the package's keyboard backend simulates the cabinet (joystick = arrow keys, Red
button = **Quote/Э**). There is **no pause** and no back/cancel action; the
cabinet's Menu button is reserved for the launcher and never read by the game.

### Gameplay (factory terminal)

| Control      | Action                                             |
|--------------|----------------------------------------------------|
| **Joystick up / left** | Select previous ready factory command (wraps, skips cooldowns) |
| **Joystick down / right** | Select next ready factory command       |
| **Red button** | Activate the selected command                    |

Room restart is available through the end-of-room menu after a defeat or the
final victory.

### Title card (Boot)

| Control      | Action                                             |
|--------------|----------------------------------------------------|
| **Red button** | Title → the lesson. There is nothing else to page through and nothing to choose |

### End-of-room screens

| Control      | Action                                             |
|--------------|----------------------------------------------------|
| **Red button** | Next room (after «ИНЖЕНЕР ОТСТУПИЛ»)             |
| **Red button** | Confirm «ПОВТОРИТЬ ЦЕХ» — the single end-menu option (after «ЦЕХ СТАБИЛИЗИРОВАН» and on the final «ЗАВОД ПОБЕДИЛ» screen). The game has no «выйти»: leaving is the cabinet's «меню» button, owned by the hub |

Every victory first plays a short industrial animation (~3.5 s); result input only
wakes up once it has finished and the opaque result panel is up.

## УПРАВЛЕНИЕ

Вся игра управляется тремя действиями: **предыдущий / следующий / подтвердить**.
Паузы и действия «назад» нет — это игра для аркадного автомата.

Автомат (слой arcade-controls):
- Джойстик вверх или влево — предыдущая система / пункт меню
- Джойстик вниз или вправо — следующая система / пункт меню
- Красная кнопка — подтвердить / активировать

Клавиатурная симуляция автомата (standalone-сборка, раскладка пакета
arcade-controls): стрелки — джойстик, **Quote/Э** — красная кнопка.

### МЕНЮ ПОСЛЕ ЗАВЕРШЕНИЯ ЦЕХА

После поражения («ЦЕХ СТАБИЛИЗИРОВАН») и на финальном экране («ЗАВОД ПОБЕДИЛ»)
под текстом результата появляется меню из одного пункта (красная кнопка —
подтвердить):

```
> ПОВТОРИТЬ ЦЕХ
```

- «ПОВТОРИТЬ ЦЕХ» — начать текущий цех заново.

Пункта «выйти» в игре нет и быть не должно: на стойке игра живёт внутри процесса
лаунчера, и завершить себя она не может — это погасило бы весь автомат. Выход
один — сенсорная кнопка **«меню»** автомата, её обрабатывает хаб
(`ArcadeInput.MenuButton` → `LauncherReturn`). См. ARCADE_INTEGRATION_CONTRACT §5.

> Изменение схемы управления: **потенциометр, а также клавиши Q и R удалены из
> схемы управления.** **Клавиши Q и R не используются.**

## ТАКТИЧЕСКИЙ ГЕЙМПЛЕЙ

Завод побеждает не количеством нажатий, а точным моментом. Наблюдайте за
инженером, готовьте ситуацию и включайте нужную систему вовремя.

- **Зоны воздействия.** У каждой системы есть зона, в которой она реально
  влияет на инженера. Зона показывается на карте при выборе команды, а под
  командой отображается статус: «ЦЕЛЬ В ЗОНЕ ВОЗДЕЙСТВИЯ» или «ЦЕЛЬ ВНЕ ЗОНЫ —
  ЭФФЕКТ БУДЕТ СЛАБЫМ». В списке команд готовые системы помечаются как
  «ГОТОВО», «ПОДХОДЯЩИЙ МОМЕНТ» или «СЕЙЧАС НЕЭФФЕКТИВНО».
- **Точный момент сильнее.** Срабатывание по цели в зоне — «ЭФФЕКТИВНОЕ
  ВОЗДЕЙСТВИЕ»: полный эффект, ускоренная перезарядка, восстановление ресурса.
  Холостое срабатывание — «СИСТЕМА СРАБОТАЛА ВПУСТУЮ»: эффекта почти нет,
  перезарядка дольше. Никакого мгновенного наказания — только цена времени.
- **Комбинации систем.** Одна система создаёт ситуацию, другая её использует
  (окно ~5 секунд, подсказки появляются только когда комбинация действительно
  возможна):
  - **Ворота → Конвейер** («ПЕРЕНАПРАВЛЕНИЕ»): перекройте маршрут — инженер
    пойдёт в обход через ленту; включите конвейер, и он унесёт его от цели.
  - **Конвейер → Манипулятор** («ЗАХВАТ ЛИНИИ»): лента подвозит инженера в
    радиус манипулятора — захват оглушает и срывает ремонт.
  - **Дрон → Манипулятор / Опасная зона** («ЦЕЛЬ ПОД КОНТРОЛЕМ»): отмеченный
    дроном инженер получает усиленные оглушения и потери решимости.
  - **Опасная зона → Ворота / Погрузчик** («ВЫТЕСНЕНИЕ»): пар или холод
    выгоняет инженера, а закрытый маршрут не даёт вернуться.
- **РЕСУРС УПРАВЛЕНИЯ.** У завода 3 заряда управления (ячейки под заголовком
  терминала). Каждая крупная активация тратит 1 заряд; повторное открытие
  ворот бесплатно. Заряды медленно восстанавливаются сами, заметно быстрее —
  после эффективных действий и комбинаций. Если ресурса не хватает —
  «НЕДОСТАТОЧНО РЕСУРСА УПРАВЛЕНИЯ», и нужно дождаться восстановления:
  включить всё подряд просто не получится.
- **Обратная связь.** Удары и ловушки показывают «РЕШИМОСТЬ −N»; комбинации —
  своё название и бонусную потерю решимости с отдельным сдержанным звуком.

Все параметры (заряды, скорость восстановления, окна комбинаций, бонусы)
настраиваются в `TacticsData` (Assets ▸ Create ▸ Last Shift ▸ Tactics Data,
положить в `Resources/TacticsData`; без ассета действуют значения по умолчанию).

## ПОСЛЕДОВАТЕЛЬНОСТЬ ЗАПУСКА

Каждый новый запуск игры проходит один и тот же путь:

```
Boot
  → Титр                       «ПОСЛЕДНЯЯ СМЕНА» + три строки вводной
                               + КРАСНАЯ КНОПКА — ДАЛЕЕ
  → Урок (обязателен)          «УЧЕБНЫЙ ЦЕХ», три действия
  → Level_01_RawMilkIntake     смена начинается сама
```

- **Экран чтения один.** Вся вводная — три строки на титре: кто вы, кто против
  вас, что вы с этим делаете. Страницы «ПОСЛЕДНЯЯ СМЕНА», «ЦЕЛЬ СМЕНЫ» и
  «УПРАВЛЕНИЕ» убраны после живого плейтеста: у автомата их не читали.
- **Урок нельзя пропустить.** Экрана выбора нет — все жали «НАЧАТЬ СМЕНУ» и
  оставались перед непонятной игрой. Пропуск есть только у безголового
  харнесса (`IntroFlowUI.DevSkipTutorial`), игроку такого органа не дано.
- Ничего не сохраняется (никакого PlayerPrefs): путь одинаков при каждом запуске.
- На титре достаточно красной кнопки; джойстик там не нужен.

## УРОК — ТРИ ДЕЙСТВИЯ

- Урок **обязателен** и идёт сразу за титром, в спокойном «УЧЕБНОМ ЦЕХЕ»: один
  инженер, один ремонтный пульт, ворота, конвейер и манипулятор — всё на
  настоящих машинах, настоящем ИИ инженера и настоящем интерфейсе.
- Он состоит из трёх **действий**, а не из карточек текста. Ничего не
  подтверждается «для галочки»: шаг заканчивается тем, что игрок сделал.
- Ровно одна цель подсвечена, подсказка — короткая карточка рядом с ней.
- **Каждая карточка называет орган**, которым делают шаг.

| Шаг | Что делает игрок | Заголовок | Подсказка |
|-----|------------------|-----------|-----------|
| 1 / 3 | ведёт джойстик на строку ворот (курсор стоит на конвейере) | «СИСТЕМЫ ЗАВОДА» | ДЖОЙСТИК ВВЕРХ — ВЫБРАТЬ ВОРОТА |

На первом шаге **всё, кроме списка систем, затемнено** — на новом экране глазу
надо показать одно место. Затемнение снимается в тот момент, когда ворота
выбраны, и дальше цех виден целиком (`ShowPractical(..., dimAround: true)` +
`TutorialStepController.Undim()`). Остальные шаги никогда не затемняют цех:
игрок в них смотрит на инженера.

| 2 / 3 | закрывает ворота перед идущим инженером | «ВОРОТА» | КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ |
| 3 / 3 | выбирает манипулятор и жмёт на «ПОДХОДЯЩИЙ МОМЕНТ» | «ПОДХОДЯЩИЙ МОМЕНТ» | ДЖОЙСТИК — ВЫБОР · КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ |

Чему урок больше **не** учит: ресурсу управления, решимости инженера и
комбинациям. Это объясняет сама игра, когда оно случается («НЕДОСТАТОЧНО
РЕСУРСА УПРАВЛЕНИЯ», «РЕШИМОСТЬ −N», «ВОЗМОЖНА КОМБИНАЦИЯ: …»). Подсказки про
комбинации на время урока заглушены, чтобы не лезть на его карточку.

Урок нельзя провалить:

- Ранняя активация (цель вне зоны) показывает «ЦЕЛЬ ВНЕ ЗОНЫ. ДОЖДИТЕСЬ
  ПОДХОДЯЩЕГО МОМЕНТА.», инженер возвращается на подход, и шаг повторяется.
- Если ничего не происходит долго, ситуация тихо разыгрывается заново.
- Инженер никогда не может закончить учебный ремонт: прогресс сбрасывается.
- «УПРАВЛЕНИЕ ОСВОЕНО» держится ~3 секунды, и смена начинается **сама**; меню
  там больше нет, красная кнопка только сокращает ожидание.

Урок использует те же органы управления, что и игра: джойстик вверх/вниз и
красная кнопка (слой arcade-controls). Отдельного EventSystem и отдельного
ввода у урока нет.

## ИНТЕРФЕЙС

- **Верхняя постоянная HUD-шапка удалена.** Убраны верхняя полоса с названием
  цеха, верхняя шкала «РЕШИМОСТЬ ИНЖЕНЕРА», глобальная шкала ремонта и строка
  задачи. Верх экрана теперь принадлежит цеху.
- **Название цеха** коротко появляется в начале цеха и плавно исчезает.
- **Шкала ремонта отображается у ремонтного пульта** — компактная плашка
  «РЕМОНТ» с процентом прямо над пультом, и только пока этот пульт действительно
  чинят. Глобальной шкалы ремонта больше нет.
- **Нижней левой информационной панели больше нет** (убрана после живого
  плейтеста: «наши люди на плейтесте вообще туда не смотрели»). Вместе с ней с
  экрана ушли назначение выбранной системы и подпись «красная кнопка —
  включить»; возвращать их не нужно.
- **Ресурс управления и решимость инженера** — одна тонкая полоса по верху
  игрового поля, справа от рамки терминала.
- Что делает система, теперь говорит **глагол в строке команды**: «Ворота —
  перекрыть маршрут», «Манипулятор — оглушить инженера». Глагол называет
  действие на инженера, а не механику — это единственное объяснение на экране.
- Слева сохраняются заголовок «СИСТЕМЫ ЗАВОДА», список систем, текущий выбор,
  перезарядка и метки «ГОТОВО» / «ПОДХОДЯЩИЙ МОМЕНТ» / «СЕЙЧАС НЕЭФФЕКТИВНО».
  Высота строк подстраивается под панель; если систем слишком много (как в цехе
  «УПАКОВОЧНАЯ ЛИНИЯ»), список прокручивается вслед за выбором, а сверху и снизу
  появляются метки ▲ / ▼. Прокрутка идёт от тех же стрелок — отдельного ввода нет.
- **Состояние инженера** («РЕМОНТИРУЕТ», «ИЩЕТ ОБХОД», «ОГЛУШЁН», «ОТСТУПАЕТ»)
  остаётся подписью рядом с самим инженером.
- **Прямоугольные объекты выделяются прямоугольными рамками.** Это относится и к
  подсветке выбранной системы в цехе: конвейер получает длинную рамку по форме
  ленты, а не жёлтый круг вокруг себя. Конвейеры, пульты, ворота, панели
  интерфейса — прямоугольная рамка с техническими уголками; инженер и
  манипулятор в уроке — только уголки; круглая рамка применяется лишь к
  действительно круглым элементам. Вспышка при активации системы использует ту же
  прямоугольную форму.

## ПОБЕДА И ПОРАЖЕНИЕ

- Результаты показываются на **непрозрачной терминальной панели**: игра за ней
  не видна и не читается.
- Поражение — «ЦЕХ СТАБИЛИЗИРОВАН»: «Инженер восстановил ручное управление.
  Завод проиграл.»
- Победа завода — «ЗАВОД ПОБЕДИЛ»: «Инженер покинул предприятие. Автономный
  режим сохранён.»
- После результата доступен один пункт:

```
> ПОВТОРИТЬ ЦЕХ
```

- **После прохождения всех цехов** финальный экран показывает победную картину:
  кадр с камеры периметра — ночной завод, который переходит на автономное
  управление. Кадр оживает по шагам: сигнал камеры устанавливается, цех зажигает
  окна одно за другим, трубы начинают дышать паром, обзорный луч проходит по
  фасаду, а последний инженер выходит за ворота и исчезает; внизу загорается
  «АВТОНОМНЫЙ РЕЖИМ · ВСЕ ЦЕХА ПОД КОНТРОЛЕМ ЗАВОДА». Под картиной — заголовок
  «ЗАВОД ПОБЕДИЛ» и то же меню. Всё нарисовано кодом, как и остальная графика.
- **Победа завода сопровождается промышленной анимацией** (~3.5 с): инженер
  уходит за границу выхода и растворяется, красный аварийный свет гаснет, цех
  переходит в стабильный зелёно-янтарный режим, одна-две машины возвращаются в
  спокойный автономный холостой ход, терминал построчно печатает «ИНЖЕНЕР
  ПОКИНУЛ ПРЕДПРИЯТИЕ» и «АВТОНОМНЫЙ РЕЖИМ СОХРАНЁН», затем появляется крупный
  заголовок «ЗАВОД ПОБЕДИЛ». Звук сдержанный: щелчок реле, промышленный тон
  подтверждения — без фанфар. Меню результата принимает ввод только после
  окончания анимации.

## ВВОД: СЛОЙ ARCADE-CONTROLS

Игра читает ввод только через пакет **com.aigamestudio.arcade-controls**
(зависимость в `Packages/manifest.json`, git-URL). Прямых обращений к
клавиатуре/мыши в коде игры нет.

- Мост: `Scripts/Input/ArcadeInputBridge.cs` — синглтон, каждый кадр публикует
  джойстик/красную кнопку из `ArcadeInput` в `UnifiedGameInput` (порядок
  исполнения −100, раньше всех потребителей). В standalone-сборке мост сам
  поднимает клавиатурный бэкенд пакета; внутри аркадного лаунчера бэкендом
  владеет лаунчер, мост его только читает.
- Кнопка Menu автомата игрой не читается — возврат в лаунчер делает сам лаунчер.
- Green/Bang/Height/Crank из контракта автомата игрой не используются.
- Клавиатурная симуляция (раскладка пакета): стрелки — джойстик,
  **Quote/Э** — красная кнопка. Клавиши **Q и R не используются**.
- Удержание джойстика повторяет шаг (задержка 0.40 с, далее каждые 0.15 с);
  диагональ даёт ровно одно действие (приоритет у вертикали).

## How to test

0. Flow: **Boot → Title Card → Lesson «УЧЕБНЫЙ ЦЕХ» (3 actions) → Level 1**.
   The Red button (Quote/Э on a keyboard) leaves the title; there is nothing else
   to page through and no way for a player to refuse the lesson. The lesson runs
   inside the Level 1 scene (flag on GameManager), so no extra scene assets exist;
   finishing it loads Level 1 by itself after ~3 s.
   Smoke variants: the default run sets `IntroFlowUI.DevSkipTutorial` and spends
   its window in Level 1; `-smokeTutorial` walks the lesson — it selects what step
   1 asks for and activates what steps 2-3 ask for, and fails the run if a step's
   card is not on screen.
1. Open the project in Unity **6000.5.3f1**.
2. If scenes/prefabs are missing (first checkout), run **Tools ▸ Last Shift ▸ Build All** —
   it regenerates all scenes, prefabs, ScriptableObjects and Build Settings.
3. Open `Assets/Scenes/Boot.unity`.
4. Press **Play**. The title card loads Level 1 automatically.
5. Use **Up/Down/Enter** to drive the factory terminal on the left.
   - Selected machine gets a bright ring in the room, plus an effect/route preview.
   - Watch **«РЕШИМОСТЬ ИНЖЕНЕРА»** in the lower-left detail panel — since Pass 9
     there is no top HUD strip at all (Room Pressure was removed in Pass 8.1 —
     machinery only ever fires on the player's command).
   - Repair progress appears as a local «РЕМОНТ» plate above the console the
     engineer is actually working on; there is no global repair bar.
   - When Resolve hits 0 the exit unlocks and the engineer retreats; when he reaches
     the exit the industrial victory animation plays, then
     **ИНЖЕНЕР ОТСТУПИЛ → ENTER — СЛЕДУЮЩИЙ ЦЕХ**.
6. If the engineer completes all repairs, you lose the room: **ЦЕХ СТАБИЛИЗИРОВАН**
   on an opaque terminal panel, with the single-option «ПОВТОРИТЬ ЦЕХ» menu.
7. Play Mode smoke tests (batch Unity, exits nonzero on runtime errors):

   ```bash
   UNITY="/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity"
   "$UNITY" -batchmode -projectPath <path> \
     -executeMethod LastShift.EditorTools.PlayModeSmokeTest.Run -logFile smoke.log
   # Variants:
   #   -smokeScene Level_02_PackagingLine              regression on another room
   #   -smokeTutorial                                   takes «ПРОЙТИ УРОК», walks the
   #                                                    lesson and checks callout layout
   #   -smokeScene Level_03_ColdStorage -smokeFinal     final «ЗАВОД ПОБЕДИЛ» + victory
   #                                                    animation gating (needs the FINAL room)
   #   -smokeScene Level_01_RawMilkIntake -smokeDefeat  restart/quit menu end-to-end
   #   -smokeSeconds 110                                longer play window; with
   #                                                    -smokeTutorial it walks the whole
   #                                                    lesson (default window is 35 s)
   # Grep the log for SMOKE_RESULT / SMOKE_ERR / SMOKE_FAIL.
   ```

   `-smokeFinal` and `-smokeDefeat` drive a room directly and do not walk the Boot
   flow, so they must be given a `-smokeScene`.

Each level scene can also be played directly (Play from any `Level_*` scene) —
managers bootstrap themselves.

## Scenes

| Scene | Room | Notes |
|-------|------|-------|
| `Boot` | Title card | Initializes GameManager, loads Level 1 |
| `Level_01_RawMilkIntake` | RAW MILK INTAKE | Tutorial: conveyors, security gate, sorting arm, forklift, cleaning spray, steam purge, alarm |
| `Level_02_PackagingLine` | PACKAGING LINE | Fast arcade: high-speed conveyors, press modules, packing arms, scanner gate, pallet mover, emergency door |
| `Level_03_ColdStorage` | COLD STORAGE | Strategic: freezer fog, ice floor, cold doors, inventory drone, pallet-shifting stacker, final end panel |

## Project structure

```
Assets/
  Documentation/       README.md, BuildLog.txt
  Prefabs/             Engineer, Machines, Hazards, Objectives, UI (generated by ProjectBuilder)
  Scenes/              Boot + 3 level scenes (generated by ProjectBuilder)
  ScriptableObjects/   EngineerData, EscalationData, MachineData per type, LevelData per room, PrefabLibrary
  Scripts/
    Core/       GameManager, LevelManager, SceneLoader, RoomPressureController,
                RoomEscalationController, RoomBuilder, PrefabFactories, BootController
    Engineer/   EngineerController, EngineerStateMachine, EngineerState(+states),
                EngineerStats, EngineerNavigation, EngineerStateUI
    Machines/   InteractableMachine, FactoryCommandTerminal, FactoryCommandItem,
                DoorMachine, ConveyorMachine, RoboticArmMachine, MobileUnitMachine,
                DroneMachine, AlarmMachine, HazardEmitterMachine
    Hazards/    HazardZone (+factory), SteamHazard, ColdHazard, SlipperyFloor
    Objectives/ ObjectiveNode, RepairObjective, ExitDoor
    UI/         UIBuilder, HUDController, CommandTerminalUI, CommandListItemUI,
                EndRoomPanel
    Data/       LayoutTypes, LevelLayouts, LevelData, MachineData, EngineerData,
                EscalationData, PrefabLibrary
    Input/      ArcadeInputBridge (arcade-controls package -> logical actions),
                UnifiedGameInput, InputRepeatController
    Utilities/  GameInput, SpriteFactory, PlaceholderVisual, PathGrid, GridBlocker
    Editor/     ProjectBuilder (project generator)
```

## Architecture decisions

- **Navigation: custom grid A\*** (`PathGrid`), not NavMesh. Unity has no built-in 2D
  NavMesh and NavMeshPlus is a third-party package; a 0.5-unit grid with dynamic
  blocked cells handles doors, pallets and moving forklifts deterministically and
  triggers engineer repathing via a single `GridChanged` event.
- **Room geometry lives in code** (`LevelLayouts`): rooms are generated at scene start
  by `RoomBuilder` from data specs. Scenes stay tiny (camera + LevelRoot) and can
  never hold broken references.
- **Placeholder art is generated at runtime** (`SpriteFactory`): solid squares,
  circles, rings and arrows, unlit material. No imported art assets needed.
- **UI is built in code** (`UIBuilder`) using uGUI + the built-in LegacyRuntime font.
- **Prefabs are bare component carriers** (visuals attach themselves at runtime), saved
  by `ProjectBuilder` and wired through the `PrefabLibrary` ScriptableObject. Every
  prefab entry has a code-factory fallback, so a missing reference degrades gracefully
  instead of breaking.
- **Input**: one shared logical-action flow (`GameInput`: NavigatePrevious/NavigateNext/
  Submit). The only physical source is the arcade-controls package (`ArcadeInput`),
  bridged by `ArcadeInputBridge` into frame-stamped `UnifiedGameInput` actions before
  consumers poll. At most one navigation action per frame; Submit fires on the Red
  button's press edge. No game code reads the keyboard or mouse directly.
- **Events**: C# events throughout (machine `Activated`, stats `ResolveChanged`/
  `ResolveEmpty`, objective `CompletedEvent`, grid `GridChanged`).

## Game systems summary

- **Resolve (0–100)**: drops from grabs/stuns (−5), hazard contact (−4…−8), blocked
  routes (−4), no valid path (−6 per 4 s), damaged objectives (−10), escalation (−20),
  alarm/drone exposure (gradual). At 0 → exit unlocks, engineer retreats permanently.
- **Pressure/escalation: removed (Pass 8.1)**. `RoomPressureController` and
  `RoomEscalationController` are no longer created by LevelManager; the
  `escalationAuto`/`escalationExpand` flags in LevelLayouts are inert. The room
  is decided purely by Resolve vs repairs.
- **Engineer FSM**: EnterRoom → Assess → MoveToObjective → Repair, with AvoidHazard,
  Repath (shows "Blocked" when stuck), Stunned, Panic (fast but sloppy pathing),
  BackOff («Отходит»: short step-away-and-return after a shock), RetreatToExit,
  Escape. He is stubborn: he keeps repairing until Resolve breaks — but after a
  stun or panic he prefers the nearest *other* unfinished repair point (objectives
  complete in any order), and when only one is left he briefly backs off and
  returns, giving the factory another interception window. Repair panels are red
  until repaired, green after.

## Known limitations

- Placeholder visuals only; prefabs look empty in the editor (visuals are runtime-built).
- Audio is fully procedural/synthesized (first pass): functional and atmospheric, but
  not recorded foley. The mixer's GUI group views list is empty (cosmetic; groups and
  exposed parameters work — re-add a view in the Audio Mixer window if you want one).
  Volume settings persist via PlayerPrefs when available, otherwise per-session.
- Engineer movement is waypoint-grid based; motion can look slightly angular.
- Balance is first-pass: a room takes roughly 2–4 minutes with active sabotage.
- Mouse is intentionally unsupported.
- If you regenerate with **Build All** while a generated scene is open, reopen the scene.
