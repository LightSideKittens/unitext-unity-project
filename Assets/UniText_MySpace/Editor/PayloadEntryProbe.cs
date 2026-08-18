#if UNITY_EDITOR

using System.Text;
using UnityEditor;
using UnityEngine;

static class PayloadEntryProbe
{
    [MenuItem("UniText/Debug/Payload Entry Probe")]
    static void Run()
    {
        var path = EditorUtility.OpenFilePanel("AssetBundle with a UniText payload entry", "", "bundle");
        if (string.IsNullOrEmpty(path)) return;

        const string address = "unitext/fonts/payload/dd28a4bc";
        var report = new StringBuilder();
        var bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            Debug.Log("AssetBundle.LoadFromFile returned null");
            return;
        }

        try
        {
            report.AppendLine($"bundle.name = {bundle.name}");
            var loaded = AssetBundle.GetAllLoadedAssetBundles();
            var listed = false;
            foreach (var candidate in loaded)
                if (candidate == bundle) listed = true;
            report.AppendLine($"listed in GetAllLoadedAssetBundles: {listed}");
            report.AppendLine($"Contains(\"{address}\"): {bundle.Contains(address)}");

            foreach (var name in bundle.GetAllAssetNames())
                report.AppendLine($"  asset name: {name}");

            var payload = bundle.LoadAsset(address);
            report.AppendLine($"LoadAsset(\"{address}\"): {(payload == null ? "null" : payload.GetType().Name)}");
            var all = bundle.LoadAllAssets();
            report.AppendLine($"LoadAllAssets: {all.Length}");
            foreach (var asset in all)
                report.AppendLine($"  object: {asset.GetType().Name} '{asset.name}'");
        }
        finally
        {
            bundle.Unload(true);
        }
        Debug.Log(report.ToString());
    }
}

#endif
