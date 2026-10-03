using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace ColorTransit.Editor
{
    public sealed class MonetizationGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var config = AdService.ReadConfiguration();
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            bool offline = Environment.GetEnvironmentVariable("TRANSIT_OFFLINE_RELEASE") == "1";
            if (offline && (config.liveEnabled || config.testEnabled)) throw new BuildFailedException("Offline build cannot request ads.");
            // Account message publication is verified externally, never represented by a hard-coded consent flag.
            // Runtime ad requests must pass UMP CanRequestAds after the adult opt-in.
            if (!offline && !config.testEnabled && (!config.liveEnabled || !Valid(config.androidAppId,'~') || !Valid(config.rewardedUnitId,'/') || !Valid(config.interstitialUnitId,'/'))) throw new BuildFailedException("Live build requires app-specific IDs.");
            if (!dev && config.testEnabled) throw new BuildFailedException("Sample units are forbidden in signed releases.");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings")).First(t => t != null);
            var instance = (UnityEngine.Object)type.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
            var serialized = new SerializedObject(instance);
            serialized.FindProperty("adMobAndroidAppId").stringValue = config.testEnabled || offline ? AdService.TestAppId : config.androidAppId;
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        }
        static bool Valid(string id, char separator) => System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "^ca-app-pub-[0-9]{16}" + (separator == '~' ? "~" : "/") + "[0-9]{10}$") && !id.Contains("3940256099942544");
    }
}
