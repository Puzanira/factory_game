# LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА — Vertical Slice

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
> is now **«ЗАВОД ПОБЕДИЛ»** with a Up/Down/Enter menu (since Pass 7:
> «ПОВТОРИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ») and a red-to-green calm-down light transition.
> The engineer is a readable top-down human worker: helmet with lamp + brim, head,
> orange jacket with hi-vis stripes (torso larger than head), dark trousers with
> alternating legs and boots, soft shadow, warm readability rim, helmet point light;
> state effects (repair lean + tool flash, panic red rim, stun yellow flicker + shake,
> cold/steam rim tints, slippery wobble, brief green evacuation arrow on retreat).
> Engineer renders on the **Characters** sorting layer (fog/decals can never hide him);
> state label on **WorldUI**. Sorting layers Floor…ScreenUI are created by ProjectBuilder.

## Controls (keyboard + optional Arduino controller — no mouse needed)

Every screen is driven by the same small set of logical actions; the mouse is never
used. All input polling is centralized in `Scripts/Utilities/GameInput.cs`, which
merges the keyboard with the optional Arduino controller (see the Russian section
«ПОДКЛЮЧЕНИЕ ARDUINO-КОНТРОЛЛЕРА» below). The keyboard is always fully functional,
with or without the controller.

### Gameplay (factory terminal)

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Up Arrow** | Select previous ready factory command (wraps, skips cooldowns) |
| **Down Arrow** | Select next ready factory command                |
| **Enter** (incl. numpad) | Activate the selected command          |
| **Escape**   | Open the pause menu                                |

Room restart is available through the pause menu («ПОВТОРИТЬ ЦЕХ») and through
the end-of-room menu after a defeat or the final victory.

### Pause menu

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Up / Down** | Select entry (ПРОДОЛЖИТЬ / НАСТРОЙКИ ЗВУКА / ПОВТОРИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ) |
| **Enter**    | Confirm entry; in «НАСТРОЙКИ ЗВУКА» cycles the selected volume 0→25→50→75→100% |
| **Escape**   | Back (sound settings → pause menu → resume game)   |

### Title card & intro briefing (Boot)

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Up / Down** | Move selection in the title menu and ДА/НЕТ modals |
| **Enter**    | Confirm / next briefing page / start the shift     |
| **Escape**   | «ВЫЙТИ ИЗ ИГРЫ?» on the title, «ПРОПУСТИТЬ ИНСТРУКТАЖ?» in the briefing |

### End-of-room screens

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Enter**    | Next room (after «ИНЖЕНЕР ОТСТУПИЛ»)               |
| **Up / Down / Enter** | Navigate/confirm the «ПОВТОРИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ» menu (after «ЦЕХ СТАБИЛИЗИРОВАН» and on the final «ЗАВОД ПОБЕДИЛ» screen) |
| **Escape**   | In that menu: jump straight to «ВЫЙТИ ИЗ ИГРЫ»     |

## УПРАВЛЕНИЕ

Вся игра управляется тремя действиями: **предыдущий / следующий / подтвердить**
(плюс Escape — пауза и «назад»).

Клавиатура:
- Стрелка вверх — предыдущая система / пункт меню
- Стрелка вниз — следующая система / пункт меню
- Enter — подтвердить / активировать

Arduino Nano:
- Джойстик вверх или влево — предыдущая система / пункт меню
- Джойстик вниз или вправо — следующая система / пункт меню
- Кнопка на джойстике — подтвердить / активировать
- Отдельная кнопка — подтвердить / активировать

### МЕНЮ ПОСЛЕ ЗАВЕРШЕНИЯ ЦЕХА

После поражения («ЦЕХ СТАБИЛИЗИРОВАН») и на финальном экране («ЗАВОД ПОБЕДИЛ»)
под текстом результата появляется меню из двух пунктов (стрелки/джойстик — выбор
с закольцовкой, Enter/кнопка — подтвердить; по умолчанию выбран первый пункт):

```
> ПОВТОРИТЬ ЦЕХ
  ВЫЙТИ ИЗ ИГРЫ
```

- «ПОВТОРИТЬ ЦЕХ» — начать текущий цех заново.
- «ВЫЙТИ ИЗ ИГРЫ» — завершить работу игры.

Тот же перезапуск и выход доступны в меню паузы (Escape во время игры).

