namespace SRDebugger.Services
{
    using System;

    public interface IDebugPanelService
    {
                bool IsLoaded { get; }

                bool IsVisible { get; set; }

                DefaultTabs? ActiveTab { get; }

        event Action<IDebugPanelService, bool> VisibilityChanged;

                void Unload();

                /// <param name="tab"></param>
        void OpenTab(DefaultTabs tab);
    }
}
