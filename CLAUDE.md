# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА** — top-down 2D sabotage game, Unity **6000.5.3f1** (URP 2D). The player *is* the factory, driving machines from a terminal to break the engineer's resolve. All player-facing text is Russian; code identifiers are English. Full design/controls doc: `Assets/Documentation/README.md`; change history: `Assets/Documentation/BuildLog.txt` (append a dated entry per pass).

## Hard constraints

- **Q and R must never do anything**: not bound, not polled, not shown in UI or docs. Potentiometer input was removed — do not reintroduce either.
- The entire game runs on three logical actions — NavigatePrevious / NavigateNext / Submit (keyboard ↑/↓/Enter; Arduino joystick + two buttons) — plus Escape for pause/back. No mouse, no new hotkeys.
- Every player-facing string lives in `Assets/FactoryGame/Scripts/Data/Loc.cs` (or `LevelLayouts.cs` room specs) — never hardcode UI text.
- **The red button has exactly two verbs**: «КРАСНАЯ КНОПКА — ДАЛЕЕ» on a screen (`Loc.FooterNext`), «КРАСНАЯ КНОПКА — ВКЛЮЧИТЬ» in the room (`Loc.TutActFooter`). It used to have five; `OnboardingTextTests` fails the build on a third.

## Commands

```bash
# Compile check (works while the Unity editor holds the project lock)
dotnet build Assembly-CSharp.csproj -v q -nologo
dotnet build Assembly-CSharp-Editor.csproj -v q -nologo

# Play Mode smoke tests (batch Unity; exits nonzero on runtime errors)
UNITY="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -projectPath <path> -executeMethod LastShift.EditorTools.PlayModeSmokeTest.Run -logFile smoke.log
# Variants: -smokeScene Level_02_PackagingLine
#           -smokeScene Level_03_ColdStorage -smokeFinal (final «ЗАВОД ПОБЕДИЛ» screen +
#           victory-animation gating; needs the FINAL room, not Boot/Level 1)
#           -smokeScene Level_01_RawMilkIntake -smokeDefeat (drives the restart/quit menu
#           end-to-end via UnifiedGameInput; does not walk the Boot flow)
#           -smokeTutorial (walks the three-step lesson: selects the system step 1
#           asks for, activates what steps 2-3 ask for, asserts callout layout; the
#           default Boot run sets IntroFlowUI.DevSkipTutorial and goes to Level 1)
#           -smokeSeconds 110 (longer play window; with -smokeTutorial leaves room for
#           the engineer to walk into each step's situation)
#           -smokeShots <dir> [-smokeShotAt 1,4,13,21] (1920×1080 PNG frames at those
#           play-time seconds — the only way a headless session can look at the screen
#           it just changed; run WITHOUT -nographics, see SmokeShots.cs)
# Grep the log for SMOKE_RESULT / SMOKE_ERR / SMOKE_FAIL / SMOKE_SHOT.
```

If the user's Unity editor has the project open (`Temp/UnityLockfile` exists), batch mode can't run on the same path — rsync `Assets Packages ProjectSettings Library UserSettings` to a scratch copy and run there (Library copy keeps import fast).

In-editor tools: **Tools ▸ Last Shift ▸ Build All** regenerates all scenes, prefabs, ScriptableObjects and Build Settings (`ProjectBuilder`); **Tools ▸ Last Shift ▸ Build Audio** regenerates WAVs/mixer/AudioLibrary. EditMode tests live in `Assets/FactoryGame/Tests/EditMode` (`ArcadeContractTests` guards contract §5 — no `Application.Quit`, no self-exit menu item); run them with `-runTests -testPlatform EditMode`.

## Architecture

**Almost everything is generated in code.** Scenes are tiny (camera + LevelRoot): room geometry comes from `LevelLayouts` specs via `RoomBuilder` at scene start; sprites from `SpriteFactory`; UI from `UIBuilder` (uGUI, built at runtime, no EventSystem anywhere — input is polled); all 54 sounds are synthesized (`SfxSynth`). Prefabs are bare component carriers with code-factory fallbacks. Editing a scene file directly is almost never the right move.

**Input funnel** — one path for every screen:
- `Scripts/Utilities/GameInput.cs` is the single polling surface (`UpPressed`/`DownPressed`/`ConfirmPressed`/`EscapePressed`). It ORs raw keyboard (new Input System) with `Scripts/Input/UnifiedGameInput` frame-stamped flags.
- `ArduinoInputBridge` (persistent, execution order −100, auto-bootstraps, never duplicates) reads the serial controller on a background thread and publishes into `UnifiedGameInput` before consumers poll. A shared debounce covers both hardware buttons *and* keyboard Enter, so a Submit can never double-fire.
- Anything that publishes into `UnifiedGameInput` must do it from the player loop *before* consumers run (see `SmokeInputDriver` in `PlayModeSmokeTest.cs`).

**Per-room flow**: `LevelManager` (one per level scene) builds the room, spawns the engineer, owns pause/end-of-room state, and routes all input in `Update()` — menus like `PauseMenuUI`/`EndRoomPanel` are pure views whose selection is driven by LevelManager polling `GameInput`. `GameManager` (persistent) holds scene-name constants; `SceneLoader.Reload()` restarts the current room (always resets `Time.timeScale`). `SceneLoader` has **no** `Quit()`: on the cabinet the game runs inside the launcher's process, so it must never end it — leaving is the cabinet's «меню» touch button, which the hub owns (ARCADE_INTEGRATION_CONTRACT §5).

