using UnityEngine;
using LastShift.Utilities;

namespace LastShift.Input
{
    /// <summary>
    /// Converts physical Arduino controller state into the game's shared logical
    /// actions and publishes them through UnifiedGameInput before any menu or
    /// the terminal polls GameInput this frame:
    ///   joystick Up/Left                      -> NavigatePrevious (keyboard Up)
    ///   joystick Down/Right                   -> NavigateNext     (keyboard Down)
    ///   joystick button, external button      -> Submit           (keyboard Enter)
    /// Guarantees: at most one navigation action per frame, at most one Submit
    /// per shared debounce window (covering both hardware buttons AND keyboard
    /// Enter), and the keyboard always keeps working when the controller is
    /// missing, on a wrong port, or unplugged mid-game.
    /// Created automatically at startup; survives scene loads; never duplicates.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ArduinoInputBridge : MonoBehaviour
    {
        public static ArduinoInputBridge Instance { get; private set; }

        public ArduinoControllerReader Reader { get; private set; }
        public ArduinoInputParser Parser { get; private set; }
        public SerialConnectionSettings Settings { get; private set; }

        InputRepeatController repeat;
        float lastSubmitTime = float.NegativeInfinity;
        bool wasConnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // A browser has no serial ports and no background threads: the reader
            // relies on both (native port handles plus a read thread), so the whole
            // controller stack simply never comes up on the web build. The keyboard
            // path is untouched — GameInput still ORs raw ↑/↓/Enter, so every screen
            // stays fully playable on the same three logical actions.
            return;
#else
            if (Instance != null) return;
            if (FindFirstObjectByType<ArduinoInputBridge>() != null) return;

            var go = new GameObject("ArduinoInput");
            go.AddComponent<ArduinoControllerReader>();
            go.AddComponent<ArduinoInputBridge>();
            go.AddComponent<ArduinoDebugStatus>();
#endif
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

            Settings = SerialConnectionSettings.Active;
            Parser = new ArduinoInputParser(Settings);
            repeat = new InputRepeatController(Settings);

            Reader = GetComponent<ArduinoControllerReader>();
            if (Reader == null)
                Reader = gameObject.AddComponent<ArduinoControllerReader>();
            Reader.Initialize(Settings);
        }

        void Update()
        {
            Reader.Pump(Parser.HandleLine);

            // Unplugged mid-game: drop back to a safe idle state so a held
            // direction can never keep scrolling with the controller gone.
            if (wasConnected && !Reader.IsConnected)
                Parser.ResetToNeutral();
            wasConnected = Reader.IsConnected;

            float now = Time.unscaledTime;
            PublishNavigation(now);
            PublishSubmit(now);
        }

        void PublishNavigation(float now)
        {
            // Joystick: held direction with initial-delay + interval repeat.
            int desired = Parser.Stick switch
            {
                StickDirection.Up => -1,
                StickDirection.Left => -1,
                StickDirection.Down => +1,
                StickDirection.Right => +1,
                _ => 0
            };
            int action = repeat.Step(desired, now);
            if (action == 0)
                return;

            // Same-frame keyboard navigation wins; the Arduino action is dropped
            // so the selection moves exactly one item.
            if (GameInput.KeyboardUpPressed || GameInput.KeyboardDownPressed)
                return;

            UnifiedGameInput.PublishNavigation(action < 0);
        }

        void PublishSubmit(float now)
        {
            int hardwarePresses = Parser.ConsumeButtonPresses();

            if (GameInput.KeyboardConfirmPressed)
            {
                // Keyboard Enter shares the debounce window with the hardware
                // buttons: inside the window it is a duplicate and is muted.
                if (now - lastSubmitTime < Settings.submitDebounce)
                    UnifiedGameInput.SuppressKeyboardSubmitThisFrame();
                else
                    lastSubmitTime = now;
                return; // hardware presses in the same frame are duplicates too
            }

            if (hardwarePresses > 0 && now - lastSubmitTime >= Settings.submitDebounce)
            {
                // Both buttons pressed near-simultaneously still submit once.
                lastSubmitTime = now;
                UnifiedGameInput.PublishSubmit();
            }
        }
    }
}
