using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LastShift.Audio
{
    public enum SfxBus { Music, Ambient, Machines, Hazards, UI, Engineer, Alerts }

    /// <summary>
    /// Persistent audio hub: routes one-shots and managed loops to AudioMixer groups,
    /// prevents duplicate/stacked loops (keyed by id), fades loops in/out with
    /// unscaled time (so fades finish while paused), rate-limits spammy sounds,
    /// and ducks gameplay audio while paused. Volume itself is not the game's to
    /// own — on the cabinet the launcher sets it, so there are no volume settings
    /// and nothing is persisted.
    /// Fails safe: a missing library/mixer/clip degrades to synth clips and plain
    /// AudioSource volume scaling — never an exception.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int OneShotVoices = 10;

        static bool quitting;

        AudioLibrary library;
        AudioMixer mixer;
        bool mixerParamsOk;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly HashSet<string> missingWarned = new HashSet<string>();
        readonly Dictionary<SfxBus, AudioMixerGroup> groups = new Dictionary<SfxBus, AudioMixerGroup>();
        readonly Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>();

        AudioSource[] oneShotPool;
        int poolIndex;

        class Loop
        {
            public string id;
            public AudioSource source;
            public SfxBus bus;
            public float baseVolume;   // designed loudness of this loop
            public float fadeLevel;    // 0..1 current fade position
            public float fadeTarget;   // 0..1
            public float fadeRate;     // fade-level units per second
            public bool removeWhenSilent;
        }

        readonly Dictionary<string, Loop> loops = new Dictionary<string, Loop>();
        readonly List<string> loopRemoveScratch = new List<string>();

        bool gamePaused;

        // ---------------- designed bus levels ----------------

        static readonly Dictionary<SfxBus, float> BaseDb = new Dictionary<SfxBus, float>
        {
            { SfxBus.Music, -16f }, { SfxBus.Ambient, -20f }, { SfxBus.Machines, -12f },
            { SfxBus.Hazards, -10f }, { SfxBus.UI, -8f }, { SfxBus.Engineer, -14f },
            { SfxBus.Alerts, -10f },
        };

        static readonly Dictionary<SfxBus, string> ParamNames = new Dictionary<SfxBus, string>
        {
            { SfxBus.Music, "MusicVolume" }, { SfxBus.Ambient, "AmbientVolume" },
            { SfxBus.Machines, "MachinesVolume" }, { SfxBus.Hazards, "HazardsVolume" },
            { SfxBus.UI, "UIVolume" }, { SfxBus.Engineer, "EngineerVolume" },
            { SfxBus.Alerts, "AlertsVolume" },
        };

        // There are no master/music/sfx settings and no PlayerPrefs behind them.
        // On the cabinet volume belongs to the launcher, not to the game; the
        // settings screen that used to drive them was removed with the pause menu,
        // and the game plays every bus at the designed loudness above.

        // ---------------- lifecycle ----------------

        public static AudioManager Ensure()
        {
            if (Instance == null && !quitting && Application.isPlaying)
            {
                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();
            }
            return Instance;
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

            library = Resources.Load<AudioLibrary>(AudioLibrary.ResourceName);
            if (library != null)
            {
                foreach (var kv in library.BuildLookup()) clips[kv.Key] = kv.Value;
                mixer = library.mixer;
            }
            ResolveMixerGroups();

            oneShotPool = new AudioSource[OneShotVoices];
            for (int i = 0; i < OneShotVoices; i++)
                oneShotPool[i] = CreateSource("OneShot" + i);

            ApplyVolumes();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        void OnApplicationQuit() => quitting = true;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Never carry room loops (alarms, conveyors, ambience) across scenes,
            // and never stay in the paused duck state after a reload.
            StopAllLoopsInternal(0.6f);
            gamePaused = false;

            // Runtime-created cameras may lack a listener; audio needs exactly one.
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
        }

        void ResolveMixerGroups()
        {
            groups.Clear();
            mixerParamsOk = false;
            if (mixer == null) return;
            foreach (SfxBus bus in System.Enum.GetValues(typeof(SfxBus)))
            {
                var found = mixer.FindMatchingGroups(bus.ToString());
                foreach (var g in found)
                    if (g != null && g.name == bus.ToString()) { groups[bus] = g; break; }
            }
            mixerParamsOk = mixer.SetFloat("MasterVolume", 0f);
        }

        AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        // ---------------- volumes ----------------

        /// <summary>Puts every bus at its designed loudness; master stays at unity gain.</summary>
        void ApplyVolumes()
        {
            if (mixer != null && mixerParamsOk)
            {
                mixer.SetFloat("MasterVolume", 0f);   // 0 dB — уровень задаёт лаунчер автомата
                foreach (var kv in ParamNames)
                    mixer.SetFloat(kv.Value, BaseDb[kv.Key]);
            }
            // Scalar fallback volumes are recomputed continuously in Update (loops)
            // and per-call (one-shots), so nothing else to do here.
        }

        /// <summary>Linear gain for a bus when no mixer routing is available.</summary>
        float ScalarBusGain(SfxBus bus)
        {
            if (mixer != null && mixerParamsOk) return 1f; // mixer handles it
            return Mathf.Pow(10f, BaseDb[bus] / 20f);
        }

        float PauseDuck(SfxBus bus)
        {
            if (!gamePaused) return 1f;
            switch (bus)
            {
                case SfxBus.Machines:
                case SfxBus.Hazards:
                case SfxBus.Engineer:
                case SfxBus.Alerts: return 0.15f;
                case SfxBus.Ambient: return 0.45f;
                default: return 1f;
            }
        }

        public static void SetGamePaused(bool paused)
        {
            if (Instance != null) Instance.gamePaused = paused;
        }

        // ---------------- clips ----------------

        AudioClip GetClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (clips.TryGetValue(name, out var clip) && clip != null) return clip;
            clip = SfxSynth.CreateClip(name);
            if (clip != null)
            {
                clips[name] = clip;
                return clip;
            }
            if (missingWarned.Add(name))
                Debug.LogWarning("[LastShift.Audio] Unknown audio clip '" + name + "' — skipping.");
            return null;
        }

        // ---------------- one-shots ----------------

        public static void Play(string name, SfxBus bus, float volume = 1f, float pitch = 1f)
        {
            var m = Ensure();
            if (m != null) m.PlayInternal(name, bus, volume, pitch, 0f);
        }

        /// <summary>One-shot with gentle stereo pan from the world position on screen.</summary>
        public static void PlayAt(string name, Vector2 worldPos, SfxBus bus, float volume = 1f, float pitch = 1f)
        {
            var m = Ensure();
            if (m != null) m.PlayInternal(name, bus, volume, pitch, m.PanFor(worldPos));
        }

        /// <summary>Rate-limited one-shot; returns true when it actually played.</summary>
        public static bool PlayLimited(string key, float minInterval, string name, SfxBus bus,
            float volume = 1f, float pitch = 1f)
        {
            var m = Ensure();
            if (m == null) return false;
            if (m.lastPlayTimes.TryGetValue(key, out float last) &&
                Time.unscaledTime - last < minInterval) return false;
            m.lastPlayTimes[key] = Time.unscaledTime;
            m.PlayInternal(name, bus, volume, pitch, 0f);
            return true;
        }

        float PanFor(Vector2 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            float vx = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f)).x;
            return Mathf.Clamp((vx - 0.5f) * 0.8f, -0.4f, 0.4f);
        }

        void PlayInternal(string name, SfxBus bus, float volume, float pitch, float pan)
        {
            if (gamePaused && bus != SfxBus.UI && bus != SfxBus.Music) return;
            var clip = GetClip(name);
            if (clip == null || oneShotPool == null) return;

            var src = oneShotPool[poolIndex];
            poolIndex = (poolIndex + 1) % oneShotPool.Length;
            if (src == null) return;
            src.outputAudioMixerGroup = groups.TryGetValue(bus, out var g) ? g : null;
            src.pitch = Mathf.Clamp(pitch, 0.3f, 2.5f);
            src.panStereo = pan;
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * ScalarBusGain(bus));
        }

        // ---------------- managed loops ----------------

        /// <summary>
        /// Starts (or retargets) a fading loop. Calling again with the same id never
        /// stacks a second copy — it just updates the target volume.
        /// </summary>
        public static void LoopOn(string id, string name, SfxBus bus, float volume, float fadeSeconds)
        {
            var m = Ensure();
            if (m != null) m.LoopOnInternal(id, name, bus, volume, fadeSeconds);
        }

        public static void LoopOff(string id, float fadeSeconds)
        {
            if (Instance != null) Instance.LoopOffInternal(id, fadeSeconds);
        }

        /// <summary>Retargets a running loop's volume (no-op when the loop is absent).</summary>
        public static void LoopSetVolume(string id, float volume, float fadeSeconds)
        {
            if (Instance == null) return;
            if (!Instance.loops.TryGetValue(id, out var loop)) return;
            loop.baseVolume = Mathf.Clamp01(volume);
            loop.fadeTarget = 1f;
            loop.fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds);
            loop.removeWhenSilent = false;
        }

        public static void StopAllLoops(float fadeSeconds)
        {
            if (Instance != null) Instance.StopAllLoopsInternal(fadeSeconds);
        }

        void LoopOnInternal(string id, string name, SfxBus bus, float volume, float fadeSeconds)
        {
            if (loops.TryGetValue(id, out var existing))
            {
                existing.baseVolume = Mathf.Clamp01(volume);
                existing.fadeTarget = 1f;
                existing.fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds);
                existing.removeWhenSilent = false;
                return;
            }

            var clip = GetClip(name);
            if (clip == null) return;

            var src = CreateSource("Loop_" + id);
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.outputAudioMixerGroup = groups.TryGetValue(bus, out var g) ? g : null;
            src.Play();

            loops[id] = new Loop
            {
                id = id,
                source = src,
                bus = bus,
                baseVolume = Mathf.Clamp01(volume),
                fadeLevel = 0f,
                fadeTarget = 1f,
                fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds),
            };
        }

        void LoopOffInternal(string id, float fadeSeconds)
        {
            if (!loops.TryGetValue(id, out var loop)) return;
            loop.fadeTarget = 0f;
            loop.fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds);
            loop.removeWhenSilent = true;
        }

        void StopAllLoopsInternal(float fadeSeconds)
        {
            foreach (var loop in loops.Values)
            {
                loop.fadeTarget = 0f;
                loop.fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds);
                loop.removeWhenSilent = true;
            }
        }

        void Update()
        {
            if (loops.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            loopRemoveScratch.Clear();
            foreach (var loop in loops.Values)
            {
                loop.fadeLevel = Mathf.MoveTowards(loop.fadeLevel, loop.fadeTarget, loop.fadeRate * dt);
                if (loop.source != null)
                    loop.source.volume = loop.fadeLevel * loop.baseVolume
                        * ScalarBusGain(loop.bus) * PauseDuck(loop.bus);
                if (loop.removeWhenSilent && loop.fadeLevel <= 0f)
                    loopRemoveScratch.Add(loop.id);
            }
            foreach (var id in loopRemoveScratch)
            {
                if (loops.TryGetValue(id, out var loop) && loop.source != null)
                    Destroy(loop.source.gameObject);
                loops.Remove(id);
            }
        }

        // ---------------- room outcome stingers ----------------

        /// <summary>Engineer retreated from a non-final room: short cold success tone.</summary>
        public static void OnRoomWon()
        {
            StopAllLoops(1.2f);
            Play("room_won", SfxBus.Music, 0.9f);
        }

        /// <summary>«ЦЕХ СТАБИЛИЗИРОВАН»: low disappointed shutdown tone.</summary>
        public static void OnRoomLost()
        {
            StopAllLoops(1.0f);
            Play("factory_defeat", SfxBus.Music, 0.9f);
        }

        /// <summary>«ЗАВОД ПОБЕДИЛ»: cold controlled victory, not cheerful.</summary>
        public static void OnFinalVictory()
        {
            StopAllLoops(1.5f);
            Play("factory_victory", SfxBus.Music, 1f);
        }
    }

    /// <summary>Thin helpers for keyboard-UI sounds so call sites stay one-liners.</summary>
    public static class UiSfx
    {
        public static void TerminalMove() => AudioManager.Play("terminal_move", SfxBus.UI, 0.7f);
        public static void Confirm() => AudioManager.Play("terminal_confirm", SfxBus.UI, 0.85f);
        public static void Denied() => AudioManager.Play("terminal_denied", SfxBus.UI, 0.7f);
        public static void PageFlip() => AudioManager.Play("intro_page", SfxBus.UI, 0.7f);
        public static void PauseMove() => AudioManager.Play("ui_pause_move", SfxBus.UI, 0.7f);
        public static void PauseConfirm() => AudioManager.Play("ui_pause_confirm", SfxBus.UI, 0.8f);

        /// <summary>Command left cooldown; rate-limited so mass-recovery never spams.</summary>
        public static void CommandReady() =>
            AudioManager.PlayLimited("ui_ready", 1.5f, "terminal_ready", SfxBus.UI, 0.5f);

        /// <summary>Command entered cooldown (tiny shutdown click, rate-limited).</summary>
        public static void CommandCooldown() =>
            AudioManager.PlayLimited("ui_cooldown", 0.4f, "terminal_cooldown", SfxBus.UI, 0.45f);
    }
}
