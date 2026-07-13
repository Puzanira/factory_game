using UnityEngine;

namespace LastShift.Data
{
    /// <summary>
    /// Hard-coded room layouts for the vertical slice. Kept in code (rather than serialized
    /// scene content) so rooms are deterministic, diff-able and easy to tune.
    /// Player-facing strings here are Russian; identifiers stay English.
    /// Balance pass: cooldowns reduced ~25%, repair times raised ~40% (easier, more forgiving).
    /// </summary>
    public static class LevelLayouts
    {
        // Palette
        static readonly Color Steel = new Color(0.60f, 0.64f, 0.68f);
        static readonly Color DarkSteel = new Color(0.38f, 0.41f, 0.45f);
        static readonly Color PalletWood = new Color(0.54f, 0.42f, 0.25f);
        static readonly Color SafetyYellow = new Color(0.91f, 0.76f, 0.29f, 0.85f);
        static readonly Color ExitGreen = new Color(0.35f, 0.85f, 0.45f, 0.9f);

        static Rect R(float cx, float cy, float w, float h) => new Rect(cx - w / 2f, cy - h / 2f, w, h);

        public static RoomLayout Get(int levelIndex)
        {
            switch (levelIndex)
            {
                case 0: return Level1();
                case 1: return Level2();
                default: return Level3();
            }
        }

        // ==================================================================
        // LEVEL 1 — ПРИЁМКА СЫРЬЯ (tutorial: gates, conveyors, arm, forklift)
        // ==================================================================
        static RoomLayout Level1()
        {
            var l = new RoomLayout
            {
                roomName = "ПРИЁМКА СЫРЬЯ",
                goalText = "Восстановить питание внутренних систем завода",
                engineerSpawn = new Vector2(0f, -4.4f),
                exitPos = new Vector2(0f, 5f),
                floorTint = new Color(0.95f, 0.85f, 0.55f, 0.05f), // warm industrial lamps
            };

            // Tanks, pallets and the gate housing that splits the central lane.
            l.obstacles.Add(new RectSpec(new Vector2(-4.2f, -3.4f), new Vector2(1.2f, 1.2f), Steel, true));
            l.obstacles.Add(new RectSpec(new Vector2(5.2f, -3.2f), new Vector2(1.2f, 1.2f), Steel, true));
            l.obstacles.Add(new RectSpec(new Vector2(3.0f, -3.9f), new Vector2(1.6f, 0.9f), PalletWood, true));
            l.obstacles.Add(new RectSpec(new Vector2(-6.0f, 2.6f), new Vector2(1.3f, 1.0f), PalletWood, true));
            l.obstacles.Add(new RectSpec(new Vector2(4.8f, 3.0f), new Vector2(1.1f, 1.0f), PalletWood, true));
            l.obstacles.Add(new RectSpec(new Vector2(0f, 1.6f), new Vector2(0.9f, 1.3f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(0f, -1.6f), new Vector2(0.9f, 1.3f), DarkSteel, true));

            // Safety lines along the pedestrian lane, exit marking.
            l.decor.Add(new RectSpec(new Vector2(0f, 0.55f), new Vector2(14f, 0.07f), SafetyYellow, false, 1));
            l.decor.Add(new RectSpec(new Vector2(0f, -0.55f), new Vector2(14f, 0.07f), SafetyYellow, false, 1));
            l.decor.Add(new RectSpec(new Vector2(0f, 4.55f), new Vector2(2.4f, 0.09f), ExitGreen, false, 1));

            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Conveyor, displayName = "Конвейер А", commandVerb = "сменить направление",
                description = "Развернуть северную ленту. Сдвигает всех, кто на ней стоит.",
                pos = new Vector2(-1f, 1.6f), size = new Vector2(11f, 1.1f),
                conveyorDir = Vector2.right, conveyorSpeed = 2.2f, cooldown = 4.5f,
                escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Conveyor, displayName = "Конвейер Б", commandVerb = "сменить направление",
                description = "Развернуть южную ленту. Сдвигает всех, кто на ней стоит.",
                pos = new Vector2(-1f, -1.6f), size = new Vector2(11f, 1.1f),
                conveyorDir = Vector2.left, conveyorSpeed = 2.2f, cooldown = 4.5f,
                escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Door, displayName = "Защитные ворота", commandVerb = "закрыть",
                description = "Перекрыть или открыть центральный проход.",
                pos = new Vector2(0f, 0f), size = new Vector2(0.9f, 2.0f), cooldown = 5f,
                escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.RoboticArm, displayName = "Сортировочный манипулятор", commandVerb = "захватить",
                description = "Замах и захват. Оглушает инженера в радиусе действия.",
                pos = new Vector2(-3.4f, 0f), radius = 2.0f, windup = 0.9f, stunDuration = 1.8f, cooldown = 8f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.MobileUnit, displayName = "Автопогрузчик F-02", commandVerb = "запустить маршрут",
                description = "Отправить погрузчик по западному коридору. Перекрывает путь и сбивает.",
                pos = new Vector2(-7.4f, -4.2f), size = new Vector2(1.1f, 1.5f),
                route = new[] { new Vector2(-7.4f, -4.2f), new Vector2(-7.4f, 4.2f) },
                moveSpeed = 3f, stunDuration = 1.8f, cooldown = 9f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.HazardEmitter, displayName = "Система мойки", commandVerb = "намочить пол",
                description = "Залить восточный проход водой. Мокрый пол замедляет инженера.",
                pos = new Vector2(3.2f, 4.4f), emitKind = HazardKind.Slippery,
                emitRect = R(1.8f, 0f, 5.4f, 1.7f), emitDuration = 11f, cooldown = 9f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Alarm, displayName = "Аварийная сигнализация", commandVerb = "включить",
                description = "Аварийный свет и сирены. Повышает стресс во всём цехе.",
                pos = new Vector2(-7.6f, 4.5f), alarmDuration = 6f, cooldown = 10f, escalationAuto = true,
            });

