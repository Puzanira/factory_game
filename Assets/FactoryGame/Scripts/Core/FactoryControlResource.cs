using UnityEngine;
using LastShift.Data;

namespace LastShift.Core
{
    /// <summary>
    /// «РЕСУРС УПРАВЛЕНИЯ» — the room's limited pool of control charges.
    /// Player activations spend charges; the pool recharges slowly on its own,
    /// noticeably faster right after effective actions, and combinations refund
    /// part of a charge. Recovery never stops, so the player can always come back.
    /// Escalation auto-fire never spends resource (it is not a player action).
    /// </summary>
    public class FactoryControlResource : MonoBehaviour
    {
        public static FactoryControlResource Instance { get; private set; }

        public float Charges { get; private set; }
        public int Max { get; private set; } = 3;

        /// <summary>Raised when the pool value changes (UI refresh).</summary>
        public event System.Action Changed;
        /// <summary>Raised when a spend was refused — UI flashes the cells.</summary>
        public event System.Action InsufficientFlash;

        TacticsData tactics;
        float boostTimer;
        float lastInsufficientToast = -99f;

        void Awake()
        {
            Instance = this;
            tactics = TacticsData.Get();
            Max = Mathf.Max(1, tactics.resourceMax);
            Charges = Max;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Charges >= Max) { boostTimer = Mathf.Max(0f, boostTimer - Time.deltaTime); return; }
            float rate = 1f / Mathf.Max(1f, tactics.rechargeSecondsPerCharge);
            if (boostTimer > 0f)
            {
                boostTimer -= Time.deltaTime;
                rate *= Mathf.Max(1f, tactics.effectiveBoostMultiplier);
            }
            float before = Charges;
            Charges = Mathf.Min(Max, Charges + rate * Time.deltaTime);
            if (Mathf.FloorToInt(before) != Mathf.FloorToInt(Charges))
                Audio.UiSfx.CommandReady();
            Changed?.Invoke();
        }

        public bool CanSpend(int cost) => cost <= 0 || Charges >= cost;

        public void Spend(int cost)
        {
            if (cost <= 0) return;
            Charges = Mathf.Max(0f, Charges - cost);
            Changed?.Invoke();
        }

        /// <summary>Spend refused: soft feedback, rate-limited toast.</summary>
        public void NotifyInsufficient()
        {
            InsufficientFlash?.Invoke();
            if (Time.unscaledTime - lastInsufficientToast > 2.5f)
            {
                lastInsufficientToast = Time.unscaledTime;
                var lm = LevelManager.Instance;
                if (lm != null) lm.ShowToast(Loc.NotEnoughResource, 2.4f, warning: true);
            }
        }

        /// <summary>Effective activation: small instant refund + temporary recharge boost.</summary>
        public static void NotifyEffectiveAction()
        {
            var r = Instance;
            if (r == null) return;
            r.Charges = Mathf.Min(r.Max, r.Charges + r.tactics.effectiveRechargeBonus);
            r.boostTimer = r.tactics.effectiveBoostSeconds;
            r.Changed?.Invoke();
        }

        /// <summary>Completed combination: bigger refund.</summary>
        public static void NotifyCombo()
        {
            var r = Instance;
            if (r == null) return;
            r.Charges = Mathf.Min(r.Max, r.Charges + r.tactics.comboRechargeBonus);
            r.boostTimer = r.tactics.effectiveBoostSeconds;
            r.Changed?.Invoke();
        }
    }
}
