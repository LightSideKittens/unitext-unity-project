using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;

internal sealed class SlideshowAddressablesBuildProcessor : BuildPlayerProcessor
{
    public override int callbackOrder => 0;

    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        if (!TryGetCIBuild(out var slideshow)) return;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings != null)
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

        if (!slideshow || buildPlayerContext.BuildPlayerOptions.target == BuildTarget.WebGL) return;
        if (settings == null)
            throw new BuildFailedException("Addressables settings are required by the slideshow build");

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (result == null)
            throw new BuildFailedException("Addressables content build returned no result");
        if (!string.IsNullOrEmpty(result.Error))
            throw new BuildFailedException($"Addressables content build failed: {result.Error}");
    }

    private static bool TryGetCIBuild(out bool slideshow)
    {
        slideshow = false;
        var ci = false;
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "-ciDebug":
                case "-ciBenchmark":
                case "-ciTests":
                    ci = true;
                    break;
                case "-ciSlideshow":
                    ci = true;
                    slideshow = args[i + 1] == "true";
                    break;
            }
        }

        return ci;
    }
}