            l.hazards.Add(new HazardSpec { kind = HazardKind.Slippery, rect = R(2.4f, -3.2f, 2.4f, 1.2f), startsActive = true, escalationExpand = true });
            l.hazards.Add(new HazardSpec { kind = HazardKind.Slippery, rect = R(-6.4f, -2.4f, 2.0f, 1.2f), startsActive = true });

            l.objectives.Add(new ObjectiveSpec("Электрощит А", new Vector2(-7.4f, 3.6f), 30f));
            l.objectives.Add(new ObjectiveSpec("Электрощит Б", new Vector2(7.4f, -4.0f), 30f));
            l.objectives.Add(new ObjectiveSpec("Центральный пульт управления", new Vector2(0f, 3.4f), 40f));
            return l;
        }

        // ==================================================================
        // LEVEL 2 — УПАКОВОЧНАЯ ЛИНИЯ (fast arcade: presses, arms, scanner, mover)
        // ==================================================================
        static RoomLayout Level2()
        {
            var l = new RoomLayout
            {
                roomName = "УПАКОВОЧНАЯ ЛИНИЯ",
                goalText = "Изолировать автономную упаковочную линию",
                engineerSpawn = new Vector2(0f, -4.4f),
                exitPos = new Vector2(0f, 5f),
                floorTint = new Color(0.95f, 0.8f, 0.5f, 0.04f), // slightly warm production hall
            };

            // Pillars and mid-line walls forming three horizontal corridors.
            l.obstacles.Add(new RectSpec(new Vector2(-2.5f, 3.4f), new Vector2(1.0f, 1.0f), Steel, true));
            l.obstacles.Add(new RectSpec(new Vector2(2.5f, 3.4f), new Vector2(1.0f, 1.0f), Steel, true));
            l.obstacles.Add(new RectSpec(new Vector2(-4.9f, 0f), new Vector2(2.4f, 0.7f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(4.9f, 0f), new Vector2(2.4f, 0.7f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(-6.2f, -3.9f), new Vector2(1.2f, 0.9f), PalletWood, true));

            l.decor.Add(new RectSpec(new Vector2(-1.6f, 0f), new Vector2(2.1f, 2.1f), new Color(0.91f, 0.76f, 0.29f, 0.25f), false, 1));
            l.decor.Add(new RectSpec(new Vector2(1.6f, 0f), new Vector2(2.1f, 2.1f), new Color(0.91f, 0.76f, 0.29f, 0.25f), false, 1));
            l.decor.Add(new RectSpec(new Vector2(0f, 4.55f), new Vector2(2.4f, 0.09f), ExitGreen, false, 1));

            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Conveyor, displayName = "Скоростной конвейер 1", commandVerb = "сменить направление",
                description = "Скоростная северная лента. Разворот сметает груз и людей.",
                pos = new Vector2(0f, 2.2f), size = new Vector2(13f, 1.1f),
                conveyorDir = Vector2.right, conveyorSpeed = 3.2f, cooldown = 4.5f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Conveyor, displayName = "Скоростной конвейер 2", commandVerb = "сменить направление",
                description = "Скоростная южная лента. Разворот сметает груз и людей.",
                pos = new Vector2(0f, -2.2f), size = new Vector2(13f, 1.1f),
                conveyorDir = Vector2.left, conveyorSpeed = 3.2f, cooldown = 4.5f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.RoboticArm, displayName = "Пресс-модуль А", commandVerb = "запустить цикл",
                description = "Запустить цикл пресса. Зона удара хорошо видна и оглушает любого внутри.",
                pos = new Vector2(-1.6f, 0f), size = new Vector2(1.9f, 1.9f),
                pressMode = true, windup = 1.3f, stunDuration = 2.0f, cooldown = 8f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.RoboticArm, displayName = "Пресс-модуль Б", commandVerb = "запустить цикл",
                description = "Запустить цикл пресса. Зона удара хорошо видна и оглушает любого внутри.",
                pos = new Vector2(1.6f, 0f), size = new Vector2(1.9f, 1.9f),
                pressMode = true, windup = 1.3f, stunDuration = 2.0f, cooldown = 8f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.RoboticArm, displayName = "Укладочный манипулятор А", commandVerb = "перекрыть путь",
                description = "Развернуть западный манипулятор через северный переход.",
                pos = new Vector2(-5.6f, 2.2f), radius = 1.9f, windup = 0.8f, stunDuration = 1.6f, cooldown = 6.5f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.RoboticArm, displayName = "Укладочный манипулятор Б", commandVerb = "задержать инженера",
                description = "Развернуть восточный манипулятор через южный переход.",
                pos = new Vector2(5.6f, -2.2f), radius = 1.9f, windup = 0.8f, stunDuration = 1.6f, cooldown = 6.5f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Alarm, displayName = "Сканирующие ворота", commandVerb = "сканировать",
                description = "Просветить центральный проход. Помечает инженера и давит на нервы.",
                pos = new Vector2(0f, 0f), useGateRect = true, gateRect = R(0f, 0f, 1.4f, 3.4f),
                alarmDuration = 5f, cooldown = 8f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.MobileUnit, displayName = "Платформенный транспортёр", commandVerb = "запустить маршрут",
                description = "Прогнать транспортёр вдоль южного пролёта. Перекрывает путь и сбивает.",
                pos = new Vector2(-7.4f, -3.4f), size = new Vector2(1.3f, 1.0f),
                route = new[] { new Vector2(-7.4f, -3.4f), new Vector2(7.4f, -3.4f) },
                moveSpeed = 2.5f, stunDuration = 1.8f, cooldown = 9f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Door, displayName = "Аварийная дверь", commandVerb = "закрыть",
                description = "Перекрыть или открыть восточный служебный коридор.",
                pos = new Vector2(7.3f, 0f), size = new Vector2(2.4f, 0.7f), cooldown = 7f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Alarm, displayName = "Аварийная сигнализация", commandVerb = "включить",
                description = "Аварийный свет и сирены. Повышает стресс во всём цехе.",
                pos = new Vector2(-7.6f, 4.5f), alarmDuration = 6f, cooldown = 10f, escalationAuto = true,
            });

            l.objectives.Add(new ObjectiveSpec("Боковой пульт А", new Vector2(-7.4f, -2.6f), 26f));
            l.objectives.Add(new ObjectiveSpec("Боковой пульт Б", new Vector2(7.4f, 2.6f), 26f));
            l.objectives.Add(new ObjectiveSpec("Центральный терминал упаковки", new Vector2(0f, 3.6f), 36f));
            return l;
        }

