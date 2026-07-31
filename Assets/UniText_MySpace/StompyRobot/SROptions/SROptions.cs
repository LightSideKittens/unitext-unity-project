using System.ComponentModel;
using UnityEngine;

public delegate void SROptionsPropertyChanged(object sender, string propertyName);

public partial class SROptions : INotifyPropertyChanged
{
    private static readonly SROptions _current = new SROptions();

    public static SROptions Current
    {
        get { return _current; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void OnStartup()
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_6000_4_OR_NEWER
        Debug.Log("[WebGL Startup] SROptions initializer entered");
#endif
        SRDebug.Instance.AddOptionContainer(Current);
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_6000_4_OR_NEWER
        Debug.Log("[WebGL Startup] SROptions initializer completed");
#endif
    }

    public event SROptionsPropertyChanged PropertyChanged;
    
#if UNITY_EDITOR
    [JetBrains.Annotations.NotifyPropertyChangedInvocator]
#endif
    public void OnPropertyChanged(string propertyName)
    {
        if (PropertyChanged != null)
        {
            PropertyChanged(this, propertyName);
        }

        if (InterfacePropertyChangedEventHandler != null)
        {
            InterfacePropertyChangedEventHandler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private event PropertyChangedEventHandler InterfacePropertyChangedEventHandler;

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add { InterfacePropertyChangedEventHandler += value; }
        remove { InterfacePropertyChangedEventHandler -= value; }
    }
}