> Изменение схемы управления: **потенциометр, а также клавиши Q и R удалены из
> схемы управления.** **Клавиши Q и R не используются.**

## ПОДКЛЮЧЕНИЕ ARDUINO-КОНТРОЛЛЕРА

Игра поддерживает самодельный контроллер на **Arduino Nano** как *дополнительный*
способ управления. Клавиатура работает всегда: без контроллера, при неверном COM-порте
и даже если контроллер выдернули прямо во время игры.

### Где что лежит

- Скетч Arduino: `my_ardruino_sketch/sketch_game_jul15a.ino`
- Референс подключения Unity ↔ Arduino: `for_claude_example_unity_grabber/`
- Скрипты интеграции: `Assets/Scripts/Input/`
- Настройки (порт, пороги, инверсия осей): `Assets/Resources/ArduinoSerialSettings.asset`
  (создаётся автоматически; можно пересоздать через меню **LastShift ▸ Create Arduino Serial Settings**)

### Железо и протокол (из скетча)

- Плата: **Arduino Nano** (при проблемах с прошивкой: Tools ▸ Processor ▸ **ATmega328P (Old Bootloader)**)
- Скорость: **115200 бод** (`Serial.begin(115200)`)
- Пины: джойстик X — **A6**, джойстик Y — **A5**, кнопка джойстика — **D2**,
  отдельная кнопка — **D6** (все кнопки на `INPUT_PULLUP`)
- Протокол (CSV, строки только при изменении значений, ~33 Гц максимум):
  `JOY,x,y` · `JOY,DOWN` / `JOY,UP,мс` · `BTN,DOWN` / `BTN,UP,мс` / `BTN,HELD,мс`
- Дебаунс кнопок (25 мс) и пороги дребезга уже реализованы в скетче.
- Если на плате осталась старая прошивка с сообщениями `POT,…`, игра их безопасно
  игнорирует и один раз пишет предупреждение в консоль — перепрошейте плату актуальным
  скетчем.

### Как запустить

1. **Прошивка**: открыть `my_ardruino_sketch/sketch_game_jul15a.ino` в Arduino IDE,
   выбрать Tools ▸ Board ▸ **Arduino Nano**, нужный порт, нажать **Upload**.
   (Библиотеки не нужны. На macOS для клона CH340 может понадобиться драйвер —
   `for_claude_example_unity_grabber/CH341SER_MAC.ZIP`.)
2. **Подключение**: воткнуть Nano в USB. Закрыть Serial Monitor в Arduino IDE —
   порт может быть открыт только одной программой!
3. **COM-порт**: по умолчанию игра ищет контроллер **автоматически** по всем
   USB-serial портам. Чтобы задать порт вручную, впишите его в поле **Port Name**
   ассета `Assets/Resources/ArduinoSerialSettings.asset`
   (Windows: `COM5`; macOS: `/dev/cu.usbserial-110`). Пустое поле = автопоиск.
4. **Запуск Unity**: открыть `Assets/Scenes/Boot.unity` → нажать **Play**.
   В консоли появится `[Arduino] Controller connected on <порт> @ 115200 baud`.

### Раскладка контроллера

```
ДЖОЙСТИК ВВЕРХ / ВЛЕВО — предыдущая система
ДЖОЙСТИК ВНИЗ / ВПРАВО — следующая система
КНОПКА НА ДЖОЙСТИКЕ ИЛИ ВНЕШНЯЯ КНОПКА — активировать

Клавиша Up    — предыдущая система / пункт меню
Клавиша Down  — следующая система / пункт меню
Клавиша Enter — подтвердить / активировать
Escape        — пауза / назад
```

Поведение: удержание джойстика повторяет шаг (задержка 0.40 с, далее каждые 0.15 с);
диагональ даёт ровно одно действие (при равном отклонении приоритет у вертикали).
Удержание кнопок не повторяет Enter; обе кнопки, нажатые одновременно, дают один Enter.
Если направления джойстика перепутаны из-за монтажа — включите
`Invert X / Invert Y` в настройках.

### Если не работает

- **Контроллер не найден**: проверьте кабель (нужен data-кабель, не «только зарядка»),
  закройте Serial Monitor/Plotter, перезапустите Play. Игра продолжает искать порт
  каждую секунду — можно втыкать контроллер прямо во время игры.
- **Неверный COM-порт**: очистите поле Port Name (автопоиск) или впишите правильный.
  Список портов: Arduino IDE ▸ Tools ▸ Port.
