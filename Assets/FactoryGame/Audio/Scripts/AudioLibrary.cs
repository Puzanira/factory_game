using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace LastShift.Audio
{
    /// <summary>
    /// Registry of generated audio assets, loaded at runtime from Resources by the
    /// AudioManager. Built/refreshed by Tools ▸ Last Shift ▸ Build Audio. Every entry
    /// is optional: a missing clip falls back to in-memory SfxSynth rendering, and a
    /// missing mixer falls back to plain per-source volume scaling.
    /// </summary>
    public class AudioLibrary : ScriptableObject
    {
        public const string ResourceName = "LastShiftAudioLibrary";

        [System.Serializable]
        public class Entry
        {
            public string clipName;
            public AudioClip clip;
        }

        public List<Entry> clips = new List<Entry>();
        public AudioMixer mixer;

        public Dictionary<string, AudioClip> BuildLookup()
        {
            var map = new Dictionary<string, AudioClip>();
            foreach (var e in clips)
                if (e != null && e.clip != null && !string.IsNullOrEmpty(e.clipName))
                    map[e.clipName] = e.clip;
            return map;
        }
    }
}
