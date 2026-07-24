using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using LastShift.Input;

namespace LastShift.Utilities
{
    /// <summary>
    /// Central logical input built on the new Input System, merged from two
    /// physical sources: the keyboard (always available, never disabled) and the
    /// optional Arduino controller (2-axis joystick + two buttons),
    /// which publishes into UnifiedGameInput before consumers poll.
    /// Keyboard: Up / Down / Enter (+ Esc for pause/back) keeps working when the
    /// Arduino is missing, on the wrong port, or unplugged mid-game.
    /// Q and R are intentionally not bound to anything.
    /// </summary>
    public static class GameInput
    {
        // Raw keyboard state, used by the Arduino bridge for same-frame merging.
        public static bool KeyboardUpPressed => Pressed(Keyboard.current?.upArrowKey);
        public static bool KeyboardDownPressed => Pressed(Keyboard.current?.downArrowKey);
        public static bool KeyboardConfirmPressed => Pressed(Keyboard.current?.enterKey) || Pressed(Keyboard.current?.numpadEnterKey);

        // Merged logical actions polled by menus, intro and the command terminal.
        public static bool UpPressed => KeyboardUpPressed || UnifiedGameInput.NavigatePrevious;
        public static bool DownPressed => KeyboardDownPressed || UnifiedGameInput.NavigateNext;
        public static bool ConfirmPressed =>
            (KeyboardConfirmPressed && !UnifiedGameInput.KeyboardSubmitSuppressed) || UnifiedGameInput.Submit;
        public static bool EscapePressed => Pressed(Keyboard.current?.escapeKey);

        /// <summary>Editor-only fast-forward (hold Space). Does nothing in released builds.</summary>
        public static bool SpeedHeld =>
            Application.isEditor && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;
    }
}
