using UnityEngine;

namespace SRDebugger
{
    public delegate void VisibilityChangedDelegate(bool isVisible);

    public delegate void ActionCompleteCallback(bool success);

    public delegate void PinnedUiCanvasCreated(RectTransform canvasTransform);
}

namespace SRDebugger.Services
{
    using UnityEngine;

    public interface IDebugService
    {
                Settings Settings { get; }

                bool IsDebugPanelVisible { get; }

                bool IsTriggerEnabled { get; set; }

        IDockConsoleService DockConsole { get; }

        bool IsProfilerDocked { get; set; }

                /// <param name="entry">The entry to be added.</param>
        /// <param name="category">The category the entry should be added to.</param>
        void AddSystemInfo(InfoEntry entry, string category = "Default");

                /// <param name="requireEntryCode">
        /// If true and entry code is enabled in settings, the user will be prompted for a passcode
        /// before opening the panel.
        /// </param>
        void ShowDebugPanel(bool requireEntryCode = true);

                /// <param name="tab">Tab that will appear when the debug panel is opened</param>
        /// <param name="requireEntryCode">
        /// If true and entry code is enabled in settings, the user will be prompted for a passcode
        /// before opening the panel.
        /// </param>
        void ShowDebugPanel(DefaultTabs tab, bool requireEntryCode = true);

                void HideDebugPanel();

                void DestroyDebugPanel();

                /// <param name="container">The object to add.</param>
        void AddOptionContainer(object container);
        
                /// <param name="container">The container to remove.</param>
        void RemoveOptionContainer(object container);

                /// <param name="category"></param>
        void PinAllOptions(string category);

                /// <param name="category"></param>
        void UnpinAllOptions(string category);

        void PinOption(string name);

        void UnpinOption(string name);

                void ClearPinnedOptions();

                /// <param name="onComplete">Callback to invoke once the bug report is completed or cancelled. Null to ignore.</param>
        /// <param name="takeScreenshot">
        /// Take a screenshot before opening the report sheet (otherwise a screenshot will be taken as
        /// the report is sent)
        /// </param>
        /// <param name="descriptionContent">Initial content of the bug report description</param>
        void ShowBugReportSheet(ActionCompleteCallback onComplete = null, bool takeScreenshot = true,
            string descriptionContent = null);

                event VisibilityChangedDelegate PanelVisibilityChanged;

        event PinnedUiCanvasCreated PinnedUiCanvasCreated;

                /// <returns>The debug panel RectTransform.</returns>
        RectTransform EnableWorldSpaceMode();
    }
}
