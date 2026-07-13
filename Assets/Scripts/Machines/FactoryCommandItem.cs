namespace LastShift.Machines
{
    /// <summary>One entry in the factory command terminal, binding a machine to a list slot.</summary>
    public class FactoryCommandItem
    {
        public readonly InteractableMachine machine;

        public FactoryCommandItem(InteractableMachine machine)
        {
            this.machine = machine;
        }

        public bool IsReady => machine != null && machine.IsReady;
        public string Label => machine != null ? machine.CommandLabel : "—";
        public string Description => machine != null ? machine.Description : "";
        public float CooldownFraction =>
            machine == null || machine.CooldownDuration <= 0f
                ? 0f
                : machine.CooldownRemaining / machine.CooldownDuration;
    }
}
