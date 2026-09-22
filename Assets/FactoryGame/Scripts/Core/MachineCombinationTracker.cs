using UnityEngine;
using LastShift.Data;
using LastShift.Machines;

namespace LastShift.Core
{
    /// <summary>
    /// Setup → payoff machine combinations. A combination fires when one machine
    /// creates a situation (door block, conveyor carry, drone mark, hazard push)
    /// that another machine exploits within a forgiving time window. Rewards are
    /// small but satisfying: bonus Pressure/Resolve, a control-resource refund and
    /// a restrained chime. Also surfaces a short Russian hint when a setup is ready.
    /// </summary>
    public class MachineCombinationTracker : MonoBehaviour
    {
        /// <summary>Raised with the combo title and bonus amount (for toasts).</summary>
        public event System.Action<string, float> ComboTriggered;

        LevelManager lm;
        TacticsData tactics;

        // Setup timestamps (-999 = never).
        float doorClosedAt = -999f;
        float conveyorCarryAt = -999f;
        float hazardContactAt = -999f;

        // Per-combo cooldowns.
        float lastRedirectAt = -999f;
        float lastLineGrabAt = -999f;
        float lastMarkedAt = -999f;
        float lastDisplacementAt = -999f;

        float lastHintAt = -999f;

        bool hasConveyor, hasArm, hasDoor, hasMobile;

        public void Init(LevelManager levelManager, RoomRefs refs)
        {
            lm = levelManager;
            tactics = TacticsData.Get();

            foreach (var m in refs.machines)
            {
                if (m == null) continue;
                if (m is ConveyorMachine) hasConveyor = true;
                if (m is RoboticArmMachine) hasArm = true;
                if (m is DoorMachine) hasDoor = true;
                if (m is MobileUnitMachine) hasMobile = true;

                var machine = m;
                machine.Judged += OnJudged;
            }

            var e = lm.Engineer;
            e.ConveyorCarried += OnConveyorCarried;
            e.StunnedBy += OnStunned;
            e.HazardContact += OnHazardContact;
            e.MarkedChanged += marked => { if (marked) OnEngineerMarked(); };
        }

        bool InWindow(float t) => Time.time - t <= tactics.comboWindowSeconds;
        bool OffCooldown(float t) => Time.time - t >= tactics.comboCooldownSeconds;

        // ---------------- setups ----------------

        void OnJudged(InteractableMachine machine, bool effective)
        {
            if (!effective || lm == null || lm.RoomEnded) return;

            if (machine is DoorMachine)
            {
                doorClosedAt = Time.time;

                // COMBINATION D payoff: hazard pushed him away, the route got closed.
                if (InWindow(hazardContactAt) && OffCooldown(lastDisplacementAt))
                {
                    lastDisplacementAt = Time.time;
                    Reward(Loc.ComboDisplacement, tactics.comboDisplacementResolve, "combo displacement");
                }
                else
                {
                    Hint(hasConveyor, Loc.ComboHintConveyor);
                }
            }
            else if (machine is MobileUnitMachine && InWindow(hazardContactAt) && OffCooldown(lastDisplacementAt))
            {
                lastDisplacementAt = Time.time;
                Reward(Loc.ComboDisplacement, tactics.comboDisplacementResolve, "combo displacement");
            }
        }

        void OnConveyorCarried()
        {
            if (lm == null || lm.RoomEnded) return;
            conveyorCarryAt = Time.time;

            // COMBINATION A: door block → conveyor moved him.
            if (InWindow(doorClosedAt) && OffCooldown(lastRedirectAt))
            {
                lastRedirectAt = Time.time;
                Reward(Loc.ComboRedirect, tactics.comboRedirectResolve, "combo redirect");
            }
            else
            {
                Hint(hasArm, Loc.ComboHintArm);
            }
        }

        void OnStunned(string source)
        {
            if (lm == null || lm.RoomEnded) return;

            // COMBINATION B: conveyor carry → stun while he is on/near the line.
            if (InWindow(conveyorCarryAt) && OffCooldown(lastLineGrabAt))
            {
                lastLineGrabAt = Time.time;
                Reward(Loc.ComboLineGrab, tactics.comboLineGrabResolve, "combo line grab");
                return;
            }

            // COMBINATION C: drone mark → stun during the mark window.
            var e = lm.Engineer;
            if (e != null && e.IsMarked && OffCooldown(lastMarkedAt))
            {
                lastMarkedAt = Time.time;
                Reward(Loc.ComboMarkedTarget, tactics.comboMarkedResolve, "combo marked");
            }
        }

        void OnHazardContact(string zoneName)
        {
            if (lm == null || lm.RoomEnded) return;
            hazardContactAt = Time.time;

            // COMBINATION C variant: hazard hits a marked engineer.
            var e = lm.Engineer;
            if (e != null && e.IsMarked && OffCooldown(lastMarkedAt))
            {
                lastMarkedAt = Time.time;
                Reward(Loc.ComboMarkedTarget, tactics.comboMarkedResolve, "combo marked");
                return;
            }

            Hint(hasDoor || hasMobile, Loc.ComboHintCutRetreat);
        }

        /// <summary>Drone mark just landed: one-time synergy hint.</summary>
        public void OnEngineerMarked()
        {
            Hint(hasArm, Loc.ComboHintMarked);
        }

        // ---------------- rewards / hints ----------------

        void Reward(string title, float resolve, string reason)
        {
            if (resolve > 0f && lm.Engineer != null) lm.Engineer.Stats.LoseResolve(resolve, reason);
            FactoryControlResource.NotifyCombo();
            Audio.AudioManager.Play("repair_done", Audio.SfxBus.UI, 0.4f);
            ComboTriggered?.Invoke(title, resolve);
        }

        void Hint(bool relevant, string text)
        {
            // Sparse: only when the payoff machine exists and hints are not spamming.
            if (!relevant || Time.time - lastHintAt < 12f) return;
            // Never during the lesson: combinations are not taught there any more,
            // and the hint lands on top of the step's own card.
            if (lm != null && lm.TutorialMode) return;
            lastHintAt = Time.time;
            lm.ShowToast(text, 3.2f);
        }
    }
}
