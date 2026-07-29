using LastShift.Input;

namespace LastShift.Utilities
{
    /// <summary>
    /// The single logical-input surface every screen polls. The physical source
    /// is the shared arcade-controls layer (ArcadeInput), bridged into the
    /// frame-stamped <see cref="UnifiedGameInput"/> flags by
    /// <see cref="ArcadeInputBridge"/> before any consumer runs this frame:
    ///   joystick Up/Left    -> UpPressed      (NavigatePrevious)
    ///   joystick Down/Right -> DownPressed     (NavigateNext)
    ///   Red button          -> ConfirmPressed  (Submit)
    /// The Menu button is deliberately never read here — it is reserved for the
    /// hub launcher return. There is no pause/back action in the arcade contract.
    /// </summary>
    public static class GameInput
    {
        public static bool UpPressed => UnifiedGameInput.NavigatePrevious;
        public static bool DownPressed => UnifiedGameInput.NavigateNext;
        public static bool ConfirmPressed => UnifiedGameInput.Submit;
    }
}
