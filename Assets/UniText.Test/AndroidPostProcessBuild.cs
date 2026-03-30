#if UNITY_ANDROID && UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Adds Firebase Test Lab game-loop intent-filter to AndroidManifest.xml
/// </summary>
public class AndroidPostProcessBuild : IPostGenerateGradleAndroidProject
{
    static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        Debug.Log("[AndroidPostProcessBuild] Processing Android build...");
        AddFirebaseGameLoopIntent(path);
    }

    void AddFirebaseGameLoopIntent(string gradlePath)
    {
        string manifestPath = Path.Combine(gradlePath, "src", "main", "AndroidManifest.xml");

        if (!File.Exists(manifestPath))
        {
            Debug.LogError($"[AndroidPostProcessBuild] AndroidManifest.xml not found at: {manifestPath}");
            return;
        }

        var doc = XDocument.Load(manifestPath);
        var application = doc.Root?.Element("application");
        if (application == null)
        {
            Debug.LogError("[AndroidPostProcessBuild] <application> not found in manifest");
            return;
        }

        var mainActivity = application.Elements("activity")
            .FirstOrDefault(a => a.Elements("intent-filter")
                .Any(f => f.Elements("action")
                    .Any(action => (string)action.Attribute(AndroidNs + "name") == "android.intent.action.MAIN")));

        if (mainActivity == null)
        {
            Debug.LogError("[AndroidPostProcessBuild] Main activity not found");
            return;
        }

        bool alreadyExists = mainActivity.Elements("intent-filter")
            .Any(f => f.Elements("action")
                .Any(a => (string)a.Attribute(AndroidNs + "name") == "com.google.intent.action.TEST_LOOP"));

        if (alreadyExists)
        {
            Debug.Log("[AndroidPostProcessBuild] Firebase game-loop intent already exists");
            return;
        }

        var gameLoopFilter = new XElement("intent-filter",
            new XElement("action", new XAttribute(AndroidNs + "name", "com.google.intent.action.TEST_LOOP")),
            new XElement("category", new XAttribute(AndroidNs + "name", "android.intent.category.DEFAULT")),
            new XElement("data", new XAttribute(AndroidNs + "mimeType", "application/javascript"))
        );

        mainActivity.Add(gameLoopFilter);
        doc.Save(manifestPath);
        Debug.Log("[AndroidPostProcessBuild] Added Firebase game-loop intent-filter");
    }
}
#endif
