using UnityEditor;
using UnityEngine;
using TMPro;

static class TMP_FontAssetCleaner
{
    [MenuItem("Assets/Clear TMP Dynamic Data", true)]
    static bool Validate()
    {
        foreach (var obj in Selection.objects)
            if (obj is TMP_FontAsset) return true;
        return false;
    }

    [MenuItem("Assets/Clear TMP Dynamic Data", false, 30)]
    static void Execute()
    {
        var fonts = Selection.GetFiltered<TMP_FontAsset>(SelectionMode.Assets);
        if (fonts.Length == 0) return;

        foreach (var font in fonts)
        {
            font.ClearFontAssetData(false);
            EditorUtility.SetDirty(font);
            Debug.Log($"[TMP Cleaner] Cleared dynamic data: {font.name}");
        }

        AssetDatabase.SaveAssets();
    }
}
