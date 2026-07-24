using UnityEngine;
using LastShift.Data;

namespace LastShift.Engineer
{
    /// <summary>Resolve / Stress / Safety bookkeeping for the engineer.</summary>
    public class EngineerStats : MonoBehaviour
    {
        public EngineerData data;

        public float Resolve { get; private set; } = 100f;
        public float Stress { get; private set; }
        /// <summary>Current local danger sampled from the path grid (for the HUD).</summary>
        public float Safety { get; set; }

        public event System.Action<float> ResolveChanged;
        public event System.Action<float> StressChanged;
        public event System.Action ResolveEmpty;
        /// <summary>A discrete Resolve hit landed (final amount after multipliers).</summary>
        public event System.Action<float, string> ResolveLost;

        public bool IsResolveEmpty => Resolve <= 0f;

        public void Init(EngineerData engineerData)
        {
            data = engineerData;
            Resolve = data != null ? data.resolveMax : 100f;
            Stress = 0f;
            ResolveChanged?.Invoke(Resolve);
            StressChanged?.Invoke(Stress);
        }

        void Update()
        {
            if (data == null) return;
            if (Stress > 0f)
            {
                Stress = Mathf.Max(0f, Stress - data.stressDecayPerSec * Time.deltaTime);
                StressChanged?.Invoke(Stress);
            }
        }

        string lastLossReason;
        float lastLossTime = -99f;

        public void LoseResolve(float amount, string reason)
        {
            if (amount <= 0f || IsResolveEmpty) return;
            float mult = data != null ? data.resolveLossMultiplier : 1f;

            // Diminishing returns: spamming the identical discrete effect in a short
            // window only counts half, so variety beats repetition (but nothing is
            // wasted). Continuous per-frame drains (alarm/drone) are exempt.
            if (amount >= 2f)
            {
                float window = data != null ? data.repeatEffectWindow : 5f;
                if (reason == lastLossReason && Time.time - lastLossTime < window)
                    mult *= 0.5f;
                lastLossReason = reason;
                lastLossTime = Time.time;
            }

            Resolve = Mathf.Max(0f, Resolve - amount * mult);
            ResolveChanged?.Invoke(Resolve);
            if (amount * mult >= 2f) ResolveLost?.Invoke(amount * mult, reason);
            if (Resolve <= 0f) ResolveEmpty?.Invoke();
        }

        public void AddStress(float amount)
        {
            if (amount <= 0f || data == null) return;
            Stress = Mathf.Min(data.stressMax, Stress + amount);
            StressChanged?.Invoke(Stress);
        }

        /// <summary>Tutorial helper: drop accumulated stress so panic can't derail a lesson step.</summary>
        public void CalmStress()
        {
            Stress = 0f;
            StressChanged?.Invoke(Stress);
        }
    }
}