**End screens**: `EndRoomPanel` shows win («ИНЖЕНЕР ОТСТУПИЛ», Enter → next room), defeat («ЦЕХ СТАБИЛИЗИРОВАН») and final victory («ЗАВОД ПОБЕДИЛ») on a *fully opaque* terminal panel. Defeat and final share one menu whose single option is «ПОВТОРИТЬ ЦЕХ» (reload current room), handled by `LevelManager.HandleEndMenu()`. **Never add a «выйти» option**: the game does not own the process (see `SceneLoader` above). Every factory win first runs `VictorySequenceController` (~3.5 s); result input is gated behind `LevelManager.VictoryPlaying`.

**Onboarding**: `IntroFlowUI` is **one title card** — name, three lines of briefing (`Loc.TitleBrief`), red button — and then the lesson, on every new game, with no PlayerPrefs. The instruction pages and the «ВВОДНЫЙ УРОК» choice screen were cut after the live-cabinet playtests: people paged through the text without reading it and then skipped the lesson, which is why nobody understood the game. The lesson is now mandatory (there is no control that refuses it — only `IntroFlowUI.DevSkipTutorial` for the harness). All removed wording is archived in `system/handoffs/texts-factory.md` of the studio repo. Do not grow the intro back: how to play is taught by the lesson, with hands. Guarded by `OnboardingTextTests`.

**HUD**: the only permanent readouts are «РЕШИМОСТЬ ИНЖЕНЕРА» and «РЕСУРС УПРАВЛЕНИЯ», built by `TacticalDetailPanelController` in **two different corners** (founder, 2026-09-22): the engineer's resolve top-right of the play area, the control resource at the **bottom of the terminal column**, where it lived before the detail panel was removed (`CommandTerminalUI` stops its command list above it). They briefly shared one top strip; side by side they read as the same kind of thing, and they are not — one is yours, one is his. The lower-left detail panel that used to hold them — plus the selected system's name, purpose and zone status — was removed after the live-cabinet playtest; nothing describes the selected system on screen any more, and the command list now fills the whole terminal below its header. That is deliberate and settled (founder, 2026-09: «наши люди на плейтесте вообще туда не смотрели») — what a system does is said by its command verb instead, so every verb in `LevelLayouts` names the EFFECT on the engineer («Ворота — перекрыть маршрут»), never the mechanism («закрыть»). Repair progress is a local `LocalRepairProgressUI` plate at the console being repaired; the room title only flashes at room start, below the strip. Keep the room title and the toast stack clear of the strip.

**Tutorial**: `TutorialFlowController` (Core) sequences **3 practical steps** — pick the gate in the list (joystick), switch it on (red button), then fire the arm on «ПОДХОДЯЩИЙ МОМЕНТ». Nothing is explained by a card any more: the control resource, the resolve gauge and combinations were dropped from the lesson and are learnt by playing room 1. Presentation is `TutorialStepController` + `TutorialFocusOverlay` / `TutorialHighlightTarget` / `TutorialCalloutPanel` / `TutorialInputLock` (UI). Exactly one target is highlighted at a time; `ShowPractical` takes the footer per step, and that footer must name the control to press. `ShowInfo` (frozen room, wait for Submit) is still there but no step uses it. `TutorialStepController` runs at execution order −60 (after `ArduinoInputBridge` at −100, before the command terminal at 0), so the acknowledging Enter never reaches the terminal. Highlight shape follows the target: rectangles for rectangular objects/UI, corner brackets for irregular ones, a ring only for genuinely round elements — never a generic circle, never a speech bubble.

**Audio**: persistent `AudioManager` singleton; `OnSceneLoaded` stops all loops and clears the pause duck, so restarts can't leak audio state. Per-room ambience lives in `RoomAudioDirector` (created by LevelManager, dies with the scene).

**Engineer**: FSM (`EngineerStateMachine`) over a custom 0.5-unit A* grid (`PathGrid`, not NavMesh); machines block/alter cells and a single `GridChanged` event triggers repathing. Resolve (engineer) and Pressure (room) in `EngineerStats`/`RoomPressureController` drive the win/lose loop.

## Conventions

- Namespaces follow folders: `LastShift.Core`, `LastShift.UI`, `LastShift.Input`, `LastShift.Data`, `LastShift.Audio`, `LastShift.Engineer`, `LastShift.Machines`, `LastShift.Utilities`, `LastShift.EditorTools`.
- Menus render selection as `> ТЕКСТ <` with amber-green highlight + background strip; idle rows dimmed — keep this terminal style for any new UI.
- Persistent singletons (GameManager, AudioManager, ArduinoInputBridge) guard against duplicates in `Awake`; anything else must die with its scene.
- `my_ardruino_sketch/sketch_game_jul15a.ino` is the controller firmware (CSV protocol `JOY,…`/`BTN,…` @115200); obsolete `POT,*` lines are deliberately ignored with one warning — keep that tolerance.
