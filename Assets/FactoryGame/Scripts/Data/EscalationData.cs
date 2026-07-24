using UnityEngine;

namespace LastShift.Data
{
    /// <summary>Tuning for the room emergency escalation triggered at 100 Pressure.</summary>
    [CreateAssetMenu(menuName = "Last Shift/Escalation Data", fileName = "EscalationData")]
    public class EscalationData : ScriptableObject
    {
        [Tooltip("Large one-time Resolve penalty when escalation starts.")]
        public float resolvePenalty = 22f;
        [Tooltip("Seconds between automatic activations of escalation machines.")]
        public float autoActivateInterval = 5f;
        [Tooltip("Scale applied to hazards flagged escalationExpand.")]
        public float hazardExpandScale = 1.5f;
        [Tooltip("Fraction of current objective progress destroyed when escalation starts.")]
        [Range(0f, 1f)] public float objectiveDamageFraction = 0.35f;
        public Color warningColor = new Color(1f, 0.15f, 0.1f, 0.16f);
    }
}
