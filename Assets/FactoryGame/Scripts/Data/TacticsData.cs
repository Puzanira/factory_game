using UnityEngine;

namespace LastShift.Data
{
    /// <summary>
    /// Tuning for the tactical layer: control resource, effective/wasted activation
    /// modifiers and combination rewards. An optional asset in Resources/TacticsData
    /// overrides the code defaults; otherwise a default instance is created at runtime
    /// so every value stays Inspector-tunable in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Shift/Tactics Data", fileName = "TacticsData")]
    public class TacticsData : ScriptableObject
    {
        [Header("Control resource")]
        [Tooltip("Maximum control charges per room.")]
        public int resourceMax = 3;
        [Tooltip("Seconds to passively regain one full charge.")]
        public float rechargeSecondsPerCharge = 11f;
        [Tooltip("Instant charge bonus for an effective activation.")]
        public float effectiveRechargeBonus = 0.4f;
        [Tooltip("Instant charge bonus for a completed combination.")]
        public float comboRechargeBonus = 1f;
        [Tooltip("Recharge speed multiplier for a few seconds after an effective action.")]
        public float effectiveBoostMultiplier = 2f;
        public float effectiveBoostSeconds = 5f;

        [Header("Activation judgment")]
        [Tooltip("Cooldown multiplier when the engineer was outside the effective zone.")]
        public float wastedCooldownMultiplier = 1.6f;
        [Tooltip("Cooldown multiplier for an effective activation.")]
        public float effectiveCooldownMultiplier = 0.85f;

        [Header("Combinations (rewards are Resolve damage)")]
        [Tooltip("Seconds after the setup during which the payoff still counts.")]
        public float comboWindowSeconds = 5f;
        [Tooltip("Per-combination cooldown so one pair can't be farmed.")]
        public float comboCooldownSeconds = 10f;
        public float comboRedirectResolve = 6f;
        public float comboLineGrabResolve = 8f;
        public float comboMarkedResolve = 5f;
        public float comboDisplacementResolve = 4f;

        [Header("Alarm as anti-repair")]
        [Tooltip("Alarm stress-per-second multiplier while the engineer is repairing («вой не даёт сосредоточиться») — the universal answer when no machine zone reaches the panel.")]
        public float alarmRepairStressMultiplier = 3f;

        [Header("Drone mark synergy")]
        [Tooltip("Resolve-loss multiplier for stuns/hazards while the engineer is marked.")]
        public float markedVulnerabilityMultiplier = 1.5f;
        [Tooltip("Extra stun seconds while marked.")]
        public float markedStunBonusSeconds = 0.5f;

        static TacticsData active;

        /// <summary>Resources asset when present, otherwise code defaults.</summary>
        public static TacticsData Get()
        {
            if (active == null)
            {
                active = Resources.Load<TacticsData>("TacticsData");
                if (active == null) active = CreateInstance<TacticsData>();
            }
            return active;
        }
    }
}
