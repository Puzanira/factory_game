using UnityEngine;
using LastShift.Core;

namespace LastShift.Audio
{
    /// <summary>
    /// Per-room ambience and music state machine. Creates the level's ambient bed and
    /// the low music drone with a fade-in, layers a subtle tension drone at 75%
    /// Pressure, switches to an industrial alarm pulse on escalation, thins the mix
    /// when the engineer starts retreating (so the retreat reads clearly), and plays
    /// sparse per-room flavour one-shots (valve hiss / distant press / ice cracks).
    /// Added by LevelManager; loops are cleaned up by the AudioManager on scene load.
    /// </summary>
    public class RoomAudioDirector : MonoBehaviour
    {
        LevelManager lm;
        int levelIndex;
        bool escalated;
        bool retreating;
        float nextFlavorAt;

        static readonly string[] AmbientByLevel =
            { "ambient_l1_loop", "ambient_l2_loop", "ambient_l3_loop" };

        public void Init(LevelManager levelManager, int level)
        {
            lm = levelManager;
            levelIndex = Mathf.Clamp(level, 0, AmbientByLevel.Length - 1);

            AudioManager.Ensure();
            // Level 3 is the cold storage: colder, slightly more present low drone.
            float ambVol = levelIndex == 2 ? 1f : 0.85f;
            AudioManager.LoopOn("room_ambient", AmbientByLevel[levelIndex], SfxBus.Ambient, ambVol, 1.6f);
            AudioManager.LoopOn("room_music", "music_drone_loop", SfxBus.Music, 0.8f, 2.5f);

            if (lm.Pressure != null)
            {
                lm.Pressure.WarningReached += OnPressureWarning;
                lm.Pressure.MaxReached += OnEscalation;
            }
            if (lm.Engineer != null && lm.Engineer.Stats != null)
                lm.Engineer.Stats.ResolveEmpty += OnRetreatStarted;

            ScheduleFlavor();
        }

        void OnPressureWarning()
        {
            if (escalated) return;
            AudioManager.LoopOn("room_tension", "tension_loop", SfxBus.Music, 0.9f, 2f);
        }

        void OnEscalation()
        {
            escalated = true;
            AudioManager.LoopOff("room_tension", 0.8f);
            AudioManager.Play("escalation_start", SfxBus.Alerts, 0.8f);
            AudioManager.LoopOn("room_escalation", "escalation_loop", SfxBus.Alerts, 0.7f, 1.2f);
        }

        void OnRetreatStarted()
        {
            if (retreating) return;
            retreating = true;
            // Thin the background so the retreat feedback stays readable.
            AudioManager.LoopSetVolume("room_ambient", 0.5f, 1.5f);
            AudioManager.LoopSetVolume("room_music", 0.4f, 1.5f);
            AudioManager.LoopOff("room_tension", 1.2f);
            if (escalated) AudioManager.LoopSetVolume("room_escalation", 0.4f, 1.5f);
        }

        void ScheduleFlavor() => nextFlavorAt = Time.time + Random.Range(7f, 15f);

        void Update()
        {
            if (lm == null || lm.RoomEnded) return;
            if (Time.time < nextFlavorAt) return;
            ScheduleFlavor();

            // Sparse room flavour, always quiet and off to one side.
            Vector2 pos = new Vector2(Random.Range(-6f, 6f), Random.Range(-4f, 4f));
            switch (levelIndex)
            {
                case 0:
                    AudioManager.PlayAt("steam_burst", pos, SfxBus.Ambient, 0.25f, 0.8f);
                    break;
                case 1:
                    AudioManager.PlayAt(Random.value < 0.5f ? "forklift_bump" : "valve_click",
                        pos, SfxBus.Ambient, 0.25f, 0.85f);
                    break;
                default:
                    AudioManager.PlayAt(Random.value < 0.7f ? "ice_crackle" : "blocked_thud",
                        pos, SfxBus.Ambient, 0.3f, 0.9f);
                    break;
            }
        }
    }
}
