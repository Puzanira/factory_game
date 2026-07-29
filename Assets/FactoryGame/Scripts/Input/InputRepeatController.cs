namespace LastShift.Input
{
    /// <summary>
    /// One shared repeat state for joystick navigation (never one per axis):
    /// entering a direction fires immediately, holding waits the initial delay,
    /// then repeats at a fixed interval; returning to neutral resets everything.
    /// Runs on unscaled time so held navigation feels consistent everywhere.
    /// </summary>
    public sealed class InputRepeatController
    {
        readonly float initialDelay;
        readonly float interval;

        int heldAction;      // -1 previous, +1 next, 0 neutral
        float nextRepeatAt;

        public InputRepeatController(float initialDelay, float interval)
        {
            this.initialDelay = initialDelay;
            this.interval = interval;
        }

        /// <summary>
        /// Feed the desired action for this frame (-1/0/+1); returns the action
        /// to emit this frame, 0 for none.
        /// </summary>
        public int Step(int desiredAction, float now)
        {
            if (desiredAction == 0)
            {
                heldAction = 0;
                return 0;
            }

            if (desiredAction != heldAction)
            {
                // New direction (from neutral or a sweep): one immediate action.
                heldAction = desiredAction;
                nextRepeatAt = now + initialDelay;
                return desiredAction;
            }

            if (now >= nextRepeatAt)
            {
                nextRepeatAt = now + interval;
                return desiredAction;
            }

            return 0;
        }
    }
}
