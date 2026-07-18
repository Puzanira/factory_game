using UnityEditor;
using UnityEngine;
using LastShift.Input;

namespace LastShift.EditorTools
{
    /// <summary>
    /// Keeps Assets/Resources/ArduinoSerialSettings.asset available so the COM
    /// port, baud rate, deadzones and repeat timings are editable in the
    /// Inspector without touching code. Created once, never overwritten.
    /// </summary>
    public static class ArduinoSetup
    {
        const string AssetPath = "Assets/Resources/ArduinoSerialSettings.asset";

        [MenuItem("LastShift/Create Arduino Serial Settings")]
        public static void CreateSettingsAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SerialConnectionSettings>(AssetPath) != null)
            {
                Debug.Log("[Arduino] Settings asset already exists: " + AssetPath);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var settings = ScriptableObject.CreateInstance<SerialConnectionSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Arduino] Created settings asset: " + AssetPath);
        }

        [InitializeOnLoadMethod]
        static void EnsureSettingsAsset()
        {
            // Delay so the asset database is ready after a fresh import.
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<SerialConnectionSettings>(AssetPath) == null)
                    CreateSettingsAsset();
            };
        }
    }
}
