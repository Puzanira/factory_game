using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using LastShift.Audio;

namespace LastShift.EditorTools
{
    /// <summary>
    /// One-shot audio asset generator (idempotent, safe to re-run):
    ///  1. Creates the Assets/Audio folder tree.
    ///  2. Renders every SfxSynth sound to a 16-bit mono WAV in Assets/Audio/Generated
    ///     (PCM import settings so loops stay sample-exact).
    ///  3. Creates Assets/Audio/Mixers/LastShiftMixer.mixer with the
    ///     Master/Music/Ambient/Machines/Hazards/UI/Engineer/VoiceOrNarrative/Alerts
    ///     groups and exposed *Volume parameters (via the internal
    ///     AudioMixerController editor API; failures degrade gracefully — the runtime
    ///     falls back to plain volume scaling).
    ///  4. Creates/refreshes the AudioLibrary asset in Assets/Audio/Resources so the
    ///     runtime AudioManager can find the clips and mixer from any scene.
    /// Run via Tools menu or: Unity -batchmode -executeMethod LastShift.EditorTools.AudioProjectBuilder.BuildAudio
    /// </summary>
    public static class AudioProjectBuilder
    {
        const string GeneratedDir = "Assets/FactoryGame/Audio/Generated";
        const string MixerPath = "Assets/FactoryGame/Audio/Mixers/LastShiftMixer.mixer";
        const string LibraryPath = "Assets/FactoryGame/Audio/Resources/" + AudioLibrary.ResourceName + ".asset";

        static readonly string[] Folders =
        {
            "Assets/FactoryGame/Audio",
            "Assets/FactoryGame/Audio/Mixers", "Assets/FactoryGame/Audio/Music", "Assets/FactoryGame/Audio/Ambient",
            "Assets/FactoryGame/Audio/Machines", "Assets/FactoryGame/Audio/Hazards", "Assets/FactoryGame/Audio/Engineer",
            "Assets/FactoryGame/Audio/UI", "Assets/FactoryGame/Audio/Alerts", "Assets/FactoryGame/Audio/Generated",
            "Assets/FactoryGame/Audio/Scripts", "Assets/FactoryGame/Audio/Resources",
        };

        static readonly string[] ChildGroups =
        {
            "Music", "Ambient", "Machines", "Hazards", "UI", "Engineer", "VoiceOrNarrative", "Alerts",
        };

        [MenuItem("Tools/Last Shift/Build Audio")]
        public static void BuildAudio()
        {
            Debug.Log("[LastShift.Audio] Audio build started.");
            CreateFolders();
            GenerateWavs();
            AudioMixer mixer = CreateMixer();
            CreateLibrary(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LastShift.Audio] Audio build finished.");
        }

        /// <summary>Batchmode sanity check for the generated assets; logs AUDIO_VALIDATE lines.</summary>
        public static void ValidateAudio()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            int groupCount = 0;
            int exposedCount = 0;
            if (mixer != null)
            {
                groupCount = mixer.FindMatchingGroups("").Length;
                // SetFloat only works in play mode, so count the serialized exposed
                // parameters through the internal controller property instead.
                var prop = mixer.GetType().GetProperty("exposedParameters",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.GetValue(mixer, null) is Array arr) exposedCount = arr.Length;
            }
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            int clipCount = 0, nullClips = 0;
            if (lib != null)
            {
                foreach (var e in lib.clips)
                {
                    clipCount++;
                    if (e == null || e.clip == null) nullClips++;
                }
            }
            Debug.Log("AUDIO_VALIDATE mixer=" + (mixer != null) + " groups=" + groupCount +
                " exposedParams=" + exposedCount + " libMixer=" + (lib != null && lib.mixer != null) +
                " clips=" + clipCount + " nullClips=" + nullClips);
        }

