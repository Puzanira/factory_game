using UnityEngine;

namespace LastShift.Data
{
    /// <summary>Per-room configuration asset. Geometry comes from LevelLayouts.Get(levelIndex).</summary>
    [CreateAssetMenu(menuName = "Last Shift/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("0-based index into LevelLayouts.")]
        public int levelIndex;
        public string roomName = "ROOM";
        public string sceneName = "";
        [Tooltip("Scene loaded after the engineer escapes this room. Empty when final.")]
        public string nextSceneName = "";
        public bool finalLevel;

        [Header("Systems")]
        public EngineerData engineerData;
        public EscalationData escalationData;

        [Header("Pressure")]
        public float maxPressure = 100f;
    }
}
