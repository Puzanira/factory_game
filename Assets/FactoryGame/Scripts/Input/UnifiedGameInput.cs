using UnityEngine;

namespace LastShift.Input
{
    /// <summary>
    /// Frame-stamped logical actions. <see cref="ArcadeInputBridge"/> publishes
    /// these from the shared arcade-controls layer before any consumer polls, and
    /// GameInput exposes them as UpPressed / DownPressed / ConfirmPressed. The
    /// smoke-test driver injects here too, so every logical action shares one seam.
    /// Flags are only valid for the frame they were published in.
    /// </summary>
    public static class UnifiedGameInput
    {
        static int navFrame = -1;
        static bool navIsPrevious;
        static int submitFrame = -1;

        /// <summary>A "previous item" step was requested this frame.</summary>
        public static bool NavigatePrevious => navFrame == Time.frameCount && navIsPrevious;

        /// <summary>A "next item" step was requested this frame.</summary>
        public static bool NavigateNext => navFrame == Time.frameCount && !navIsPrevious;

        /// <summary>A Submit was requested this frame.</summary>
        public static bool Submit => submitFrame == Time.frameCount;

        /// <summary>At most one navigation action per frame: previous OR next, never both.</summary>
        public static void PublishNavigation(bool previous)
        {
            navFrame = Time.frameCount;
            navIsPrevious = previous;
        }

        public static void PublishSubmit() => submitFrame = Time.frameCount;
    }
}
