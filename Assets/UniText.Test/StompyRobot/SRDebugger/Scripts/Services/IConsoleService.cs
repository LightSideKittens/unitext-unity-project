using System;

namespace SRDebugger.Services
{
    using UnityEngine;

    public delegate void ConsoleUpdatedEventHandler(IConsoleService console);

    public interface IConsoleService
    {
        int ErrorCount { get; }
        int WarningCount { get; }
        int InfoCount { get; }

                IReadOnlyList<ConsoleEntry> Entries { get; }

                IReadOnlyList<ConsoleEntry> AllEntries { get; }

        event ConsoleUpdatedEventHandler Updated;

        event ConsoleUpdatedEventHandler Error;

        void Clear();
    }

    public class ConsoleEntry
    {
        private const int MessagePreviewLength = 180;
        private const int StackTracePreviewLength = 120;
        private string _messagePreview;
        private string _stackTracePreview;

                public int Count = 1;

        public LogType LogType;
        public int Frame;
        public float TimeSinceStartUp;
        public string Message;
        public string StackTrace;

        public ConsoleEntry()
        {
#if UNITY_EDITOR
            try
            {
                Frame = Time.frameCount;
                TimeSinceStartUp = Time.realtimeSinceStartup;
            }
            catch (Exception) { }
#else
            Frame = Time.frameCount;
            TimeSinceStartUp = Time.realtimeSinceStartup;
#endif
        }

        public ConsoleEntry(ConsoleEntry other)
        {
            Message = other.Message;
            StackTrace = other.StackTrace;
            LogType = other.LogType;
            Count = other.Count;
        }

        public string MessagePreview
        {
            get
            {
                if (_messagePreview != null)
                {
                    return _messagePreview;
                }
                if (string.IsNullOrEmpty(Message))
                {
                    return "";
                }

                _messagePreview = Message.Split('\n')[0];
                _messagePreview = _messagePreview.Substring(0, Mathf.Min(_messagePreview.Length, MessagePreviewLength));

                return _messagePreview;
            }
        }

        public string StackTracePreview
        {
            get
            {
                if (_stackTracePreview != null)
                {
                    return _stackTracePreview;
                }
                if (string.IsNullOrEmpty(StackTrace))
                {
                    return "";
                }

                _stackTracePreview = StackTrace.Split('\n')[0];
                _stackTracePreview = _stackTracePreview.Substring(0,
                    Mathf.Min(_stackTracePreview.Length, StackTracePreviewLength));

                return _stackTracePreview;
            }
        }

        public bool Matches(ConsoleEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return string.Equals(Message, other.Message) && string.Equals(StackTrace, other.StackTrace) &&
                   LogType == other.LogType;
        }
    }
}
