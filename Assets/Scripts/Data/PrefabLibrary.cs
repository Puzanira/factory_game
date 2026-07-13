using UnityEngine;

namespace LastShift.Data
{
    /// <summary>
    /// Central prefab registry assigned to each LevelManager. If any entry is missing
    /// at runtime the RoomBuilder falls back to code factories, so the game never breaks
    /// on an unassigned reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Shift/Prefab Library", fileName = "PrefabLibrary")]
    public class PrefabLibrary : ScriptableObject
    {
        [Header("Engineer")]
        public GameObject engineer;

        [Header("Machines")]
        public GameObject doorMachine;
        public GameObject conveyorMachine;
        public GameObject roboticArmMachine;
        public GameObject mobileUnitMachine;
        public GameObject droneMachine;
        public GameObject alarmMachine;
        public GameObject hazardEmitterMachine;

        [Header("Hazards")]
        public GameObject steamHazard;
        public GameObject coldHazard;
        public GameObject slipperyFloor;
        public GameObject dangerZone;

        [Header("Objectives")]
        public GameObject repairObjective;
        public GameObject exitDoor;

        [Header("UI")]
        public GameObject uiRoot;

        public GameObject MachinePrefab(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Door: return doorMachine;
                case MachineKind.Conveyor: return conveyorMachine;
                case MachineKind.RoboticArm: return roboticArmMachine;
                case MachineKind.MobileUnit: return mobileUnitMachine;
                case MachineKind.Drone: return droneMachine;
                case MachineKind.Alarm: return alarmMachine;
                default: return hazardEmitterMachine;
            }
        }

        public GameObject HazardPrefab(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Steam: return steamHazard;
                case HazardKind.Cold: return coldHazard;
                case HazardKind.Slippery: return slipperyFloor;
                default: return dangerZone;
            }
        }
    }
}
