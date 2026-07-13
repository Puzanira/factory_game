using UnityEngine;

namespace LastShift.Data
{
    /// <summary>
    /// Default configuration for a machine type. MachineSpec values in a layout
    /// override these when provided (spec cooldown <= 0 falls back to this asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Last Shift/Machine Data", fileName = "MachineData")]
    public class MachineData : ScriptableObject
    {
        public MachineKind kind;
        public string displayName = "Machine";
        public string commandVerb = "Activate";
        [TextArea] public string description = "";
        public float cooldown = 8f;
        [Tooltip("Pressure added when this machine meaningfully affects the engineer.")]
        public float pressureOnEffect = 6f;
        public Color bodyColor = Color.gray;
    }
}
