using System.Collections.Generic;
using UnityEngine;

namespace LastShift.Data
{
    public enum MachineKind { Door, Conveyor, RoboticArm, MobileUnit, Drone, Alarm, HazardEmitter }
    public enum HazardKind { Steam, Cold, Slippery, Danger }

    /// <summary>A static rectangle in the room: wall/obstacle (blocksPath) or pure decor.</summary>
    [System.Serializable]
    public class RectSpec
    {
        public Vector2 pos;
        public Vector2 size = Vector2.one;
        public Color color = Color.gray;
        public int sortingOrder = 1;
        public bool blocksPath;

        public RectSpec() { }
        public RectSpec(Vector2 pos, Vector2 size, Color color, bool blocksPath, int sortingOrder = 1)
        {
            this.pos = pos; this.size = size; this.color = color;
            this.blocksPath = blocksPath; this.sortingOrder = sortingOrder;
        }
    }

    /// <summary>Pallet position pair toggled by a stacker-style mobile unit.</summary>
    [System.Serializable]
    public class PalletShiftSpec
    {
        public Vector2 posA;
        public Vector2 posB;
        public Vector2 size = new Vector2(1.6f, 1f);
    }

    /// <summary>Everything needed to place and configure one machine in a room.</summary>
    [System.Serializable]
    public class MachineSpec
    {
        public MachineKind kind;
        public string displayName = "Machine";
        public string commandVerb = "Activate";
        [TextArea] public string description = "";
        public Vector2 pos;
        public Vector2 size = Vector2.one;
        public float cooldown = 8f;
        [Tooltip("Auto-activated repeatedly while the room is escalated.")]
        public bool escalationAuto;

        // Door
        public bool startsClosed;

        // Conveyor
        public Vector2 conveyorDir = Vector2.right;
        public float conveyorSpeed = 2.2f;

        // Robotic arm / press
        public float radius = 2f;
        public bool pressMode;
        public float windup = 0.9f;
        public float stunDuration = 1.6f;

        // Mobile unit
        public Vector2[] route;
        public float moveSpeed = 3f;
        public PalletShiftSpec[] palletShifts;

        // Alarm / scanner gate
        public bool useGateRect;
        public Rect gateRect;
        public float alarmDuration = 6f;

        // Hazard emitter
        public HazardKind emitKind = HazardKind.Slippery;
        public Rect emitRect;
        public float emitDuration = 9f;
    }

    [System.Serializable]
    public class HazardSpec
    {
        public HazardKind kind;
        public Rect rect;
        public bool startsActive = true;
        [Tooltip("Grows when the room escalates.")]
        public bool escalationExpand;
    }

    [System.Serializable]
    public class ObjectiveSpec
    {
        public string name = "Objective";
        public Vector2 pos;
        public float repairTime = 20f;

        public ObjectiveSpec() { }
        public ObjectiveSpec(string name, Vector2 pos, float repairTime)
        {
            this.name = name; this.pos = pos; this.repairTime = repairTime;
        }
    }

    /// <summary>Full description of one room. Rooms are centred on the world origin.</summary>
    public class RoomLayout
    {
        public string roomName = "Room";
        public string goalText = "";
        public Vector2 roomSize = new Vector2(17f, 10.5f);
        public Vector2 engineerSpawn = new Vector2(0f, -4.4f);
        public Vector2 exitPos = new Vector2(0f, 5f);
        public Vector2 exitSize = new Vector2(2.4f, 0.9f);
        public Color floorTint = Color.clear; // optional full-room tint overlay (e.g. cold blue)

        public readonly List<RectSpec> obstacles = new List<RectSpec>();
        public readonly List<RectSpec> decor = new List<RectSpec>();
        public readonly List<MachineSpec> machines = new List<MachineSpec>();
        public readonly List<HazardSpec> hazards = new List<HazardSpec>();
        public readonly List<ObjectiveSpec> objectives = new List<ObjectiveSpec>();
    }
}
