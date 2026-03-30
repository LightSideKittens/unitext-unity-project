using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Firebase Test Lab support for Android game-loop tests.
/// Based on official Google implementation:
/// https://github.com/googlecodelabs/unity-firebase-test-lab-game-loop
/// </summary>
public static class FirebaseTestLabAndroid
{
#if UNITY_ANDROID && !UNITY_EDITOR
    [DllImport("c")]
    private static extern int dup(int fd);

    [DllImport("c")]
    private static extern int write(int fd, byte[] buf, int count);

    private static AndroidJavaObject activity;
    private static AndroidJavaObject intent;
    private static int logFileDescriptor = -1;
    private static bool initialized;
#endif

    /// <summary>
    /// Initialize Firebase Test Lab integration. Must be called early in app startup.
    /// </summary>
    public static void Initialize()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (initialized) return;
        initialized = true;

        try
        {
            Debug.Log("[FirebaseTestLabAndroid] Initializing...");

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                intent = activity.Call<AndroidJavaObject>("getIntent");
            }

            var action = intent.Call<string>("getAction");
            Debug.Log($"[FirebaseTestLabAndroid] Intent action: {action}");

            if (action != "com.google.intent.action.TEST_LOOP")
            {
                Debug.Log("[FirebaseTestLabAndroid] Not a game-loop test, skipping initialization");
                return;
            }

            var scenario = intent.Call<int>("getIntExtra", "scenario", 0);
            Debug.Log($"[FirebaseTestLabAndroid] Scenario: {scenario}");

            var logFileUri = intent.Call<AndroidJavaObject>("getData");
            if (logFileUri != null)
            {
                var encodedPath = logFileUri.Call<string>("getEncodedPath");
                Debug.Log($"[FirebaseTestLabAndroid] Log file URI: {encodedPath}");

                var fd = activity
                    .Call<AndroidJavaObject>("getContentResolver")
                    .Call<AndroidJavaObject>("openAssetFileDescriptor", logFileUri, "w")
                    .Call<AndroidJavaObject>("getParcelFileDescriptor")
                    .Call<int>("getFd");

                Debug.Log($"[FirebaseTestLabAndroid] Original fd: {fd}");

                logFileDescriptor = dup(fd);
                Debug.Log($"[FirebaseTestLabAndroid] Duplicated fd: {logFileDescriptor}");
            }
            else
            {
                Debug.LogWarning("[FirebaseTestLabAndroid] No log file URI in Intent data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseTestLabAndroid] Initialization failed: {e.Message}\n{e.StackTrace}");
        }
#endif
    }

    /// <summary>
    /// Writes test results to Firebase Test Lab.
    /// Uses the file descriptor from Intent if available, otherwise falls back to local file.
    /// </summary>
    public static void WriteResults(string filename, string content)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var backupDir = Path.Combine(Application.persistentDataPath, "GameLoopResults");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, filename);
            File.WriteAllText(backupPath, content);
            Debug.Log($"[FirebaseTestLabAndroid] Local backup written: {backupPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseTestLabAndroid] Failed to write local backup: {e.Message}");
        }

        if (logFileDescriptor > 0)
        {
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var bytesWritten = write(logFileDescriptor, bytes, bytes.Length);
                Debug.Log($"[FirebaseTestLabAndroid] Written {bytesWritten} bytes to Firebase fd {logFileDescriptor}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FirebaseTestLabAndroid] Failed to write to fd: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[FirebaseTestLabAndroid] No Firebase file descriptor available");
        }
#endif
    }

    /// <summary>
    /// Signals test completion to Firebase Test Lab by finishing the activity.
    /// </summary>
    public static void NotifyTestComplete()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[FirebaseTestLabAndroid] Test complete, finishing activity...");
        try
        {
            activity?.Call("finish");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirebaseTestLabAndroid] Failed to finish activity: {e.Message}");
        }
#endif
    }
}