        static void CreateFolders()
        {
            foreach (string folder in Folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string leaf = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        // ---------------- WAV generation ----------------

        static void GenerateWavs()
        {
            int written = 0;
            foreach (string name in SfxSynth.Names)
            {
                float[] data = SfxSynth.Render(name);
                if (data == null)
                {
                    Debug.LogWarning("[LastShift.Audio] Could not render '" + name + "'.");
                    continue;
                }
                WriteWav(Path.Combine(GeneratedDir, name + ".wav"), data, SfxSynth.Rate);
                written++;
            }
            AssetDatabase.Refresh();

            foreach (string name in SfxSynth.Names)
            {
                string assetPath = GeneratedDir + "/" + name + ".wav";
                var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                bool dirty = settings.loadType != AudioClipLoadType.DecompressOnLoad
                    || settings.compressionFormat != AudioCompressionFormat.PCM
                    || !importer.forceToMono;
                if (!dirty) continue;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM; // sample-exact loops
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            Debug.Log("[LastShift.Audio] Generated " + written + " WAV clips in " + GeneratedDir + ".");
        }

        static void WriteWav(string path, float[] samples, int rate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int byteCount = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + byteCount);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);            // PCM
                w.Write((short)1);            // mono
                w.Write(rate);
                w.Write(rate * 2);            // byte rate
                w.Write((short)2);            // block align
                w.Write((short)16);           // bits per sample
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(byteCount);
                foreach (float f in samples)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(f, -1f, 1f) * short.MaxValue));
            }
        }

        // ---------------- mixer ----------------

        static AudioMixer CreateMixer()
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                Type tController = asm.GetType("UnityEditor.Audio.AudioMixerController");
                Type tGroup = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                if (tController == null || tGroup == null)
                {
                    Debug.LogWarning("[LastShift.Audio] AudioMixerController API not found; skipping mixer.");
                    return null;
                }
                const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;

                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                if (mixer == null)
                {
                    var create = tController.GetMethod("CreateMixerControllerAtPath", Any, null,
                        new[] { typeof(string) }, null);
                    if (create == null)
                    {
                        DumpMembers(tController, "static-create");
                        return null;
                    }
                    mixer = create.Invoke(null, new object[] { MixerPath }) as AudioMixer;
                    AssetDatabase.ImportAsset(MixerPath);
                    mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                }
                if (mixer == null || !tController.IsInstanceOfType(mixer))
                {
                    Debug.LogWarning("[LastShift.Audio] Mixer asset could not be created/loaded.");
                    return mixer;
                }

                object controller = mixer;
                object master = GetMemberValue(tController, controller, "masterGroup");
                if (master == null)
                {
                    Debug.LogWarning("[LastShift.Audio] Mixer has no master group; skipping group setup.");
                    return mixer;
                }

                var createGroup = tController.GetMethod("CreateNewGroup", Any, null,
                    new[] { typeof(string), typeof(bool) }, null);
                var addChild = tController.GetMethod("AddChildToParent", Any);
                var addToView = tController.GetMethod("AddGroupToCurrentView", Any);

                // A headless-created mixer has no group views; view bookkeeping is
                // GUI-only, so create one if we can and otherwise skip it entirely.
                bool viewsOk = EnsureView(tController, controller);

                var groupsByName = new Dictionary<string, object> { { "Master", master } };
                foreach (var g in mixer.FindMatchingGroups(""))
                    if (g != null && !groupsByName.ContainsKey(g.name)) groupsByName[g.name] = g;

                foreach (string name in ChildGroups)
                {
                    if (groupsByName.ContainsKey(name)) continue;
                    if (createGroup == null || addChild == null)
                    {
                        DumpMembers(tController, "group-create");
                        break;
                    }
                    object child = createGroup.Invoke(controller, new object[] { name, false });
                    addChild.Invoke(controller, new[] { child, master });
                    if (viewsOk && addToView != null)
                    {
                        try { addToView.Invoke(controller, new[] { child }); }
                        catch (Exception) { viewsOk = false; }
                    }
                    groupsByName[name] = child;
                }

                ExposeVolumes(asm, tController, tGroup, controller, groupsByName);

                // Rebuild the GUI group views so the Audio Mixer window shows all groups.
                try
                {
                    var sanitize = tController.GetMethod("SanitizeGroupViews", Any);
                    if (sanitize != null) sanitize.Invoke(controller, null);
                }
                catch (Exception) { /* views are GUI-only; safe to skip */ }

                EditorUtility.SetDirty(mixer);
                Debug.Log("[LastShift.Audio] Mixer ready with groups: " +
                    string.Join(", ", groupsByName.Keys));
                return mixer;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LastShift.Audio] Mixer creation failed (runtime falls back to " +
                    "plain volume scaling): " + e);
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
        }

        /// <summary>Makes sure the mixer has at least one GUI view; returns false when unsure.</summary>
        static bool EnsureView(Type tController, object controller)
        {
            try
            {
                var viewsValue = GetMemberValue(tController, controller, "views") as Array;
                if (viewsValue != null && viewsValue.Length > 0) return true;

                const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var createView = tController.GetMethod("CreateView", Any, null, new[] { typeof(string) }, null);
                if (createView == null) return false;
                createView.Invoke(controller, new object[] { "View" });
                viewsValue = GetMemberValue(tController, controller, "views") as Array;
                return viewsValue != null && viewsValue.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void ExposeVolumes(Assembly asm, Type tController, Type tGroup,
            object controller, Dictionary<string, object> groupsByName)
        {
            var getGuid = tGroup.GetMethod("GetGUIDForVolume",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type tExposed = asm.GetType("UnityEditor.Audio.ExposedAudioParameter");
            var exposedProp = tController.GetProperty("exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getGuid == null || tExposed == null || exposedProp == null)
            {
                DumpMembers(tController, "expose");
                DumpMembers(tGroup, "expose-group");
                return;
            }

            var wanted = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Master", "MasterVolume") };
            foreach (string g in ChildGroups)
                if (g != "VoiceOrNarrative") wanted.Add(new KeyValuePair<string, string>(g, g + "Volume"));

            var entries = new List<object>();
            foreach (var kv in wanted)
            {
                if (!groupsByName.TryGetValue(kv.Key, out object group)) continue;
                object guid = getGuid.Invoke(group, null);
                object param = Activator.CreateInstance(tExposed);
                SetMemberValue(tExposed, param, "guid", guid);
                SetMemberValue(tExposed, param, "name", kv.Value);
                entries.Add(param);
            }

            var array = Array.CreateInstance(tExposed, entries.Count);
            for (int i = 0; i < entries.Count; i++) array.SetValue(entries[i], i);
            exposedProp.SetValue(controller, array, null);
        }

        static object GetMemberValue(Type type, object obj, string name)
        {
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = type.GetProperty(name, Any);
            if (prop != null) return prop.GetValue(obj, null);
            var field = type.GetField(name, Any);
            return field != null ? field.GetValue(obj) : null;
        }

        static void SetMemberValue(Type type, object obj, string name, object value)
        {
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, Any);
            if (field != null) { field.SetValue(obj, value); return; }
            var prop = type.GetProperty(name, Any);
            if (prop != null) prop.SetValue(obj, value, null);
        }

        /// <summary>Diagnostics when the internal editor API surface changed.</summary>
        static void DumpMembers(Type type, string label)
        {
            var names = new List<string>();
            foreach (var m in type.GetMembers(BindingFlags.Instance | BindingFlags.Static |
                                              BindingFlags.Public | BindingFlags.NonPublic))
                names.Add(m.MemberType + ":" + m.Name);
            names.Sort();
            Debug.LogWarning("[LastShift.Audio] API mismatch at '" + label + "' for " + type.Name +
                ". Members: " + string.Join(", ", names));
        }

        // ---------------- library ----------------

        static void CreateLibrary(AudioMixer mixer)
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.mixer = mixer != null ? mixer : AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            lib.clips.Clear();
            int missing = 0;
            foreach (string name in SfxSynth.Names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(GeneratedDir + "/" + name + ".wav");
                if (clip == null) { missing++; continue; }
                lib.clips.Add(new AudioLibrary.Entry { clipName = name, clip = clip });
            }
            EditorUtility.SetDirty(lib);
            Debug.Log("[LastShift.Audio] AudioLibrary updated: " + lib.clips.Count +
                " clips" + (missing > 0 ? ", " + missing + " missing (runtime synth fallback)" : "") +
                ", mixer=" + (lib.mixer != null));
        }
    }
}
