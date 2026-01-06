using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Cross-platform screenshot capture for tests.
/// On WebGL: sends base64 PNG to JavaScript for Playwright to collect.
/// On other platforms: saves PNG to persistentDataPath.
/// </summary>
public static class TestScreenshot
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AddTestScreenshot(string name, string base64);
#endif

    private static Camera screenshotCamera;
    private static RenderTexture renderTexture;

    /// <summary>
    /// Captures a screenshot and stores it for test artifacts.
    /// </summary>
    /// <param name="name">Screenshot name (without extension)</param>
    /// <param name="camera">Camera to render from. If null, uses Camera.main</param>
    public static void Capture(string name, Camera camera = null)
    {
        if (camera == null)
            camera = Camera.main;

        if (camera == null)
        {
            Debug.LogWarning($"[TestScreenshot] No camera available for screenshot: {name}");
            return;
        }

        int width = Screen.width;
        int height = Screen.height;

        bool isPortrait = height > width || Screen.orientation == ScreenOrientation.Portrait
                                         || Screen.orientation == ScreenOrientation.PortraitUpsideDown;

        int minLong = 1920;
        int minShort = 1080;

        int minWidth = isPortrait ? minShort : minLong;
        int minHeight = isPortrait ? minLong : minShort;

        if (width < minWidth || height < minHeight)
        {
            width = minWidth;
            height = minHeight;
        }

        if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
        {
            if (renderTexture != null)
                RenderTexture.ReleaseTemporary(renderTexture);

            renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        }

        var prevTarget = camera.targetTexture;
        var prevActive = RenderTexture.active;

        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;

        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        camera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        var pngBytes = texture.EncodeToPNG();
        UnityEngine.Object.Destroy(texture);

        SaveScreenshot(name, pngBytes);
    }

    private static void SaveScreenshot(string name, byte[] pngBytes)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var base64 = Convert.ToBase64String(pngBytes);
        AddTestScreenshot(name, base64);
        Debug.Log($"[TestScreenshot] Sent to JS: {name} ({pngBytes.Length} bytes)");
#else
        var dir = Path.Combine(Application.persistentDataPath, "Screenshots");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{name}.png");
        File.WriteAllBytes(path, pngBytes);
        Debug.Log($"[TestScreenshot] Saved: {path}");
#endif
    }

    /// <summary>
    /// Releases temporary render texture resources.
    /// </summary>
    public static void Cleanup()
    {
        if (renderTexture != null)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
            renderTexture = null;
        }
    }
}
