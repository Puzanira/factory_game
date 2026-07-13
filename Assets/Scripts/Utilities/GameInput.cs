using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LastShift.Utilities
{
    /// <summary>
    /// Central keyboard polling built on the new Input System.
    /// The whole game is keyboard-only by design: Up / Down / Enter (+ Esc, R, Q, Space).
    /// </summary>
    public static class GameInput
    {
        public static bool UpPressed => Pressed(Keyboard.current?.upArrowKey);
        public static bool DownPressed => Pressed(Keyboard.current?.downArrowKey);
        public static bool ConfirmPressed => Pressed(Keyboard.current?.enterKey) || Pressed(Keyboard.current?.numpadEnterKey);
        public static bool EscapePressed => Pressed(Keyboard.current?.escapeKey);
        public static bool RestartPressed => Pressed(Keyboard.current?.rKey);
        public static bool QuitPressed => Pressed(Keyboard.current?.qKey);

        /// <summary>Dev-only fast-forward (hold Space). Never required for normal play.</summary>
        public static bool SpeedHeld => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;
    }
}
