#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.Text;

static class FontAssetCleaner
{
    [MenuItem("Assets/Clear Dynamic Data", true)]
    static bool Validate()
    {
        foreach (var obj in Selection.objects)
            if (obj is TMP_FontAsset or FontAsset) return true;
        return false;
    }

    [MenuItem("Assets/Clear Dynamic Data", false, 30)]
    static void Execute()
    {
        var fonts = Selection.GetFiltered<TMP_FontAsset>(SelectionMode.Assets);
        if (fonts.Length == 0) return;

        foreach (var font in fonts)
        {
            font.ClearFontAssetData(false);
            EditorUtility.SetDirty(font);
            Debug.Log($"[Cleaner] Cleared dynamic data: {font.name}");
        }
        
        var fonts2 = Selection.GetFiltered<FontAsset>(SelectionMode.Assets);
        if (fonts2.Length == 0) return;

        foreach (var font in fonts2)
        {
            font.ClearFontAssetData(false);
            EditorUtility.SetDirty(font);
            Debug.Log($"[Cleaner] Cleared dynamic data: {font.name}");
        }

        AssetDatabase.SaveAssets();
    }
}

#endif