        // ==================================================================
        // LEVEL 3 — ХОЛОДИЛЬНЫЙ СКЛАД (strategic: fog, ice, shifting pallet maze)
        // ==================================================================
        static RoomLayout Level3()
        {
            var l = new RoomLayout
            {
                roomName = "ХОЛОДИЛЬНЫЙ СКЛАД",
                goalText = "Открыть безопасный маршрут через холодильный склад",
                engineerSpawn = new Vector2(0f, -4.4f),
                exitPos = new Vector2(0f, 5f),
                floorTint = new Color(0.45f, 0.65f, 0.95f, 0.08f), // cold blue, kept readable
            };

            // Shelving aisles (serpentine corridors).
            l.obstacles.Add(new RectSpec(new Vector2(-5.2f, -0.5f), new Vector2(0.8f, 5.5f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(-2.0f, 0.8f), new Vector2(0.8f, 5.5f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(2.0f, -0.5f), new Vector2(0.8f, 5.5f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(5.2f, 0.8f), new Vector2(0.8f, 5.5f), DarkSteel, true));
            l.obstacles.Add(new RectSpec(new Vector2(-3.6f, -3.9f), new Vector2(1.1f, 1.0f), PalletWood, true));

            l.decor.Add(new RectSpec(new Vector2(0f, 4.55f), new Vector2(2.4f, 0.09f), ExitGreen, false, 1));

            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.HazardEmitter, displayName = "Холодильный вентилятор А", commandVerb = "выпустить холодный туман",
                description = "Заполнить западный проход морозным туманом. Замедляет и изматывает.",
                pos = new Vector2(-3.6f, 4.5f), emitKind = HazardKind.Cold,
                emitRect = R(-3.6f, 0.2f, 2.2f, 7f), emitDuration = 10f, cooldown = 8f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.HazardEmitter, displayName = "Холодильный вентилятор Б", commandVerb = "выпустить холодный туман",
                description = "Заполнить восточный проход морозным туманом. Замедляет и изматывает.",
                pos = new Vector2(3.6f, 4.5f), emitKind = HazardKind.Cold,
                emitRect = R(3.6f, 0.2f, 2.2f, 7f), emitDuration = 10f, cooldown = 8f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.MobileUnit, displayName = "Автономный штабелёр", commandVerb = "переставить палеты",
                description = "Переставить палеты: меняет план цеха и закрывает короткие пути.",
                pos = new Vector2(-0.8f, -2.4f), size = new Vector2(1.0f, 1.3f),
                route = new[] { new Vector2(-0.8f, -2.4f), new Vector2(-0.8f, 2.4f) },
                moveSpeed = 2.2f, stunDuration = 1.6f, cooldown = 10f, escalationAuto = true,
                palletShifts = new[]
                {
                    new PalletShiftSpec { posA = new Vector2(0.4f, -3.2f), posB = new Vector2(0.4f, 0.3f), size = new Vector2(1.5f, 1.0f) },
                    new PalletShiftSpec { posA = new Vector2(7.0f, -2.2f), posB = new Vector2(7.0f, 2.2f), size = new Vector2(2.4f, 1.0f) },
                },
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Drone, displayName = "Инвентарный дрон", commandVerb = "сканировать инженера",
                description = "Преследовать инженера. Помечает его и подтачивает решимость.",
                pos = new Vector2(7.6f, 4.6f), moveSpeed = 4.5f, alarmDuration = 6f, cooldown = 9f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Door, displayName = "Холодильная дверь А", commandVerb = "закрыть",
                description = "Перекрыть или открыть западный шлюзовой проход.",
                pos = new Vector2(-3.6f, 2.0f), size = new Vector2(2.4f, 0.7f), cooldown = 7f,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Door, displayName = "Холодильная дверь Б", commandVerb = "закрыть",
                description = "Перекрыть или открыть восточный шлюзовой проход.",
                pos = new Vector2(3.6f, -2.0f), size = new Vector2(2.4f, 0.7f), cooldown = 7f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.HazardEmitter, displayName = "Система обледенения", commandVerb = "обледенить пол",
                description = "Заморозить южный проход. Скользкий лёд замедляет инженера.",
                pos = new Vector2(7.6f, -4.4f), emitKind = HazardKind.Slippery,
                emitRect = R(0f, -3.9f, 10f, 1.2f), emitDuration = 11f, cooldown = 9f, escalationAuto = true,
            });
            l.machines.Add(new MachineSpec
            {
                kind = MachineKind.Alarm, displayName = "Аварийная сигнализация", commandVerb = "включить",
                description = "Аварийный свет и сирены. Повышает стресс во всём цехе.",
                pos = new Vector2(-7.6f, 4.6f), alarmDuration = 6f, cooldown = 10f, escalationAuto = true,
            });

            l.hazards.Add(new HazardSpec { kind = HazardKind.Cold, rect = R(-7.3f, 0f, 2.0f, 3.6f), startsActive = true, escalationExpand = true });

            l.objectives.Add(new ObjectiveSpec("Центральный узел распределения", new Vector2(0.4f, 1.6f), 26f));
            l.objectives.Add(new ObjectiveSpec("Шлюз морозильника А", new Vector2(-7.4f, 3.8f), 22f));
            l.objectives.Add(new ObjectiveSpec("Шлюз морозильника Б", new Vector2(7.4f, 3.8f), 22f));
            l.objectives.Add(new ObjectiveSpec("Финальный терминал безопасности", new Vector2(0f, 3.8f), 28f));
            return l;
        }
    }
}
