using UnityEngine;
using LastShift.Data;

namespace LastShift.Engineer
{
    public enum EngineerStateId
    {
        EnterRoom, AssessSituation, MoveToObjective, RepairObjective,
        AvoidHazard, Repath, Stunned, Panic, RetreatToExit, Escape,
    }

    /// <summary>Base class for engineer FSM states.</summary>
    public abstract class EngineerState
    {
        protected readonly EngineerController engineer;
        protected EngineerState(EngineerController engineer) { this.engineer = engineer; }

        public abstract EngineerStateId Id { get; }
        public virtual string DisplayName => Id.ToString();

        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
    }

    // ---------------------------------------------------------------

    public class EnterRoomState : EngineerState
    {
        float timer;
        public EnterRoomState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.EnterRoom;
        public override string DisplayName => Loc.StateEntering;

        public override void Enter()
        {
            timer = 0f;
            engineer.Nav.SetDestination((Vector2)engineer.transform.position + Vector2.up * 0.9f, engineer.DangerWeight);
        }

        public override void Tick(float dt)
        {
            timer += dt;
            engineer.Nav.Tick(engineer.CurrentSpeed, dt);
            if (timer >= 1.0f) engineer.Fsm.ChangeState(EngineerStateId.AssessSituation);
        }
    }

    public class AssessSituationState : EngineerState
    {
        float timer;
        public AssessSituationState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.AssessSituation;
        public override string DisplayName => Loc.StateAssessing;

        public override void Enter() { timer = 0f; engineer.Nav.ClearPath(); }

        public override void Tick(float dt)
        {
            timer += dt;
            if (timer < 1.4f) return;
            engineer.Fsm.ChangeState(engineer.CurrentObjective != null
                ? EngineerStateId.MoveToObjective
                : EngineerStateId.RetreatToExit);
        }
    }

    public class MoveToObjectiveState : EngineerState
    {
        public MoveToObjectiveState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.MoveToObjective;
        public override string DisplayName => Loc.StateMoving;

        public override void Enter()
        {
            if (engineer.CurrentObjective == null)
            {
                engineer.Fsm.ChangeState(EngineerStateId.AssessSituation);
                return;
            }
            bool found = engineer.Nav.SetDestination(engineer.CurrentObjective.Pos, engineer.DangerWeight);
            if (!found) engineer.Fsm.ChangeState(EngineerStateId.Repath);
        }

        public override void Tick(float dt)
        {
            if (engineer.CurrentObjective == null)
            {
                engineer.Fsm.ChangeState(EngineerStateId.AssessSituation);
                return;
            }
            if (engineer.LocalDangerHigh)
            {
                engineer.Fsm.ChangeState(EngineerStateId.AvoidHazard);
                return;
            }
            bool arrived = engineer.Nav.Tick(engineer.CurrentSpeed, dt);
            if (arrived || engineer.WithinRepairRange)
                engineer.Fsm.ChangeState(EngineerStateId.RepairObjective);
        }
    }

    public class RepairObjectiveState : EngineerState
    {
        public RepairObjectiveState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.RepairObjective;
        public override string DisplayName => Loc.StateRepairing;

        public override void Enter() => engineer.Nav.ClearPath();

        public override void Tick(float dt)
        {
            var obj = engineer.CurrentObjective;
            if (obj == null)
            {
                engineer.Fsm.ChangeState(EngineerStateId.AssessSituation);
                return;
            }
            if (engineer.LocalDangerHigh)
            {
                engineer.NotifyRepairInterrupted();
                engineer.Fsm.ChangeState(EngineerStateId.AvoidHazard);
                return;
            }
            if (!engineer.WithinRepairRange)
            {
                engineer.Fsm.ChangeState(EngineerStateId.MoveToObjective);
                return;
            }
            obj.TickRepair(dt);
        }
    }

    public class AvoidHazardState : EngineerState
    {
        float timer;
        public AvoidHazardState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.AvoidHazard;
        public override string DisplayName => Loc.StateAvoiding;

        public override void Enter()
        {
            timer = 0f;
            var grid = Utilities.PathGrid.Instance;
            if (grid != null)
            {
                Vector2 safe = grid.NearestSafe(engineer.transform.position, 0.4f);
                engineer.Nav.SetDestination(safe, 0f); // getting out matters more than comfort
            }
        }

        public override void Tick(float dt)
        {
            timer += dt;
            bool arrived = engineer.Nav.Tick(engineer.CurrentSpeed, dt);
            if ((arrived && !engineer.LocalDangerHigh) || timer > 5f)
                engineer.Fsm.ChangeState(EngineerStateId.Repath);
        }
    }

