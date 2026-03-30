using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightSide.Editor
{
    /// <summary>
    /// Intercepts Debug.Log at the ILogHandler level and suppresses messages
    /// from categories the user has disabled in the Log Filter window.
    /// Suppressed messages never reach the Console or Application.logMessageReceived.
    /// Thread-safe: worker threads can log freely without touching EditorPrefs.
    /// </summary>
    [InitializeOnLoad]
    static class LogFilter
    {
        const string PrefsPrefix = "UniText.LogFilter.";
        const string KnownCategoriesKey = PrefsPrefix + "KnownCategories";

        // Thread-safe: read from any thread, written only from main thread (LoadState/SaveState)
        // suppressedSet is a snapshot copy for lock-free reads from worker threads
        static volatile HashSet<string> suppressedSet = new HashSet<string>();
        static readonly HashSet<string> suppressedCategories = new HashSet<string>();

        static readonly List<string> knownCategories = new List<string>();
        static readonly object knownLock = new object();

        // Thread-safe counters
        static readonly ConcurrentDictionary<string, int> categoryCounts = new ConcurrentDictionary<string, int>();

        // New categories discovered on worker threads, flushed to knownCategories on main thread
        static readonly ConcurrentQueue<string> pendingCategories = new ConcurrentQueue<string>();
        static volatile bool hasPending;

        static readonly string[] seedCategories =
        {
            "EmojiFont", "FontSubsetter", "FreeType", "GlyphDiag", "GpuUpload",
            "GradientModifier", "Migration", "UniText", "UniText Timing", "UnicodeData"
        };

        static LogFilter()
        {
            LoadState();
            var original = Debug.unityLogger.logHandler;
            if (!(original is FilteringLogHandler))
                Debug.unityLogger.logHandler = new FilteringLogHandler(original);
            EditorApplication.update += FlushPendingCategories;
        }

        public static IReadOnlyList<string> KnownCategories
        {
            get
            {
                FlushPendingCategories();
                return knownCategories;
            }
        }

        public static int GetCount(string category) =>
            categoryCounts.TryGetValue(category, out var c) ? c : 0;

        public static bool IsSuppressed(string category) => suppressedSet.Contains(category);

        public static void SetSuppressed(string category, bool suppressed)
        {
            bool changed = suppressed
                ? suppressedCategories.Add(category)
                : suppressedCategories.Remove(category);
            if (changed)
            {
                PublishSuppressedSnapshot();
                SaveState();
            }
        }

        public static void EnableAll()
        {
            suppressedCategories.Clear();
            PublishSuppressedSnapshot();
            SaveState();
        }

        public static void SuppressAll()
        {
            lock (knownLock)
            {
                foreach (var cat in knownCategories)
                    suppressedCategories.Add(cat);
            }
            PublishSuppressedSnapshot();
            SaveState();
        }

        public static void ResetCounts() => categoryCounts.Clear();

        /// <summary>
        /// Extracts the category tag from a log message.
        /// Matches [Category] at the start, or PascalCase single-word prefix before ": ".
        /// </summary>
        internal static string ExtractCategory(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;

            // [Category] pattern
            if (message[0] == '[')
            {
                int end = message.IndexOf(']', 1);
                if (end > 1 && end < 60)
                    return message.Substring(1, end - 1);
            }

            // PascalWord: pattern (e.g. "FontSubsetter: error")
            int colon = message.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 1 && colon < 40)
            {
                bool valid = char.IsUpper(message[0]);
                for (int i = 1; valid && i < colon; i++)
                    valid = char.IsLetterOrDigit(message[i]);
                if (valid)
                    return message.Substring(0, colon);
            }

            return null;
        }

        static void RegisterCategory(string category)
        {
            // Fast path: already known (lock-free read via snapshot check)
            lock (knownLock)
            {
                if (knownCategories.Contains(category))
                    return;
            }

            // Queue for main thread processing
            pendingCategories.Enqueue(category);
            hasPending = true;
        }

        static void FlushPendingCategories()
        {
            if (!hasPending) return;

            bool added = false;
            while (pendingCategories.TryDequeue(out var cat))
            {
                lock (knownLock)
                {
                    if (!knownCategories.Contains(cat))
                    {
                        knownCategories.Add(cat);
                        added = true;
                    }
                }
            }
            hasPending = false;

            if (added)
            {
                lock (knownLock)
                    knownCategories.Sort(StringComparer.Ordinal);
                SaveKnownCategories();
            }
        }

        static void PublishSuppressedSnapshot()
        {
            suppressedSet = new HashSet<string>(suppressedCategories);
        }

        static void LoadState()
        {
            knownCategories.Clear();
            suppressedCategories.Clear();

            var saved = EditorPrefs.GetString(KnownCategoriesKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (var cat in saved.Split('|'))
                    if (!string.IsNullOrEmpty(cat) && !knownCategories.Contains(cat))
                        knownCategories.Add(cat);
            }

            foreach (var cat in seedCategories)
                if (!knownCategories.Contains(cat))
                    knownCategories.Add(cat);

            knownCategories.Sort(StringComparer.Ordinal);

            foreach (var cat in knownCategories)
                if (EditorPrefs.GetBool(PrefsPrefix + cat, false))
                    suppressedCategories.Add(cat);

            PublishSuppressedSnapshot();
        }

        static void SaveState()
        {
            foreach (var cat in knownCategories)
                EditorPrefs.SetBool(PrefsPrefix + cat, suppressedCategories.Contains(cat));
            SaveKnownCategories();
        }

        static void SaveKnownCategories()
        {
            lock (knownLock)
                EditorPrefs.SetString(KnownCategoriesKey, string.Join("|", knownCategories));
        }

        class FilteringLogHandler : ILogHandler
        {
            readonly ILogHandler inner;

            public FilteringLogHandler(ILogHandler inner) => this.inner = inner;

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                string message = ResolveMessage(format, args);
                var category = ExtractCategory(message);

                if (category != null)
                {
                    RegisterCategory(category);
                    categoryCounts.AddOrUpdate(category, 1, (_, old) => old + 1);

                    if (suppressedSet.Contains(category))
                        return;
                }

                inner.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, Object context)
            {
                inner.LogException(exception, context);
            }

            static string ResolveMessage(string format, object[] args)
            {
                if (args == null || args.Length == 0)
                    return format ?? "";
                if (args.Length == 1 && format == "{0}")
                    return args[0]?.ToString() ?? "";
                try { return string.Format(format, args); }
                catch { return format ?? ""; }
            }
        }
    }
}
