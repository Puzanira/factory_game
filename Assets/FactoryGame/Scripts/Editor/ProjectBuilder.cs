using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using LastShift.Core;
using LastShift.Data;
using LastShift.Machines;

namespace LastShift.EditorTools
{
    /// <summary>
    /// One-shot project generator: folders, ScriptableObject assets, prefabs, the four
    /// scenes and Build Settings. Idempotent — safe to re-run (regenerates everything).
    /// Run via Tools menu or: Unity -batchmode -executeMethod LastShift.EditorTools.ProjectBuilder.BuildAll
    /// </summary>
    public static class ProjectBuilder
    {
        const string DocsPath = "Assets/FactoryGame/Documentation";

        static PrefabLibrary library;
        static EngineerData engineerData;
        static EscalationData escalationData;

        [MenuItem("Tools/Last Shift/Build All")]
        public static void BuildAll()
        {
            Log("=== LAST SHIFT project build started ===");
            EnsureSortingLayers();
            CreateFolders();
            CreateCoreData();
            CreatePrefabs();
            CreateLevelData();
            CreateScenes();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("=== LAST SHIFT project build finished ===");
            Debug.Log("[LastShift] ProjectBuilder finished successfully.");
        }

        // ---------------- sorting layers ----------------

        static readonly string[] SortingLayerNames =
        {
            "Floor", "FloorDetails", "HazardsBack", "PropsBack", "Machines",
            "Characters", "PropsFront", "Effects", "WorldUI", "ScreenUI",
        };

        /// <summary>Adds the game's sorting layers (after Default) if missing.</summary>
        static void EnsureSortingLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null) return;

