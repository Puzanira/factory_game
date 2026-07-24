using System.Globalization;
using UnityEngine;

namespace LastShift.Input
{
    public enum StickDirection { Neutral, Up, Down, Left, Right }

    /// <summary>
    /// Turns the sketch's CSV lines into physical controller state.
    /// Protocol (my_ardruino_sketch/sketch_game_jul15a.ino, 115200 baud):
    ///   JOY,x,y      — joystick axes, raw 0..1023, sent on change (~33 Hz max)
    ///   JOY,DOWN     — joystick push button pressed (debounced on the board)
    ///   JOY,UP,ms    — joystick push button released
    ///   BTN,DOWN     — external button pressed (debounced on the board)
    ///   BTN,UP,ms    — external button released
    ///   BTN,HELD,ms  — external button held (ignored: holding must not repeat Submit)
    /// Obsolete POT,* lines from an outdated sketch are ignored with a single
    /// logged warning. Malformed lines are counted and dropped, never thrown.
    /// </summary>
    public sealed class ArduinoInputParser
    {
        readonly SerialConnectionSettings settings;

        // Latest raw values.
        public int JoyX { get; private set; }
        public int JoyY { get; private set; }
        public bool JoystickButtonHeld { get; private set; }
        public bool ExternalButtonHeld { get; private set; }
        public string LastValidLine { get; private set; } = "";
        public int InvalidLineCount { get; private set; }
        public int ObsoleteLineCount { get; private set; }

        // Derived state.
        public StickDirection Stick { get; private set; } = StickDirection.Neutral;

        int pendingButtonPresses;   // joystick button + external button, DOWN edges only
        bool warnedObsoletePot;

        public ArduinoInputParser(SerialConnectionSettings settings)
        {
            this.settings = settings;
            JoyX = settings.joystickCenter;
            JoyY = settings.joystickCenter;
        }

        /// <summary>Back to a safe idle state (called when the port disconnects).</summary>
        public void ResetToNeutral()
        {
            JoyX = settings.joystickCenter;
            JoyY = settings.joystickCenter;
            Stick = StickDirection.Neutral;
            JoystickButtonHeld = false;
            ExternalButtonHeld = false;
            pendingButtonPresses = 0;
        }

        public void HandleLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            string[] fields = line.Split(',');
            string tag = fields[0].Trim().ToUpperInvariant();

            switch (tag)
            {
                case "JOY":
                    // "JOY,x,y" is the axes; "JOY,DOWN"/"JOY,UP,ms" is the stick's button.
                    if (fields.Length >= 3 && TryInt(fields[1], out int x) && TryInt(fields[2], out int y))
                    {
                        JoyX = x;
                        JoyY = y;
                        UpdateStickDirection();
                        LastValidLine = line;
                    }
                    else if (fields.Length >= 2)
                    {
                        HandleButtonState(fields[1], line, isExternal: false);
                    }
                    else InvalidLineCount++;
                    return;

                case "BTN":
                    if (fields.Length >= 2) HandleButtonState(fields[1], line, isExternal: true);
                    else InvalidLineCount++;
                    return;

                case "POT":
                    // The potentiometer was removed from the controller; an outdated
                    // sketch may still send these — ignore them, warn exactly once.
                    ObsoleteLineCount++;
                    if (!warnedObsoletePot)
                    {
                        warnedObsoletePot = true;
                        Debug.LogWarning("[Arduino] Obsolete POT message received — the board is running " +
                                         "an outdated sketch. Re-upload my_ardruino_sketch (potentiometer removed). " +
                                         "These messages are ignored; this warning is shown once.");
                    }
                    return;

                default:
                    InvalidLineCount++;
                    return;
            }
        }

        void HandleButtonState(string state, string line, bool isExternal)
        {
            switch (state.Trim().ToUpperInvariant())
            {
                case "DOWN":
                    if (isExternal) ExternalButtonHeld = true; else JoystickButtonHeld = true;
                    pendingButtonPresses++;
                    LastValidLine = line;
                    return;
                case "UP":
                    if (isExternal) ExternalButtonHeld = false; else JoystickButtonHeld = false;
                    LastValidLine = line;
                    return;
                case "HELD":
                    // Holding a button must not repeat Submit.
                    LastValidLine = line;
                    return;
                default:
                    InvalidLineCount++;
                    return;
            }
        }

        /// <summary>
        /// Deadzone + hysteresis + diagonal resolution. A diagonal picks the axis
        /// with the larger displacement; on a tie the vertical axis wins, so a
        /// single direction (and a single logical action) comes out per frame.
        /// </summary>
        void UpdateStickDirection()
        {
            int dx = JoyX - settings.joystickCenter;
            int dy = JoyY - settings.joystickCenter;
            if (settings.invertX) dx = -dx;
            if (settings.invertY) dy = -dy;

            int adx = dx < 0 ? -dx : dx;
            int ady = dy < 0 ? -dy : dy;
            int magnitude = adx > ady ? adx : ady;

            // Hysteresis: harder to leave neutral than to fall back into it.
            int threshold = Stick == StickDirection.Neutral
                ? settings.joystickEnterThreshold
                : settings.joystickExitThreshold;

            if (magnitude < threshold)
            {
                Stick = StickDirection.Neutral;
                return;
            }

            Stick = ady >= adx
                ? (dy < 0 ? StickDirection.Up : StickDirection.Down)
                : (dx < 0 ? StickDirection.Left : StickDirection.Right);
        }

        /// <summary>Pending Submit edges from both hardware buttons, consumed once per frame.</summary>
        public int ConsumeButtonPresses()
        {
            int presses = pendingButtonPresses;
            pendingButtonPresses = 0;
            return presses;
        }

        static bool TryInt(string field, out int value) =>
            int.TryParse(field.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
