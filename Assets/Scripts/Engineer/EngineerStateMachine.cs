using System.Collections.Generic;

namespace LastShift.Engineer
{
    /// <summary>Minimal FSM: owns state instances, handles transitions, raises label events.</summary>
    public class EngineerStateMachine
    {
        readonly Dictionary<EngineerStateId, EngineerState> states = new Dictionary<EngineerStateId, EngineerState>();

        public EngineerState Current { get; private set; }
        public EngineerStateId CurrentId => Current?.Id ?? EngineerStateId.EnterRoom;
        public string CurrentLabel => Current?.DisplayName ?? "";

        public event System.Action<EngineerStateId, string> StateChanged;

        public EngineerStateMachine(EngineerController engineer)
        {
            Register(new EnterRoomState(engineer));
            Register(new AssessSituationState(engineer));
            Register(new MoveToObjectiveState(engineer));
            Register(new RepairObjectiveState(engineer));
            Register(new AvoidHazardState(engineer));
            Register(new RepathState(engineer));
            Register(new StunnedState(engineer));
            Register(new PanicState(engineer));
            Register(new RetreatToExitState(engineer));
            Register(new EscapeState(engineer));
        }

        void Register(EngineerState state) => states[state.Id] = state;

        public void Start(EngineerStateId id)
        {
            Current = states[id];
            Current.Enter();
            StateChanged?.Invoke(Current.Id, Current.DisplayName);
        }

        public void ChangeState(EngineerStateId id)
        {
            if (Current != null && Current.Id == id) return;
            Current?.Exit();
            Current = states[id];
            Current.Enter();
            StateChanged?.Invoke(Current.Id, Current.DisplayName);
        }

        public void Tick(float dt) => Current?.Tick(dt);

        /// <summary>For states whose display label changes without a state change (e.g. Blocked).</summary>
        public void NotifyLabelChanged() => StateChanged?.Invoke(CurrentId, CurrentLabel);
    }
}
