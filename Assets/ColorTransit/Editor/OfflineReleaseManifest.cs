using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace ColorTransit.Editor
{
    // Apply at the highest-priority launcher manifest so transitive SDK permissions cannot reappear.
    public sealed class OfflineReleaseManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            bool offline = Environment.GetEnvironmentVariable("TRANSIT_OFFLINE_RELEASE") == "1";
            var file = Path.GetFullPath(Path.Combine(path, "../launcher/src/main/AndroidManifest.xml"));
            var doc = new XmlDocument(); doc.Load(file);
            const string android = "http://schemas.android.com/apk/res/android";
            const string tools = "http://schemas.android.com/tools";
            var manifest = doc.DocumentElement;
            if (offline) foreach (var permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE",
                "com.google.android.gms.permission.AD_ID", "android.permission.ACCESS_ADSERVICES_AD_ID",
                "android.permission.ACCESS_ADSERVICES_ATTRIBUTION", "android.permission.ACCESS_ADSERVICES_TOPICS" })
            {
                var node = doc.CreateElement("uses-permission");
                node.SetAttribute("name", android, permission); node.SetAttribute("node", tools, "remove");
                manifest.AppendChild(node);
            }
            var app = (XmlElement)manifest.SelectSingleNode("application");
            app.SetAttribute("allowBackup", android, "false");
            app.SetAttribute("fullBackupContent", android, "false");
            app.SetAttribute("replace", tools, "android:allowBackup,android:fullBackupContent");
            var provider = doc.CreateElement("provider");
            provider.SetAttribute("name", android, "com.google.android.gms.ads.MobileAdsInitProvider");
            provider.SetAttribute("node", tools, "remove"); app.AppendChild(provider);
            doc.Save(file);
        }
    }
}