- **Порт занят** («could not open», «busy»): порт держит другая программа
  (Serial Monitor, второй Unity). Закройте её; игра сама переподключится.
- **Клавиатура**: работает всегда, ничего включать не нужно. При отключении
  контроллера меню не блокируются, исключений нет.
- **Отладка**: нажмите **F9** в игре — оверлей покажет
  «ARDUINO: ПОДКЛЮЧЕН» / «ARDUINO: НЕ НАЙДЕН — КЛАВИАТУРА АКТИВНА», порт, скорость,
  последнее сообщение, направление джойстика и состояние кнопок.
  (Или включите Debug Overlay в `ArduinoSerialSettings.asset`.)

## How to test

0. Flow: **Boot → Title Card → Intro Briefing (4 pages, Enter/Esc) → Level 1**.
1. Open the project in Unity **6000.3.19f1**.
2. If scenes/prefabs are missing (first checkout), run **Tools ▸ Last Shift ▸ Build All** —
   it regenerates all scenes, prefabs, ScriptableObjects and Build Settings.
3. Open `Assets/Scenes/Boot.unity`.
4. Press **Play**. The title card loads Level 1 automatically.
5. Use **Up/Down/Enter** to drive the factory terminal on the left.
   - Selected machine gets a bright ring in the room, plus an effect/route preview.
   - Watch **ENGINEER RESOLVE** and **ROOM PRESSURE** in the top HUD.
   - At 100 Pressure the room escalates (red overlay, machines fire on their own).
   - When Resolve hits 0 the exit unlocks and the engineer retreats; when he reaches
     the exit you get **ENGINEER RETREATED → ENTER — NEXT ROOM**.
6. If the engineer completes all repairs, you lose the room: **ROOM STABILIZED**,
   with the «ПОВТОРИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ» menu below the result text.

Each level scene can also be played directly (Play from any `Level_*` scene) —
managers bootstrap themselves.

## Scenes

| Scene | Room | Notes |
|-------|------|-------|
| `Boot` | Title card | Initializes GameManager, loads Level 1 |
| `Level_01_RawMilkIntake` | RAW MILK INTAKE | Tutorial: conveyors, security gate, sorting arm, forklift, cleaning spray, alarm |
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
                PauseMenuUI, EndRoomPanel
    Data/       LayoutTypes, LevelLayouts, LevelData, MachineData, EngineerData,
                EscalationData, PrefabLibrary
    Input/      ArduinoControllerReader (serial, background thread), ArduinoInputParser,
                ArduinoInputBridge, UnifiedGameInput, InputRepeatController,
                SerialConnectionSettings, ArduinoDebugStatus
    Utilities/  GameInput, SpriteFactory, PlaceholderVisual, PathGrid, GridBlocker
    Editor/     ProjectBuilder (project generator), ArduinoSetup (settings asset)
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
  Submit/Back/Restart). Physical sources — keyboard (new Input System, `Keyboard.current`)
  and the optional Arduino controller (`Scripts/Input/`, serial port read on a background
  thread, lines parsed and published as frame-stamped actions before consumers poll).
  At most one navigation action per frame and one Submit per shared debounce window;
  keyboard is the permanent fallback and is never disabled.
- **Events**: C# events throughout (machine `Activated`, stats `ResolveChanged`/
  `ResolveEmpty`, objective `CompletedEvent`, grid `GridChanged`).

## Game systems summary

- **Resolve (0–100)**: drops from grabs/stuns (−5), hazard contact (−4…−8), blocked
  routes (−4), no valid path (−6 per 4 s), damaged objectives (−10), escalation (−20),
  alarm/drone exposure (gradual). At 0 → exit unlocks, engineer retreats permanently.
- **Pressure (0–100)**: rises only from *meaningful* interference (stuns, forced
  repaths, hazard hits, interrupted repairs), never from merely activating a machine.
  At 100 → escalation: big Resolve hit, objective damage, hazards expand, flagged
  machines auto-fire until the room ends.
- **Engineer FSM**: EnterRoom → Assess → MoveToObjective → Repair, with AvoidHazard,
  Repath (shows "Blocked" when stuck), Stunned, Panic (fast but sloppy pathing),
  RetreatToExit, Escape. He is stubborn: he keeps repairing until Resolve breaks.

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
