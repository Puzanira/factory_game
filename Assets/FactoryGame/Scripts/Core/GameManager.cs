using UnityEngine;

namespace LastShift.Core
{
    /// <summary>
    /// Persistent app-level singleton: scene name constants and global bookkeeping.
    /// Deliberately small — per-room orchestration lives in LevelManager.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string BootScene = "Boot";
        public const string Level1Scene = "Level_01_RawMilkIntake";
        public const string Level2Scene = "Level_02_PackagingLine";
        public const string Level3Scene = "Level_03_ColdStorage";

        public static GameManager Instance { get; private set; }

        /// <summary>Index of the room currently being played (0-based).</summary>
        public int CurrentLevelIndex { get; set; }

        /// <summary>
        /// «ПРОЙТИ УРОК» was chosen: the next Level 1 load runs the interactive
        /// tutorial room instead of the normal layout. Session-only (no PlayerPrefs).
        /// </summary>
        public static bool TutorialRequested { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Creates the singleton on demand so any scene can be played directly.</summary>
        public static GameManager Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
            return Instance;
        }
    }
}
