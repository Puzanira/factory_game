using UnityEngine;
using AiGameStudio.ArcadeControls;

namespace LastShift.Input
{
    /// <summary>
    /// Bridges the shared arcade-controls layer (ArcadeInput) into the game's
    /// logical actions and publishes them through UnifiedGameInput before any menu
    /// or the terminal polls GameInput this frame:
    ///   joystick Up/Left    -> NavigatePrevious
    ///   joystick Down/Right -> NavigateNext
    ///   Red button          -> Submit
    /// The Menu button is deliberately NOT read — it is reserved for the hub
    /// launcher return. Held joystick auto-repeats (initial delay, then interval).
    /// This component owns the arcade backend: it initialises ArcadeInput with the
    /// packaged keyboard mapping and pumps it each frame, so the whole read happens
    /// at execution order -100, before every consumer. GreenButton / Bang / Height
    /// / Crank exist in the contract but the factory game does not use them.
    /// Created automatically at startup; survives scene loads; never duplicates.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ArcadeInputBridge : MonoBehaviour
    {
        public static ArcadeInputBridge Instance { get; private set; }

        // Held-joystick navigation repeat (seconds).
        const float RepeatInitialDelay = 0.40f;
        const float RepeatInterval = 0.15f;
        // Fraction of full axis deflection needed before a direction counts as pushed.
        const float NavThreshold = 0.5f;

        InputRepeatController repeat;
        bool redWasHeld;
        // True only in a standalone build where nothing else set up ArcadeInput.
        // When the hub launches us in-process it owns and pumps ArcadeInput; we
        // must then only READ it — never re-initialise (clobbering the hub's
        // backend) nor pump it twice.
        bool ownsBackend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<ArcadeInputBridge>() != null) return;

            var go = new GameObject("ArcadeInput");
            go.AddComponent<ArcadeInputBridge>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Standalone: nobody set up ArcadeInput, so we own a keyboard backend
            // and pump it (the package supports driving ArcadeInput yourself). In
            // the hub the launcher already initialised and pumps ArcadeInput
            // process-wide — leave its backend untouched and only read it.
            ownsBackend = ArcadeInput.Backend == null;
            if (ownsBackend)
            {
                KeyboardMapping map = KeyboardMapping.LoadDefault();
                ArcadeInput.Initialize(new KeyboardBackend(map));
            }
            repeat = new InputRepeatController(RepeatInitialDelay, RepeatInterval);
        }

        void Update()
        {
            if (ownsBackend) ArcadeInput.Update(Time.unscaledDeltaTime);

            float now = Time.unscaledTime;
            PublishNavigation(now);
            PublishSubmit();
        }

        void PublishNavigation(float now)
        {
            Vector2 v = ArcadeInput.Joystick.Vector;

            // Up OR left -> previous; down OR right -> next. Vertical wins ties.
            int desired = 0;
            if (v.y > NavThreshold || v.x < -NavThreshold) desired = -1;
            else if (v.y < -NavThreshold || v.x > NavThreshold) desired = +1;

            int action = repeat.Step(desired, now);
            if (action != 0)
                UnifiedGameInput.PublishNavigation(action < 0);
        }

        void PublishSubmit()
        {
            bool held = ArcadeInput.RedButton.IsHeld;
            if (held && !redWasHeld)
                UnifiedGameInput.PublishSubmit();
            redWasHeld = held;
        }
    }
}
