# LAST SHIFT / ПОСЛЕДНЯЯ СМЕНА

Top-down 2D игра-саботаж: **ты — завод**. Последний дежурный инженер пытается
починить автоматическую молочную линию и вернуть ручное управление; ты ломаешь
его **решимость** машинами из терминала, пока он не отступит к выходу. Три цеха —
Boot → Level 1 (Приёмка) → Level 2 (Фасовка) → Level 3 (Холодный склад).

Весь текст для игрока — русский; идентификаторы в коде — английские.

## Требования

- Unity **6000.5.3f1**.
- Рендер: **URP (2D Renderer)** — пайплайн уже настроен в проекте
  (`Assets/FactoryGame/Settings/UniversalRP.asset` + `Renderer2D.asset`),
  Built-in не используется.

## Как запустить

1. Открыть **корень репозитория** в Unity Hub (Unity 6000.5.3f1).
2. Entry-сцена — `Assets/FactoryGame/Scenes/Boot.unity` (уже в Build Settings,
   первой). Play.
3. Управление (клавиатура; опционально Arduino-контроллер — джойстик + 2 кнопки):
   - **↑ / ↓** — предыдущий / следующий пункт (команда терминала, пункт меню),
   - **Enter** — подтвердить / активировать,
   - **Escape** — пауза / «назад».
   - Мышь не используется. Клавиши Q и R намеренно ни к чему не привязаны.

Почти весь контент генерируется из кода: сцены крошечные (камера + LevelRoot),
геометрия комнат, спрайты, UI и 54 звука собираются в рантайме. Пересобрать всё
из редактора: **Tools ▸ Last Shift ▸ Build All** (сцены/префабы/SO/Build Settings)
и **Tools ▸ Last Shift ▸ Build Audio** (WAV/микшер/AudioLibrary).

## Структура

Вся игра — один самодостаточный пакет в `Assets/FactoryGame/`:

- `Scripts/` — код в собственных сборках: `FactoryGame.Runtime.asmdef` и
  `Scripts/Editor/FactoryGame.Editor.asmdef` (namespace-ы `LastShift.*`).
- `Scenes/` — Boot + три уровня. `Prefabs/`, `ScriptableObjects/`, `Art/`,
  `Audio/`, `Resources/`, `Settings/` (URP-ассеты), `Documentation/`.
- Полная проектная документация и история сборок — в
  `Assets/FactoryGame/Documentation/README.md`.

Прошивка контроллера — `my_ardruino_sketch/sketch_game_jul15a.ino`
(CSV-протокол `JOY,…`/`BTN,…` @115200).

## Проверка (batch, без открытого редактора)

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity"
# Компиляция (exit 0, без ошибок/предупреждений CS)
"$UNITY" -batchmode -quit -projectPath . -logFile compile.log
# Play Mode smoke (exit != 0 при рантайм-ошибке)
"$UNITY" -batchmode -projectPath . \
  -executeMethod LastShift.EditorTools.PlayModeSmokeTest.Run -logFile smoke.log
```

## Статус интеграции в аркадный автомат

Сделано: URP 2D, Unity 6000.5.3f1, весь контент в одной папке-пакете со своими
asmdef. **Ещё не сделано** (см. `system/specs/ARCADE_INTEGRATION_CONTRACT.md`):
ввод пока читается напрямую с клавиатуры/Input System — миграция на пакет
`arcade-controls` (`ArcadeInput.*`), `package.json`/`GameManifest`, обработка
`MenuButton` (выход в лаунчер) — отдельным шагом.
