using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LightSide.Editor
{
    /// <summary>
    /// Captures Unity console output and writes it to Logs/cat.log for external tooling.
    /// Editor-only, zero runtime impact.
    /// </summary>
    [InitializeOnLoad]
    static class ConsoleLogWriter
    {
        const string LogPath = "Logs/cat.log";
        const int MaxSizeBytes = 500 * 1024;          // 500 KB
        const int TrimToBytes = 300 * 1024;            // trim to 300 KB when exceeded
        const int MaxStackFrames = 5;

        static readonly StringBuilder lineBuffer = new StringBuilder(512);

        static ConsoleLogWriter()
        {
            TruncateOnEditorStart();
            Application.logMessageReceived += OnLogMessage;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var separator = $"\n{'═'.Repeat(60)}\n  Play Session {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{'═'.Repeat(60)}\n";
                AppendToFile(separator);
            }
        }

        static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            lineBuffer.Clear();

            var now = DateTime.Now;
            lineBuffer.Append('[');
            lineBuffer.Append(now.Hour.ToString("D2"));
            lineBuffer.Append(':');
            lineBuffer.Append(now.Minute.ToString("D2"));
            lineBuffer.Append(':');
            lineBuffer.Append(now.Second.ToString("D2"));
            lineBuffer.Append('.');
            lineBuffer.Append(now.Millisecond.ToString("D3"));
            lineBuffer.Append("] [");
            lineBuffer.Append(EditorApplication.isPlaying ? "Play" : "Edit");
            lineBuffer.Append("] [");
            lineBuffer.Append(TypeLabel(type));
            lineBuffer.Append("] F");
            lineBuffer.Append(Time.frameCount);
            lineBuffer.Append(' ');
            lineBuffer.Append(message);
            lineBuffer.Append('\n');

            bool includeStack = type == LogType.Error
                             || type == LogType.Exception
                             || type == LogType.Assert;

            if (includeStack && !string.IsNullOrEmpty(stackTrace))
                AppendCompactStack(lineBuffer, stackTrace);

            AppendToFile(lineBuffer.ToString());
        }

        static void AppendCompactStack(StringBuilder sb, string stackTrace)
        {
            int count = 0;
            int start = 0;

            while (start < stackTrace.Length && count < MaxStackFrames)
            {
                int end = stackTrace.IndexOf('\n', start);
                if (end < 0) end = stackTrace.Length;

                var line = stackTrace.Substring(start, end - start);
                start = end + 1;

                // skip Unity/System internals
                if (line.Contains("UnityEngine.") || line.Contains("UnityEditor.") || line.Contains("System."))
                    continue;

                if (line.Length > 0)
                {
                    sb.Append("  → ");
                    sb.Append(line.TrimStart());
                    sb.Append('\n');
                    count++;
                }
            }
        }

        static string TypeLabel(LogType type)
        {
            switch (type)
            {
                case LogType.Log:       return "Log";
                case LogType.Warning:   return "Warn";
                case LogType.Error:     return "Error";
                case LogType.Exception: return "Exception";
                case LogType.Assert:    return "Assert";
                default:                return "?";
            }
        }

        static void AppendToFile(string text)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(LogPath, text, Encoding.UTF8);
                TrimIfNeeded();
            }
            catch
            {
                // never disrupt the editor
            }
        }

        static void TrimIfNeeded()
        {
            try
            {
                var info = new FileInfo(LogPath);
                if (!info.Exists || info.Length <= MaxSizeBytes) return;

                var bytes = File.ReadAllBytes(LogPath);
                int cutFrom = bytes.Length - TrimToBytes;

                // find next newline after cut point so we don't break a line
                while (cutFrom < bytes.Length && bytes[cutFrom] != (byte)'\n')
                    cutFrom++;
                cutFrom++; // skip the newline itself

                if (cutFrom >= bytes.Length) return;

                var header = Encoding.UTF8.GetBytes("[...truncated...]\n");
                var remaining = new byte[header.Length + bytes.Length - cutFrom];
                Buffer.BlockCopy(header, 0, remaining, 0, header.Length);
                Buffer.BlockCopy(bytes, cutFrom, remaining, header.Length, bytes.Length - cutFrom);
                File.WriteAllBytes(LogPath, remaining);
            }
            catch
            {
                // never disrupt the editor
            }
        }

        /// <summary>
        /// Clears the log file once per Editor launch (not per domain reload).
        /// </summary>
        static void TruncateOnEditorStart()
        {
            const string sessionKey = "ConsoleLogWriter_SessionId";
            string currentSession = EditorAnalyticsSessionInfo.id.ToString();

            if (SessionState.GetString(sessionKey, "") == currentSession)
                return;

            SessionState.SetString(sessionKey, currentSession);

            try
            {
                if (File.Exists(LogPath))
                    File.WriteAllText(LogPath, $"[Editor started {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n", Encoding.UTF8);
            }
            catch
            {
                // never disrupt the editor
            }
        }
    }

    static class StringRepeatExtension
    {
        public static string Repeat(this char c, int count) => new string(c, count);
    }
}
