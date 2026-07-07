using LightSide;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class UIToolkitBenchmark : TextBenchmarkBase<Label>
{
    [Header("UI Toolkit")]
    public PanelRenderer panelRenderer;
    public PanelSettings panelSettings;

    [Tooltip("UXML template containing a Label to clone")]
    public VisualTreeAsset labelTemplate;

    public override string SystemName => "UIToolkit";

    /// <summary>uGUI benchmark rect width — UITK labels must wrap at the same width or they lay out a different line count than the other engines.</summary>
    const float UGuiRectWidth = 1068.9f;

    VisualElement container;
    VisualElement root;
    bool reloadHooked;

    private void Start() => EnsurePanelRenderer();

    private void EnsurePanelRenderer()
    {
        if (panelRenderer == null)
        {
            panelRenderer = GetComponent<PanelRenderer>();
            if (panelRenderer == null)
                panelRenderer = gameObject.AddComponent<PanelRenderer>();
        }

        if (panelSettings != null && panelRenderer.panelSettings == null)
            panelRenderer.panelSettings = panelSettings;

        if (!reloadHooked)
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
            reloadHooked = true;
        }
    }

    /// <summary>The reload callback is PanelRenderer's only public path to the root element; the root is rebuilt on every UI reload, so a live container re-attaches here.</summary>
    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
    {
        root = rootElement;
        if (container != null)
            rootElement.Add(container);
    }

    private void OnDestroy()
    {
        if (reloadHooked && panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    protected override void OnBeforeAllTests() { }
    protected override void OnAfterAllTests() { }

    protected override bool ValidateSetup()
    {
        EnsurePanelRenderer();
        if (panelRenderer != null && panelRenderer.panelSettings != null) return true;
        Debug.LogError("PanelRenderer or PanelSettings not assigned!");
        return false;
    }

    protected override void SetupContainer()
    {
        container = new VisualElement { name = "BenchmarkContainer" };
        container.style.position = Position.Absolute;
        container.style.width = Length.Percent(100);
        container.style.height = Length.Percent(100);
        root?.Add(container);
    }

    protected override void TeardownContainer()
    {
        if (container != null)
        {
            container.RemoveFromHierarchy();
            container = null;
        }
    }

    protected override Label CreateInstance(int index)
    {
        Label label = null;
        if (labelTemplate != null)
        {
            label = labelTemplate.CloneTree().Q<Label>();
            if (label == null)
                Debug.LogWarning("No Label found in UXML template, using fallback");
        }

        label ??= new Label();
        label.style.position = Position.Absolute;
        label.style.left = 10;
        label.style.top = 10;
        label.style.width = UGuiRectWidth;
        container.Add(label);
        return label;
    }

    protected override void DestroyInstance(Label instance) => instance?.RemoveFromHierarchy();

    protected override void SetText(Label instance, string text) => instance.text = text;
    protected override void SetFontSize(Label instance, float size) => instance.style.fontSize = new Length(size, LengthUnit.Pixel);
    protected override void SetColor(Label instance, Color color) => instance.style.color = new StyleColor(color);
    protected override void SetWordWrap(Label instance, bool enabled) => instance.style.whiteSpace = enabled ? WhiteSpace.Normal : WhiteSpace.NoWrap;
    protected override void SetRichText(Label instance, bool enabled) => instance.enableRichText = enabled;

    protected override void SetAutoSize(Label instance, bool enabled)
    {
#if UNITY_6000_0_OR_NEWER
        instance.style.unityTextAutoSize = enabled
            ? new StyleTextAutoSize(new TextAutoSize(TextAutoSizeMode.BestFit, minSize: 10, maxSize: 72))
            : new StyleTextAutoSize(new TextAutoSize(TextAutoSizeMode.None, 28, 28));
#endif
    }

    protected override void SetRectSize(Label instance, float width, float height)
    {
        instance.style.width = new Length(width, LengthUnit.Pixel);
        instance.style.height = new Length(height, LengthUnit.Pixel);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run UIToolkit Benchmark")]
    private static void RunFromMenu()
    {
        var test = ObjectUtils.FindAny<UIToolkitBenchmark>();
        if (test != null)
            test.RunBenchmark();
        else
            Debug.LogError("No UIToolkitBenchmark found in scene. Add UIToolkitBenchmark component to a GameObject with PanelRenderer.");
    }
#endif
}
