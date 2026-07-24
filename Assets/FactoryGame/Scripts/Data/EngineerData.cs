using UnityEngine;

namespace LastShift.Data
{
    /// <summary>Tuning for the stubborn engineer: movement, stress, and Resolve losses.</summary>
    [CreateAssetMenu(menuName = "Last Shift/Engineer Data", fileName = "EngineerData")]
    public class EngineerData : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 2.25f;
        public float panicMoveSpeed = 2.8f;
        public float retreatMoveSpeed = 3.0f;

        [Header("Stress")]
        public float stressMax = 100f;
        public float stressDecayPerSec = 3.5f;
        [Tooltip("Stress level at which Panic starts.")]
        public float panicEnterThreshold = 70f;
        [Tooltip("Stress level below which Panic ends.")]
        public float panicExitThreshold = 30f;

        [Header("Resolve")]
        public float resolveMax = 100f;
        [Tooltip("Global multiplier applied to every Resolve loss.")]
        public float resolveLossMultiplier = 1f;

        [Header("Resolve losses per event")]
        public float lossArmGrab = 7f;
        public float lossHazardEnter = 10f;
        public float lossNoPath = 7f;
        public float lossRouteBlocked = 6f;
        public float lossObjectiveDamaged = 12f;
        public float lossEscalation = 22f;
        public float lossAlarmPerSec = 1.1f;
        public float lossDronePerSec = 1.4f;

        [Header("Diminishing returns")]
        [Tooltip("Repeating the identical effect within this window is halved (anti-spam).")]
        public float repeatEffectWindow = 5f;

        [Header("Pathing")]
        [Tooltip("How strongly hazard danger repels path planning (0 = ignores danger).")]
        public float dangerWeight = 3f;
        [Tooltip("Local grid danger at which he breaks off to AvoidHazard (raise to make him ignore telegraphs, e.g. in the tutorial).")]
        public float avoidDangerThreshold = 1.5f;
        [Tooltip("Seconds without a valid path before losing Resolve (repeats).")]
        public float noPathGraceSeconds = 4f;

        [Header("Repair")]
        [Tooltip("Distance from an objective at which repairing works.")]
        public float repairRange = 1.15f;

        [Header("Stun")]
        public float defaultStunDuration = 1.6f;
    }
}
