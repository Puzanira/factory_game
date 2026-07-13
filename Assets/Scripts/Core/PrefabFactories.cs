using UnityEngine;
using LastShift.Data;
using LastShift.Engineer;
using LastShift.Hazards;
using LastShift.Machines;
using LastShift.Objectives;
using LastShift.UI;

namespace LastShift.Core
{
    /// <summary>
    /// Creates the canonical GameObject for each prefab type. Used both by the editor
    /// ProjectBuilder (to save real prefab assets) and as a runtime fallback when a
    /// PrefabLibrary entry is missing. Visuals are attached at runtime by the
    /// components themselves, so these objects are safe to serialize as prefabs.
    /// </summary>
    public static class PrefabFactories
    {
        public static GameObject CreateEngineer()
        {
            var go = new GameObject("Engineer");
            go.AddComponent<EngineerStats>();
            go.AddComponent<EngineerNavigation>();
            go.AddComponent<EngineerController>();
            go.AddComponent<EngineerStateUI>();
            return go;
        }

        public static GameObject CreateMachine(MachineKind kind)
        {
            var go = new GameObject(kind + "Machine");
            switch (kind)
            {
                case MachineKind.Door: go.AddComponent<DoorMachine>(); break;
                case MachineKind.Conveyor: go.AddComponent<ConveyorMachine>(); break;
                case MachineKind.RoboticArm: go.AddComponent<RoboticArmMachine>(); break;
                case MachineKind.MobileUnit: go.AddComponent<MobileUnitMachine>(); break;
                case MachineKind.Drone: go.AddComponent<DroneMachine>(); break;
                case MachineKind.Alarm: go.AddComponent<AlarmMachine>(); break;
                default: go.AddComponent<HazardEmitterMachine>(); break;
            }
            return go;
        }

        public static GameObject CreateHazard(HazardKind kind)
        {
            var go = new GameObject(kind + "Hazard");
            switch (kind)
            {
                case HazardKind.Steam: go.AddComponent<SteamHazard>(); break;
                case HazardKind.Cold: go.AddComponent<ColdHazard>(); break;
                case HazardKind.Slippery: go.AddComponent<SlipperyFloor>(); break;
                default: go.AddComponent<HazardZone>(); break;
            }
            return go;
        }

        public static GameObject CreateRepairObjective()
        {
            var go = new GameObject("RepairObjective");
            go.AddComponent<RepairObjective>();
            return go;
        }

        public static GameObject CreateExitDoor()
        {
            var go = new GameObject("ExitDoor");
            go.AddComponent<ExitDoor>();
            return go;
        }

        public static GameObject CreateUIRoot()
        {
            var go = new GameObject("UIRoot");
            go.AddComponent<FactoryCommandTerminal>();
            go.AddComponent<CommandTerminalUI>();
            go.AddComponent<HUDController>();
            go.AddComponent<PauseMenuUI>();
            go.AddComponent<EndRoomPanel>();
            return go;
        }
    }
}
