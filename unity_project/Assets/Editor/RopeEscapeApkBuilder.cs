using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RopeEscape.Editor
{
    public static class RopeEscapeApkBuilder
    {
        [MenuItem("RopeEscape/Build Android APK")]
        public static void BuildAndroidAPK()
        {
            try
            {
                ConfigureAndroidToolPaths();
                string scenePath = EnsureMainScene();

                PlayerSettings.companyName = "FirstParty";
                PlayerSettings.productName = "Rope Escape Master";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.firstparty.ropeescapemaster");
                PlayerSettings.bundleVersion = "1.0.0";
                PlayerSettings.Android.bundleVersionCode = 1;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(scenePath, true)
                };

                string outputApk = Path.GetFullPath(Path.Combine(Application.dataPath, "../../RopeEscapeMaster_0CDN.apk"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputApk));

                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = outputApk,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                Debug.Log($"[RopeEscapeApkBuilder] Starting Android APK build -> {outputApk}");
                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    Debug.Log($"[RopeEscapeApkBuilder] APK Build SUCCEEDED: {outputApk} ({summary.totalSize} bytes)");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"[RopeEscapeApkBuilder] APK Build FAILED: {summary.result} (Errors: {summary.totalErrors})");
                    if (Application.isBatchMode)
                        EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        private static void ConfigureAndroidToolPaths()
        {
            string editorApp = EditorApplication.applicationPath;
            string editorDir = Path.GetDirectoryName(editorApp);
            if (string.IsNullOrEmpty(editorDir))
                return;

            string androidPlayerDir = Path.Combine(editorDir, "Data", "PlaybackEngines", "AndroidPlayer");
            string sdkPath = Path.Combine(androidPlayerDir, "SDK");
            string ndkPath = Path.Combine(androidPlayerDir, "NDK");
            string jdkPath = Path.Combine(androidPlayerDir, "OpenJDK");

            if (Directory.Exists(sdkPath))
                EditorPrefs.SetString("AndroidSdkRoot", sdkPath);
            if (Directory.Exists(ndkPath))
                EditorPrefs.SetString("AndroidNdkRoot", ndkPath);
            if (Directory.Exists(jdkPath))
                EditorPrefs.SetString("JdkPath", jdkPath);
        }

        private static string EnsureMainScene()
        {
            const string scenesDir = "Assets/Scenes";
            const string scenePath = "Assets/Scenes/MainGame.unity";

            if (!Directory.Exists(scenesDir))
            {
                Directory.CreateDirectory(scenesDir);
            }

            if (!File.Exists(scenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.Refresh();
            }

            return scenePath;
        }
    }
}
