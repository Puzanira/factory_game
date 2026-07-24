using UnityEngine;

namespace LastShift.Core
{
    /// <summary>
    /// Room Pressure 0..100. Rises only from meaningful factory effects (stuns,
    /// blocked routes, hazard contact...), never from merely pressing Enter.
    /// Different machine effects in quick succession earn a small combo bonus;
    /// crossing 75 fires a warning; at max it fires once and the room escalates.
    /// </summary>
    public class RoomPressureController : MonoBehaviour
    {
        public float Value { get; private set; }
        public float Max { get; private set; } = 100f;

        public event System.Action<float> Changed;
        public event System.Action MaxReached;
        /// <summary>Two different effects hit the engineer in sequence (bonus amount).</summary>
        public event System.Action<float> ComboBonus;
        /// <summary>Crossed the 75% warning threshold.</summary>
        public event System.Action WarningReached;

        const float ComboWindow = 6f;
        const float ComboCooldown = 8f;
        const float ComboAmount = 5f;

        bool maxed;
        bool warned;
        string lastReason;
        float lastReasonTime = -99f;
        float lastComboTime = -99f;

        public void Init(float max)
        {
            Max = Mathf.Max(1f, max);
            Value = 0f;
        }

        public void Add(float amount, string reason)
        {
            if (amount <= 0f) return;

            // Combo: a different effect source within the window → small bonus.
            if (!string.IsNullOrEmpty(reason) && !string.IsNullOrEmpty(lastReason) &&
                reason != lastReason &&
                Time.time - lastReasonTime < ComboWindow &&
                Time.time - lastComboTime > ComboCooldown)
            {
                lastComboTime = Time.time;
                amount += ComboAmount;
                ComboBonus?.Invoke(ComboAmount);
            }
            lastReason = reason;
            lastReasonTime = Time.time;

            Apply(amount);
        }

        /// <summary>Slow passive gain (engineer delayed/trapped/panicking); no combo tracking.</summary>
        public void AddPassive(float amount)
        {
            if (amount <= 0f) return;
            Apply(amount);
        }

        void Apply(float amount)
        {
            Value = Mathf.Min(Max, Value + amount);
            Changed?.Invoke(Value);
            if (!warned && Value >= Max * 0.75f)
            {
                warned = true;
                WarningReached?.Invoke();
            }
            if (Value >= Max && !maxed)
            {
                maxed = true;
                MaxReached?.Invoke();
            }
        }
    }
}
