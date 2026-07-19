# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА** — top-down 2D sabotage game, Unity **6000.3.19f1** (URP 2D). The player *is* the factory, driving machines from a terminal to break the engineer's resolve. All player-facing text is Russian; code identifiers are English. Full design/controls doc: `Assets/Documentation/README.md`; change history: `Assets/Documentation/BuildLog.txt` (append a dated entry per pass).

## Hard constraints

- **Q and R must never do anything**: not bound, not polled, not shown in UI or docs. Potentiometer input was removed — do not reintroduce either.
- The entire game runs on three logical actions — NavigatePrevious / NavigateNext / Submit (keyboard ↑/↓/Enter; Arduino joystick + two buttons) — plus Escape for pause/back. No mouse, no new hotkeys.
- Every player-facing string lives in `Assets/Scripts/Data/Loc.cs` (or `LevelLayouts.cs` room specs) — never hardcode UI text.

## Commands

```bash
# Compile check (works while the Unity editor holds the project lock)
dotnet build Assembly-CSharp.csproj -v q -nologo
dotnet build Assembly-CSharp-Editor.csproj -v q -nologo

# Play Mode smoke tests (batch Unity; exits nonzero on runtime errors)
UNITY="/Applications/Unity/Hub/Editor/6000.3.19f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath <path> -executeMethod LastShift.EditorTools.PlayModeSmokeTest.Run -logFile smoke.log
# Variants: -smokeScene Level_02_PackagingLine | -smokeFinal (final victory screen)
#           -smokeDefeat (drives the restart/quit menu end-to-end via UnifiedGameInput)
#           -smokeTutorial (takes «ПРОЙТИ УРОК» at the choice screen and validates the
#           interactive tutorial room; the default Boot run picks «СРАЗУ К СМЕНЕ»)
# Grep the log for SMOKE_RESULT / SMOKE_ERR.
```

If the user's Unity editor has the project open (`Temp/UnityLockfile` exists), batch mode can't run on the same path — rsync `Assets Packages ProjectSettings Library UserSettings` to a scratch copy and run there (Library copy keeps import fast).

In-editor tools: **Tools ▸ Last Shift ▸ Build All** regenerates all scenes, prefabs, ScriptableObjects and Build Settings (`ProjectBuilder`); **Tools ▸ Last Shift ▸ Build Audio** regenerates WAVs/mixer/AudioLibrary. There is no NUnit test suite.

## Architecture

**Almost everything is generated in code.** Scenes are tiny (camera + LevelRoot): room geometry comes from `LevelLayouts` specs via `RoomBuilder` at scene start; sprites from `SpriteFactory`; UI from `UIBuilder` (uGUI, built at runtime, no EventSystem anywhere — input is polled); all 54 sounds are synthesized (`SfxSynth`). Prefabs are bare component carriers with code-factory fallbacks. Editing a scene file directly is almost never the right move.

**Input funnel** — one path for every screen:
- `Scripts/Utilities/GameInput.cs` is the single polling surface (`UpPressed`/`DownPressed`/`ConfirmPressed`/`EscapePressed`). It ORs raw keyboard (new Input System) with `Scripts/Input/UnifiedGameInput` frame-stamped flags.
- `ArduinoInputBridge` (persistent, execution order −100, auto-bootstraps, never duplicates) reads the serial controller on a background thread and publishes into `UnifiedGameInput` before consumers poll. A shared debounce covers both hardware buttons *and* keyboard Enter, so a Submit can never double-fire.
- Anything that publishes into `UnifiedGameInput` must do it from the player loop *before* consumers run (see `SmokeInputDriver` in `PlayModeSmokeTest.cs`).

**Per-room flow**: `LevelManager` (one per level scene) builds the room, spawns the engineer, owns pause/end-of-room state, and routes all input in `Update()` — menus like `PauseMenuUI`/`EndRoomPanel` are pure views whose selection is driven by LevelManager polling `GameInput`. `GameManager` (persistent) holds scene-name constants; `SceneLoader.Reload()` restarts the current room (always resets `Time.timeScale`); `SceneLoader.Quit()` wraps `Application.Quit` / editor-stop behind `#if UNITY_EDITOR`.

**End screens**: `EndRoomPanel` shows win («ИНЖЕНЕР ОТСТУПИЛ», Enter → next room), defeat («ЦЕХ СТАБИЛИЗИРОВАН») and final victory («ЗАВОД ПОБЕДИЛ»). Defeat and final share one two-option menu — «ПОВТОРИТЬ ЦЕХ» (reload current room) / «ВЫЙТИ ИЗ ИГРЫ» — handled by `LevelManager.HandleEndMenu()`; initial selection is always «ПОВТОРИТЬ ЦЕХ», navigation wraps, Esc jumps to quit. The pause menu (Esc) reuses the same labels.

**Audio**: persistent `AudioManager` singleton; `OnSceneLoaded` stops all loops and clears the pause duck, so restarts can't leak audio state. Per-room ambience lives in `RoomAudioDirector` (created by LevelManager, dies with the scene).

**Engineer**: FSM (`EngineerStateMachine`) over a custom 0.5-unit A* grid (`PathGrid`, not NavMesh); machines block/alter cells and a single `GridChanged` event triggers repathing. Resolve (engineer) and Pressure (room) in `EngineerStats`/`RoomPressureController` drive the win/lose loop.

## Conventions

- Namespaces follow folders: `LastShift.Core`, `LastShift.UI`, `LastShift.Input`, `LastShift.Data`, `LastShift.Audio`, `LastShift.Engineer`, `LastShift.Machines`, `LastShift.Utilities`, `LastShift.EditorTools`.
- Menus render selection as `> ТЕКСТ <` with amber-green highlight + background strip; idle rows dimmed — keep this terminal style for any new UI.
- Persistent singletons (GameManager, AudioManager, ArduinoInputBridge) guard against duplicates in `Awake`; anything else must die with its scene.
- `my_ardruino_sketch/sketch_game_jul15a.ino` is the controller firmware (CSV protocol `JOY,…`/`BTN,…` @115200); obsolete `POT,*` lines are deliberately ignored with one warning — keep that tolerance.
