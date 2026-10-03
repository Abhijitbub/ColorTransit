using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorTransit.Editor
{
    public static class BuildGame
    {
        public static void Setup()
        {
            // Runtime-created UI needs its shaders explicitly retained in player builds.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[] { "UI/Default", "UI/Default Font", "Sprites/Default" })
            {
                var shader = Shader.Find(shaderName);
                bool present = false;
                for (int i = 0; i < shaders.arraySize; i++) if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (!present && shader != null) { int index = shaders.arraySize; shaders.InsertArrayElementAtIndex(index); shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/ColorTransit/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .06f, .105f);
            camera.orthographic = true;
            camera.gameObject.AddComponent<AudioListener>();
            new GameObject("Color Transit Flow").AddComponent<TransitGame>();
            EditorSceneManager.SaveScene(scene, "Assets/ColorTransit/Scenes/Transit.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/ColorTransit/Scenes/Transit.unity", true) };
            PlayerSettings.companyName = "JSCA Games";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.productName = "Color Transit Flow";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.jscagames.colortransitflow");
            PlayerSettings.bundleVersion = "1.1.0";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ColorTransit/Resources/Art/TransitIcon.png");
            if (icon != null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/ColorTransit/Resources/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var setting = importer.defaultSampleSettings;
                setting.loadType = path.Contains("CityAfterglow") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                setting.compressionFormat = AudioCompressionFormat.Vorbis; setting.quality = .72f;
                importer.defaultSampleSettings = setting; importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
        public static void AndroidEmulator() { Android(false, true, true); }
        public static void AndroidPhone() { Android(false, false, false); }
        public static void AndroidRelease() { Android(true, false, false); }
        public static void AndroidAdTest() { Android(false, true, true); }
        public static void AndroidSignedEmulator() { Android(false, true, false); }
        public static void AndroidDeliverables()
        {
            Android(true, false, false);
            Android(false, false, false);
            Android(false, true, false);
        }
        static void Android(bool bundle, bool emulator, bool development)
        {
            Setup(); Directory.CreateDirectory("Builds/Android");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = emulator ? AndroidArchitecture.X86_64 : AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.buildAppBundle = bundle;
            string key = System.Environment.GetEnvironmentVariable("TRANSIT_UPLOAD_KEY_PATH");
            string password = System.Environment.GetEnvironmentVariable("TRANSIT_UPLOAD_KEY_PASSWORD");
            if (!development && (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(password))) throw new System.Exception("Signing credentials must be supplied by Tools/Build-Android.ps1");
            try
            {
                if (!development) {
                    PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = key;
                    PlayerSettings.Android.keyaliasName = "colortransitflow-upload";
                    PlayerSettings.Android.keystorePass = password; PlayerSettings.Android.keyaliasPass = password;
                }
                else PlayerSettings.Android.useCustomKeystore = false;
                string file = bundle ? "ColorTransitFlow-1.1.0-v2.aab" : emulator ? (development ? "ColorTransitFlow-1.1.0-QA-dev-x86.apk" : "ColorTransitFlow-1.1.0-QA-x86.apk") : "ColorTransitFlow-1.1.0.apk";
                var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Android/" + file, BuildTarget.Android, development ? BuildOptions.Development : BuildOptions.None);
                if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Android build failed: " + report.summary.result);
            }
            finally {
                PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = ""; PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }
        }
        public static void Windows()
        {
            Setup();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Windows/ColorTransitFlow.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Build failed: " + report.summary.result);
        }
    }
}
