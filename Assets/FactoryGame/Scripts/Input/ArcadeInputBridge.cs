using UnityEngine;
using UnityEngine.SceneManagement;
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

        // Create the bridge PER FACTORY-SCENE LOAD, not once at app start. Standalone this is
        // functionally the same (the first scene IS a factory scene, so the bridge appears
        // immediately and — being DontDestroyOnLoad — survives Boot→level transitions).
        //
        // In the arcade-hub it is the essential difference: the hub launches Factory in-process
        // and, on every menu entry, its DDOL "janitor" sweeps foreign DontDestroyOnLoad roots
        // (this bridge is namespace LastShift.Input — foreign to the hub). A once-only
        // AfterSceneLoad bootstrap spawned the bridge in the HUB MENU at startup, where the
        // janitor immediately swept it and — being once-only — it never came back, so Factory
        // launched with NO ArcadeInput→UnifiedGameInput bridge and the cabinet RedButton
        // ("КРАСНАЯ КНОПКА — ДАЛЕЕ") did nothing. Rebuilding on each factory-scene load makes
        // the sweep harmless: the next Factory launch re-creates the bridge in its own scene.
        // Gated to factory scenes so it never spawns in the hub menu / another game's scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => EnsureForScene(scene);
            // The active scene at startup already fired its load before this callback was
            // attached (standalone Boot, or a directly-played factory scene) — cover it now.
            EnsureForScene(SceneManager.GetActiveScene());
        }

        static void EnsureForScene(Scene scene)
        {
            if (Instance != null) return;
            if (!IsFactoryScene(scene)) return;
            if (FindAnyObjectByType<ArcadeInputBridge>() != null) return;

            var go = new GameObject("ArcadeInput");
            go.AddComponent<ArcadeInputBridge>();
        }

        // True for Factory's own scenes, in BOTH layouts: the standalone build
        // ("Assets/FactoryGame/…") and the arcade-hub package path
        // ("Packages/com.aigamestudio.game-factory/…"). Matched by explicit path
        // prefixes/segments — NOT a bare "factory" substring, which could false-positive
        // on an unrelated scene that merely contains the word (e.g. another game's
        // "ToyFactory" level parked in the same build).
        static bool IsFactoryScene(Scene scene)
        {
            string path = scene.path;
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.ToLowerInvariant();
            return p.StartsWith("assets/factorygame/")
                || p.Contains("/com.aigamestudio.game-factory/");
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

            // Prime the Submit edge-detector with the CURRENT red state (the hub's Prime()
            // pattern): launching Factory from the hub is a ~5s RED hold, so the bridge is
            // (re)created while the button is still physically down. Starting redWasHeld at
            // false would read that stale hold as a rising edge on the very first Update and
            // auto-advance the first «КРАСНАЯ КНОПКА — ДАЛЕЕ» screen. Priming means the first
            // Submit fires only after a seen release + a fresh press. Standalone this is a
            // no-op (nothing is held at startup).
            redWasHeld = ArcadeInput.RedButton.IsHeld;
        }

        void OnDestroy()
        {
            // Clear the static handle when we're torn down (e.g. the hub's DDOL janitor sweeps
            // us on menu entry) so EnsureForScene builds a fresh bridge on the next factory load.
            if (Instance == this) Instance = null;
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
