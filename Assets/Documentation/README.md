# LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА — Vertical Slice

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
> Enter cycles, Esc back); the old R/Q pause shortcuts still work.

> **Pass 3 (2026-07-12):** Boot flow is now **Title Card «ПОСЛЕДНЯЯ СМЕНА» → 4-page
> Russian intro briefing → Level 1** (Enter advances pages, Esc opens «ПРОПУСТИТЬ
> ИНСТРУКТАЖ?», all keyboard-only, shown on every new game). The final Level-3 screen
> is now **«ЗАВОД ПОБЕДИЛ»** with a Up/Down/Enter menu («НАЧАТЬ ЗАНОВО / ВЕРНУТЬСЯ К
> ПЕРВОМУ ЦЕХУ / ВЫЙТИ ИЗ ИГРЫ») and a red-to-green calm-down light transition.
> The engineer is a readable top-down human worker: helmet with lamp + brim, head,
> orange jacket with hi-vis stripes (torso larger than head), dark trousers with
> alternating legs and boots, soft shadow, warm readability rim, helmet point light;
> state effects (repair lean + tool flash, panic red rim, stun yellow flicker + shake,
> cold/steam rim tints, slippery wobble, brief green evacuation arrow on retreat).
> Engineer renders on the **Characters** sorting layer (fog/decals can never hide him);
> state label on **WorldUI**. Sorting layers Floor…ScreenUI are created by ProjectBuilder.

## Controls (keyboard only — no mouse needed)

Every screen is driven by the same small set of keys; the mouse is never used.
All key polling is centralized in `Scripts/Utilities/GameInput.cs`.

### Gameplay (factory terminal)

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Up Arrow** | Select previous ready factory command (wraps, skips cooldowns) |
| **Down Arrow** | Select next ready factory command                |
| **Enter** (incl. numpad) | Activate the selected command          |
| **Escape**   | Open the pause menu                                |
| **R**        | Restart the current room                           |
| **Space** (hold) | Dev-only 3× time speed-up (never required)     |

### Pause menu

| Key          | Action                                             |
|--------------|----------------------------------------------------|
| **Up / Down** | Select entry (ПРОДОЛЖИТЬ / НАСТРОЙКИ ЗВУКА / ПЕРЕЗАПУСТИТЬ ЦЕХ / ВЫЙТИ ИЗ ИГРЫ) |
| **Enter**    | Confirm entry; in «НАСТРОЙКИ ЗВУКА» cycles the selected volume 0→25→50→75→100% |
| **Escape**   | Back (sound settings → pause menu → resume game)   |
| **R** / **Q** | Legacy shortcuts on the main pause screen: restart / quit |

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
| **R**        | Retry the room (incl. after «ЦЕХ СТАБИЛИЗИРОВАН»)  |
| **Q**        | Quit (after «ЦЕХ СТАБИЛИЗИРОВАН»)                  |
| **Up / Down / Enter** | Navigate/confirm the final «ЗАВОД ПОБЕДИЛ» menu |
| **Escape**   | On the final screen: jump straight to «ВЫЙТИ ИЗ ИГРЫ» |

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
6. If the engineer completes all repairs, you lose the room: **ROOM STABILIZED** (R to retry).

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
- **Input**: new Input System (`Keyboard.current`), keyboard only.
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