    /// <summary>Recomputes a route; shows "Blocked" while no route exists.</summary>
    public class RepathState : EngineerState
    {
        float retryTimer;
        float blockedTime;
        float nextLossAt;
        float reassessDelay; // he stops and thinks after a route failure (easier for the player)
        bool blocked;

        public RepathState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.Repath;
        public override string DisplayName => blocked ? Loc.StateBlocked : Loc.StateRepathing;

        public override void Enter()
        {
            retryTimer = 1.2f; // first attempt right after the reassess pause
            blockedTime = 0f;
            reassessDelay = 1.0f;
            blocked = false;
            nextLossAt = engineer.Data != null ? engineer.Data.noPathGraceSeconds : 4f;
            engineer.Nav.ClearPath();
        }

        void TryRoute()
        {
            Vector2 target = engineer.CurrentObjective != null
                ? engineer.CurrentObjective.Pos
                : engineer.ExitPos;
            if (engineer.Nav.SetDestination(target, engineer.DangerWeight))
            {
                engineer.Fsm.ChangeState(engineer.CurrentObjective != null
                    ? EngineerStateId.MoveToObjective
                    : EngineerStateId.RetreatToExit);
            }
            else
            {
                blocked = true;
                engineer.Fsm.NotifyLabelChanged();
            }
        }

        public override void Tick(float dt)
        {
            if (reassessDelay > 0f) { reassessDelay -= dt; return; }
            retryTimer += dt;
            blockedTime += dt;
            if (blockedTime >= nextLossAt)
            {
                nextLossAt += engineer.Data != null ? engineer.Data.noPathGraceSeconds : 4f;
                engineer.NotifyNoPath();
            }
            if (retryTimer >= 1.2f)
            {
                retryTimer = 0f;
                TryRoute();
            }
        }
    }

    public class StunnedState : EngineerState
    {
        float timer;
        public StunnedState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.Stunned;
        public override string DisplayName => Loc.StateStunned;

        public override void Enter()
        {
            timer = engineer.PendingStunDuration;
            engineer.Nav.ClearPath();
        }

        public override void Tick(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;
            engineer.Fsm.ChangeState(engineer.Stats.IsResolveEmpty
                ? EngineerStateId.RetreatToExit
                : EngineerStateId.Repath);
        }
    }

    /// <summary>Faster but sloppier: recomputes noisy paths and ignores danger costs.</summary>
    public class PanicState : EngineerState
    {
        float replanTimer;
        public PanicState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.Panic;
        public override string DisplayName => Loc.StatePanicking;

        public override void Enter()
        {
            replanTimer = 0f;
            Replan();
        }

        void Replan()
        {
            Vector2 target = engineer.CurrentObjective != null ? engineer.CurrentObjective.Pos : engineer.ExitPos;
            var grid = Utilities.PathGrid.Instance;
            if (grid != null)
            {
                // Panic makes him inaccurate: aim near the goal, not exactly at it.
                Vector2 jitter = Random.insideUnitCircle * 1.4f;
                Vector2 t = grid.NearestFree(target + jitter);
                engineer.Nav.SetDestination(t, 0f);
            }
        }

        public override void Tick(float dt)
        {
            replanTimer += dt;
            if (replanTimer > 2f) { replanTimer = 0f; Replan(); }
            engineer.Nav.Tick(engineer.CurrentSpeed, dt);

            if (engineer.Stats.Stress <= engineer.PanicExitThreshold)
                engineer.Fsm.ChangeState(EngineerStateId.Repath);
        }
    }

    public class RetreatToExitState : EngineerState
    {
        float replanTimer;
        public RetreatToExitState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.RetreatToExit;
        public override string DisplayName => Loc.StateRetreating;

        public override void Enter()
        {
            engineer.AbandonObjective();
            engineer.Nav.SetDestination(engineer.ExitPos, 0.5f);
        }

        public override void Tick(float dt)
        {
            replanTimer += dt;
            if (!engineer.Nav.HasPath && replanTimer > 0.6f)
            {
                replanTimer = 0f;
                engineer.Nav.SetDestination(engineer.ExitPos, 0.5f);
            }
            bool arrived = engineer.Nav.Tick(engineer.CurrentSpeed, dt);
            if (arrived && Vector2.Distance(engineer.transform.position, engineer.ExitPos) < 0.8f)
                engineer.Fsm.ChangeState(EngineerStateId.Escape);
        }
    }

    public class EscapeState : EngineerState
    {
        public EscapeState(EngineerController e) : base(e) { }
        public override EngineerStateId Id => EngineerStateId.Escape;
        public override string DisplayName => Loc.StateEscaping;

        public override void Enter() => engineer.NotifyEscaped();
    }
}
