using UnityEngine;

namespace LastShift.Input
{
    /// <summary>
    /// All tunables for the optional Arduino Nano controller: serial connection,
    /// joystick deadzones, repeat timing and button debounce.
    /// Lives in Assets/Resources/ArduinoSerialSettings.asset so it can be edited
    /// in the Inspector without touching code; when the asset is missing the
    /// built-in defaults below are used.
    /// </summary>
    [CreateAssetMenu(menuName = "LastShift/Arduino Serial Settings", fileName = "ArduinoSerialSettings")]
    public sealed class SerialConnectionSettings : ScriptableObject
    {
        public const string ResourcesName = "ArduinoSerialSettings";

        [Header("Serial connection")]
        [Tooltip("Exact port to open (Windows: COM5, macOS: /dev/cu.usbserial-110). Leave empty to auto-detect USB serial adapters.")]
        public string portName = "";
        [Tooltip("Must match Serial.begin() in my_ardruino_sketch (115200).")]
        public int baudRate = 115200;
        [Min(0.25f)] public float portScanInterval = 1f;
        [Tooltip("A candidate port is closed if it never sends JOY/POT/BTN lines within this many seconds (a Nano auto-resets on open and needs ~2 s to boot).")]
        [Min(2f)] public float identificationTimeout = 8f;
        [Tooltip("Delay before re-trying a port that failed or disconnected.")]
        [Min(0.5f)] public float reconnectDelay = 3f;
        [Min(1)] public int maxMessagesPerFrame = 200;
        public bool logConnections = true;

        [Header("Joystick (raw 0..1023, center ~512)")]
        public int joystickCenter = 512;
        [Tooltip("Displacement from center needed to LEAVE neutral (deadzone radius).")]
        public int joystickEnterThreshold = 170;
        [Tooltip("Displacement below which the stick returns to neutral (hysteresis, must be < enter threshold).")]
        public int joystickExitThreshold = 120;
        [Tooltip("Flip if left/right feel swapped for how the stick is mounted.")]
        public bool invertX;
        [Tooltip("Flip if up/down feel swapped for how the stick is mounted.")]
        public bool invertY;

        [Header("Navigation repeat (joystick held)")]
        public float repeatInitialDelay = 0.40f;
        public float repeatInterval = 0.15f;

        [Header("Buttons")]
        [Tooltip("Shared window: both hardware buttons and keyboard Enter count as one Submit inside it.")]
        public float submitDebounce = 0.25f;

        [Header("Debug")]
        [Tooltip("Show the on-screen Arduino status overlay (also toggled at runtime with F9).")]
        public bool debugOverlay;

        static SerialConnectionSettings active;

        /// <summary>Asset from Resources if present, otherwise a default instance.</summary>
        public static SerialConnectionSettings Active
        {
            get
            {
                if (active == null)
                {
                    active = Resources.Load<SerialConnectionSettings>(ResourcesName);
                    if (active == null)
                        active = CreateInstance<SerialConnectionSettings>();
                }
                return active;
            }
        }
    }
}
