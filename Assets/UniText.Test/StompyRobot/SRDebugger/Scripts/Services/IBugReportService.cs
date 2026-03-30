namespace SRDebugger.Services
{
    using System.Collections.Generic;

    public class BugReport
    {
        public List<ConsoleEntry> ConsoleLog;
        public string Email;
        public byte[] ScreenshotData;
        public Dictionary<string, Dictionary<string, object>> SystemInformation;
        public string UserDescription;
    }

    public delegate void BugReportCompleteCallback(bool didSucceed, string errorMessage);

    public delegate void BugReportProgressCallback(float progress);

    public interface IBugReportService
    {
                /// <param name="report">Bug report to send</param>
        /// <param name="completeHandler">Delegate to call once bug report is submitted successfully</param>
        /// <param name="progressCallback">Optionally provide a callback for when progress % is known</param>
        void SendBugReport(BugReport report, BugReportCompleteCallback completeHandler,
            BugReportProgressCallback progressCallback = null);
    }
}
