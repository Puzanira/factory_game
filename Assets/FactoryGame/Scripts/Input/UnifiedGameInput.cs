using UnityEngine;

namespace LastShift.Input
{
    /// <summary>
    /// Frame-stamped logical actions published by non-keyboard sources
    /// (the Arduino bridge). GameInput ORs these into the same UpPressed /
    /// DownPressed / ConfirmPressed properties every menu and the terminal
    /// already poll, so both physical sources share one code path.
    /// Flags are only valid for the frame they were published in; when no
    /// Arduino bridge is running they stay stale and the game is pure keyboard.
    /// </summary>
    public static class UnifiedGameInput
    {
        static int navFrame = -1;
        static bool navIsPrevious;
        static int submitFrame = -1;
        static int suppressKeyboardSubmitFrame = -1;

        /// <summary>Arduino asked for one "previous item" step this frame.</summary>
        public static bool NavigatePrevious => navFrame == Time.frameCount && navIsPrevious;

        /// <summary>Arduino asked for one "next item" step this frame.</summary>
        public static bool NavigateNext => navFrame == Time.frameCount && !navIsPrevious;

        /// <summary>Arduino asked for one Submit this frame.</summary>
        public static bool Submit => submitFrame == Time.frameCount;

        /// <summary>Keyboard Enter falls inside the shared submit-debounce window.</summary>
        public static bool KeyboardSubmitSuppressed => suppressKeyboardSubmitFrame == Time.frameCount;

        /// <summary>At most one navigation action per frame: previous OR next, never both.</summary>
        public static void PublishNavigation(bool previous)
        {
            navFrame = Time.frameCount;
            navIsPrevious = previous;
        }

        public static void PublishSubmit() => submitFrame = Time.frameCount;

        public static void SuppressKeyboardSubmitThisFrame() => suppressKeyboardSubmitFrame = Time.frameCount;
    }
}