            foreach (string name in SortingLayerNames)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = name;
                element.FindPropertyRelative("uniqueID").intValue = Mathf.Abs(name.GetHashCode());
                var locked = element.FindPropertyRelative("locked");
                if (locked != null) locked.boolValue = false;
            }
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Log("Sorting layers verified/created (Floor..ScreenUI).");
        }

        // ---------------- folders ----------------

        static readonly string[] Folders =
        {
            "Assets/FactoryGame/Art", "Assets/FactoryGame/Art/Placeholder", "Assets/FactoryGame/Art/Sprites", "Assets/FactoryGame/Art/Materials",
            "Assets/FactoryGame/Audio", "Assets/FactoryGame/Audio/SFX", "Assets/FactoryGame/Audio/Music",
            "Assets/FactoryGame/Documentation",
            "Assets/FactoryGame/Prefabs", "Assets/FactoryGame/Prefabs/Engineer", "Assets/FactoryGame/Prefabs/Machines",
            "Assets/FactoryGame/Prefabs/Hazards", "Assets/FactoryGame/Prefabs/Objectives", "Assets/FactoryGame/Prefabs/UI",
            "Assets/FactoryGame/Scenes",
            "Assets/FactoryGame/Scripts",
            "Assets/FactoryGame/ScriptableObjects", "Assets/FactoryGame/ScriptableObjects/Levels",
            "Assets/FactoryGame/ScriptableObjects/Machines", "Assets/FactoryGame/ScriptableObjects/Engineer",
            "Assets/FactoryGame/ScriptableObjects/Escalation",
            "Assets/FactoryGame/Settings",
        };

        static void CreateFolders()
        {
            foreach (string folder in Folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string leaf = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, leaf);
            }
            Log("Folders verified/created.");
        }

        // ---------------- ScriptableObject assets ----------------

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CreateCoreData()
        {
            engineerData = CreateAsset<EngineerData>("Assets/FactoryGame/ScriptableObjects/Engineer/EngineerData.asset");
            escalationData = CreateAsset<EscalationData>("Assets/FactoryGame/ScriptableObjects/Escalation/EscalationData.asset");
            ResetToScriptDefaults(engineerData);
            ResetToScriptDefaults(escalationData);
            EditorUtility.SetDirty(engineerData);
            EditorUtility.SetDirty(escalationData);
            Log("Engineer/Escalation data assets created/updated to current script defaults.");
        }

        /// <summary>Existing assets keep old serialized values; re-sync them with script defaults.</summary>
        static void ResetToScriptDefaults<T>(T asset) where T : ScriptableObject
        {
            var fresh = ScriptableObject.CreateInstance<T>();
            EditorUtility.CopySerialized(fresh, asset);
            Object.DestroyImmediate(fresh);
        }

        static MachineData CreateMachineData(MachineKind kind, string displayName, string verb, float cooldown)
        {
            var data = CreateAsset<MachineData>($"Assets/FactoryGame/ScriptableObjects/Machines/{kind}Data.asset");
            data.kind = kind;
            data.displayName = displayName;
            data.commandVerb = verb;
            data.cooldown = cooldown;
            EditorUtility.SetDirty(data);
            return data;
        }

        static void CreateLevelData()
        {
            var l1 = CreateAsset<LevelData>("Assets/FactoryGame/ScriptableObjects/Levels/Level_01_RawMilkIntake.asset");
            l1.levelIndex = 0;
            l1.roomName = "ПРИЁМКА СЫРЬЯ";
            l1.sceneName = GameManager.Level1Scene;
            l1.nextSceneName = GameManager.Level2Scene;
            l1.finalLevel = false;
            l1.engineerData = engineerData;
            l1.escalationData = escalationData;
            EditorUtility.SetDirty(l1);

            var l2 = CreateAsset<LevelData>("Assets/FactoryGame/ScriptableObjects/Levels/Level_02_PackagingLine.asset");
            l2.levelIndex = 1;
            l2.roomName = "УПАКОВОЧНАЯ ЛИНИЯ";
            l2.sceneName = GameManager.Level2Scene;
            l2.nextSceneName = GameManager.Level3Scene;
            l2.finalLevel = false;
            l2.engineerData = engineerData;
            l2.escalationData = escalationData;
            EditorUtility.SetDirty(l2);

            var l3 = CreateAsset<LevelData>("Assets/FactoryGame/ScriptableObjects/Levels/Level_03_ColdStorage.asset");
            l3.levelIndex = 2;
            l3.roomName = "ХОЛОДИЛЬНЫЙ СКЛАД";
            l3.sceneName = GameManager.Level3Scene;
            l3.nextSceneName = "";
            l3.finalLevel = true;
            l3.engineerData = engineerData;
            l3.escalationData = escalationData;
            EditorUtility.SetDirty(l3);

            Log("LevelData assets created (3 levels).");
        }

        // ---------------- prefabs ----------------

        static GameObject SavePrefab(GameObject temp, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static void CreatePrefabs()
        {
            library = CreateAsset<PrefabLibrary>("Assets/FactoryGame/ScriptableObjects/PrefabLibrary.asset");

            library.engineer = SavePrefab(PrefabFactories.CreateEngineer(), "Assets/FactoryGame/Prefabs/Engineer/Engineer.prefab");

            library.doorMachine = SaveMachine(MachineKind.Door, "Door", "Close", 8f, "Assets/FactoryGame/Prefabs/Machines/DoorMachine.prefab");
            library.conveyorMachine = SaveMachine(MachineKind.Conveyor, "Conveyor", "Reverse", 6f, "Assets/FactoryGame/Prefabs/Machines/ConveyorMachine.prefab");
            library.roboticArmMachine = SaveMachine(MachineKind.RoboticArm, "Robotic Arm", "Grab", 9f, "Assets/FactoryGame/Prefabs/Machines/RoboticArmMachine.prefab");
            library.mobileUnitMachine = SaveMachine(MachineKind.MobileUnit, "Mobile Unit", "Start Route", 12f, "Assets/FactoryGame/Prefabs/Machines/MobileUnitMachine.prefab");
            library.droneMachine = SaveMachine(MachineKind.Drone, "Drone", "Scan Engineer", 12f, "Assets/FactoryGame/Prefabs/Machines/DroneMachine.prefab");
            library.alarmMachine = SaveMachine(MachineKind.Alarm, "Alarm", "Activate", 14f, "Assets/FactoryGame/Prefabs/Machines/AlarmMachine.prefab");
            library.hazardEmitterMachine = SaveMachine(MachineKind.HazardEmitter, "Hazard Emitter", "Activate", 12f, "Assets/FactoryGame/Prefabs/Machines/HazardEmitterMachine.prefab");

            library.steamHazard = SavePrefab(PrefabFactories.CreateHazard(HazardKind.Steam), "Assets/FactoryGame/Prefabs/Hazards/SteamHazard.prefab");
            library.coldHazard = SavePrefab(PrefabFactories.CreateHazard(HazardKind.Cold), "Assets/FactoryGame/Prefabs/Hazards/ColdHazard.prefab");
            library.slipperyFloor = SavePrefab(PrefabFactories.CreateHazard(HazardKind.Slippery), "Assets/FactoryGame/Prefabs/Hazards/SlipperyFloor.prefab");
            library.dangerZone = SavePrefab(PrefabFactories.CreateHazard(HazardKind.Danger), "Assets/FactoryGame/Prefabs/Hazards/DangerZone.prefab");

            library.repairObjective = SavePrefab(PrefabFactories.CreateRepairObjective(), "Assets/FactoryGame/Prefabs/Objectives/RepairObjective.prefab");
            library.exitDoor = SavePrefab(PrefabFactories.CreateExitDoor(), "Assets/FactoryGame/Prefabs/Objectives/ExitDoor.prefab");

            library.uiRoot = SavePrefab(PrefabFactories.CreateUIRoot(), "Assets/FactoryGame/Prefabs/UI/UIRoot.prefab");

            EditorUtility.SetDirty(library);
            Log("Prefabs created and registered in PrefabLibrary.");
        }

        static GameObject SaveMachine(MachineKind kind, string displayName, string verb, float cooldown, string path)
        {
            MachineData data = CreateMachineData(kind, displayName, verb, cooldown);
            GameObject temp = PrefabFactories.CreateMachine(kind);
            var machine = temp.GetComponent<InteractableMachine>();
            machine.defaultData = data;
            machine.displayName = displayName;
            machine.commandVerb = verb;
            machine.cooldown = cooldown;
            return SavePrefab(temp, path);
        }

        // ---------------- scenes ----------------

        static void CreateScenes()
        {
            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCamera();
            var bootGO = new GameObject("Boot");
            bootGO.AddComponent<BootController>();
            var gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();
            EditorSceneManager.SaveScene(boot, "Assets/FactoryGame/Scenes/Boot.unity");

            CreateLevelScene(GameManager.Level1Scene, "Assets/FactoryGame/ScriptableObjects/Levels/Level_01_RawMilkIntake.asset");
            CreateLevelScene(GameManager.Level2Scene, "Assets/FactoryGame/ScriptableObjects/Levels/Level_02_PackagingLine.asset");
            CreateLevelScene(GameManager.Level3Scene, "Assets/FactoryGame/ScriptableObjects/Levels/Level_03_ColdStorage.asset");
            Log("Scenes created: Boot + 3 levels.");
        }

        static void CreateLevelScene(string sceneName, string levelDataPath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneCamera();
            var rootGO = new GameObject("LevelRoot");
            var lm = rootGO.AddComponent<LevelManager>();
            lm.levelData = AssetDatabase.LoadAssetAtPath<LevelData>(levelDataPath);
            lm.prefabLibrary = library;
            EditorSceneManager.SaveScene(scene, "Assets/FactoryGame/Scenes/" + sceneName + ".unity");
        }

        static void CreateSceneCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.07f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData(); // ensure URP camera data component
        }

        static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/FactoryGame/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/FactoryGame/Scenes/" + GameManager.Level1Scene + ".unity", true),
                new EditorBuildSettingsScene("Assets/FactoryGame/Scenes/" + GameManager.Level2Scene + ".unity", true),
                new EditorBuildSettingsScene("Assets/FactoryGame/Scenes/" + GameManager.Level3Scene + ".unity", true),
            };
            Log("Build Settings updated (Boot first, then Levels 1-3).");
        }

        // ---------------- build log ----------------

        static void Log(string message)
        {
            Directory.CreateDirectory(DocsPath);
            File.AppendAllText(DocsPath + "/BuildLog.txt",
                $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
    }
}
