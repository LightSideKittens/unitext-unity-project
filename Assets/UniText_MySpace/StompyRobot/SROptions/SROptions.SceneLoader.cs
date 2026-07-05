using System.ComponentModel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
[Preserve]
public partial class SROptions
{
    [Preserve]
    private int _sceneIndex;

    [Preserve]
    [Category("Scene Loader")]
    [SRDebugger.NumberRange(0, 99)]
    public int SceneIndex
    {
        get => _sceneIndex;
        set
        {
            _sceneIndex = value;
            OnPropertyChanged(nameof(SceneIndex));
            OnPropertyChanged(nameof(SceneName));
        }
    }

    [Preserve]
    [Category("Scene Loader")]
    public string SceneName
    {
        get
        {
            var path = ScenePathAtIndex(_sceneIndex);
            if (path == null) return "(invalid index)";
            var slash = path.LastIndexOf('/');
            var dot = path.LastIndexOf('.');
            if (slash < 0) slash = -1;
            if (dot < 0) dot = path.Length;
            return path.Substring(slash + 1, dot - slash - 1);
        }
    }

    [Preserve]
    [Category("Scene Loader")]
    public void LoadScene()
    {
        if (_sceneIndex < 0 || _sceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneLoader] Invalid scene index: {_sceneIndex}. Build has {SceneManager.sceneCountInBuildSettings} scenes.");
            return;
        }

        Debug.Log($"[SceneLoader] Loading scene {_sceneIndex}: {SceneName}");
        SceneManager.LoadScene(_sceneIndex);
    }

    [Preserve]
    static string ScenePathAtIndex(int index)
    {
        if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
            return null;
        return SceneUtility.GetScenePathByBuildIndex(index);
    }
}
