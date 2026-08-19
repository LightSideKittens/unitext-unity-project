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

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern IntPtr UniTextTestScreenshot_CaptureWindow(out int length);

    [DllImport("__Internal")]
    private static extern void UniTextTestScreenshot_FreeWindowCapture(IntPtr buffer);
#endif

    private static Camera screenshotCamera;
    private static RenderTexture renderTexture;

    /// <summary>
    /// Re-renders one camera offscreen at no less than 1920x1080 and stores the result for test
    /// artifacts. Nothing outside that camera reaches the image — screen-space-overlay canvases,
    /// other cameras and OS-owned layers such as the soft keyboard are absent.
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

        Save(name, pngBytes);
    }

    /// <summary>
    /// Captures the app's own UIKit window, which contains the OS-drawn views UniText overlays on
    /// the Unity view — the native input field and its presenter surface. The camera render in
    /// <see cref="Capture"/> cannot reach them. The soft keyboard is a separate system window and
    /// is never included. Does nothing off iOS.
    /// </summary>
    /// <param name="name">Screenshot name (without extension)</param>
    public static void CaptureWindow(string name)
    {
#if UNITY_IOS && !UNITY_EDITOR
        var buffer = UniTextTestScreenshot_CaptureWindow(out var length);
        if (buffer == IntPtr.Zero || length <= 0)
        {
            Debug.LogWarning($"[TestScreenshot] Window capture produced no image: {name}");
            return;
        }

        try
        {
            var pngBytes = new byte[length];
            Marshal.Copy(buffer, pngBytes, 0, length);
            Save(name, pngBytes);
        }
        finally
        {
            UniTextTestScreenshot_FreeWindowCapture(buffer);
        }
#endif
    }

    /// <summary>Routes an already-encoded PNG through the platform artifact channel (persistentDataPath/Screenshots, the WebGL JS bridge, the iOS game-loop results dir) — the single save path shared by test captures and benchmark captures.</summary>
    public static void Save(string name, byte[] pngBytes)
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
#if UNITY_IOS
        FirebaseTestLabiOS.WriteResults($"{name}.png", pngBytes);
#endif
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
