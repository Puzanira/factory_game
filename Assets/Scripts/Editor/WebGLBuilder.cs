using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastShift.EditorTools
{
    /// <summary>
    /// One-click browser build. Publishing settings are applied here rather than left
    /// to Project Settings, so a web build always comes out hostable as plain static
    /// files: Gzip + Decompression Fallback means the loader unpacks the payload in
    /// JavaScript, and the server needs no Content-Encoding headers at all. That costs
    /// a little size and startup time and buys a bucket you can upload to and forget.
    ///
    /// The web build has no serial port, so ArduinoInputBridge never starts there and
    /// the game is keyboard-only (↑/↓/Enter + Esc) — see the UNITY_WEBGL branches in
    /// ArduinoInputBridge, ArduinoControllerReader, SceneLoader and Loc.
    ///
    /// Output goes to &lt;project&gt;/Build/WebGL, which .gitignore already excludes.
    /// Serve it over HTTP to test — a Unity build cannot run from a file:// URL:
    ///     cd Build/WebGL &amp;&amp; python3 -m http.server 8000
    /// </summary>
    public static class WebGLBuilder
    {
        const string DefaultOutput = "Build/WebGL";

        [MenuItem("Tools/Last Shift/Build WebGL")]
        public static void BuildWebGL()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, DefaultOutput);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUtility.DisplayDialog(
                    "Сборка для браузера",
                    "Активная платформа — " + EditorUserBuildSettings.activeBuildTarget +
                    ".\nПереключение на WebGL переимпортирует ассеты, это долго.\n\nПродолжить?",
                    "Переключить и собрать", "Отмена"))
                return;

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[WebGL] Не удалось переключить платформу — установлен ли модуль WebGL Build Support?");
                return;
            }

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGL] В Build Settings нет включённых сцен — запустите Tools ▸ Last Shift ▸ Build All.");
                return;
            }

            Directory.CreateDirectory(outDir);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGL] Готово: {outDir}\n" +
                          $"размер {summary.totalSize / (1024f * 1024f):0.0} МБ, " +
                          $"время {summary.totalTime}.\n" +
                          $"Проверить локально: cd \"{outDir}\" && python3 -m http.server 8000");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(outDir);
            }
            else
            {
                Debug.LogError($"[WebGL] Сборка не удалась: {summary.result}, " +
                               $"ошибок {summary.totalErrors}.");
            }
        }
    }
}
