#if UNITY_IOS && UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class iOSPostProcessBuild
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
            return;

        Debug.Log("[iOSPostProcessBuild] Processing iOS build...");

        AddFirebaseGameLoopURLScheme(path);
    }

    private static void AddFirebaseGameLoopURLScheme(string buildPath)
    {
        string plistPath = Path.Combine(buildPath, "Info.plist");

        if (!File.Exists(plistPath))
        {
            Debug.LogError($"[iOSPostProcessBuild] Info.plist not found at: {plistPath}");
            return;
        }

        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementArray urlTypes;
        if (plist.root.values.ContainsKey("CFBundleURLTypes"))
        {
            urlTypes = plist.root["CFBundleURLTypes"].AsArray();
        }
        else
        {
            urlTypes = plist.root.CreateArray("CFBundleURLTypes");
        }

        bool schemeExists = false;
        foreach (var element in urlTypes.values)
        {
            var dict = element.AsDict();
            if (dict != null && dict.values.ContainsKey("CFBundleURLSchemes"))
            {
                var schemes = dict["CFBundleURLSchemes"].AsArray();
                foreach (var scheme in schemes.values)
                {
                    if (scheme.AsString() == "firebase-game-loop")
                    {
                        schemeExists = true;
                        break;
                    }
                }
            }
            if (schemeExists) break;
        }

        if (!schemeExists)
        {
            var urlTypeDict = urlTypes.AddDict();
            urlTypeDict.SetString("CFBundleURLName", "com.firebase.game-loop");
            urlTypeDict.SetString("CFBundleTypeRole", "Editor");  
            var urlSchemes = urlTypeDict.CreateArray("CFBundleURLSchemes");
            urlSchemes.AddString("firebase-game-loop");

            Debug.Log("[iOSPostProcessBuild] Added firebase-game-loop URL scheme with CFBundleTypeRole=Editor");
        }
        else
        {
            Debug.Log("[iOSPostProcessBuild] firebase-game-loop URL scheme already exists");
        }

        plist.WriteToFile(plistPath);
        Debug.Log("[iOSPostProcessBuild] Info.plist updated successfully");
    }
}
#endif
