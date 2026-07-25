using UnityEngine;

namespace LastShift.UI
{
    /// <summary>
    /// Short cooldown after a tutorial acknowledgement. The same Enter that confirms
    /// a text step must never also activate a factory system, so the lesson blocks
    /// game input for a moment after every confirmation. Unscaled time: the lock
    /// works while the lesson freezes the clock.
    /// </summary>
    public class TutorialInputLock : MonoBehaviour
    {
        public const float DefaultSeconds = 0.35f;

        float lockedUntil = -1f;

        public void Lock(float seconds = DefaultSeconds) =>
            lockedUntil = Time.unscaledTime + Mathf.Max(0f, seconds);

        public void Clear() => lockedUntil = -1f;

        public bool Locked => Time.unscaledTime < lockedUntil;
    }
}
