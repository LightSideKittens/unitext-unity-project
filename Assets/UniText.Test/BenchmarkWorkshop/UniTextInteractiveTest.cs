using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;

public class UniTextInteractiveTest : MonoBehaviour
{
    [Header("Target")] [SerializeField] private UniText target;

    [Header("Text Tests")] [SerializeField] [TextArea(5, 10)]
    private string testText = "Hello World!";

    [SerializeField] [TextArea(5, 10)] private string rtlText = "مرحبا بالعالم";
    [SerializeField] [TextArea(5, 10)] private string mixedText = "Hello مرحبا World عالم";

    [SerializeField] [TextArea(5, 10)] private string longText =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

    [Header("Font Tests")] [SerializeField]
    private UniTextFontStack alternateFontStack;

    [SerializeField] private float testFontSize = 24f;

    [Header("Color Tests")] [SerializeField]
    private Color testColor = Color.red;

    [SerializeReference] public List<ModRegister> modifiers = new();

    private UniTextFontStack originalFontStack;
    private float originalFontSize;
    private Color originalColor;
    private string originalText;

    private void Reset()
    {
        target = GetComponent<UniText>();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponent<UniText>();
    }

    private void SaveOriginalState()
    {
        if (target == null) return;
        originalFontStack = target.FontStack;
        originalFontSize = target.FontSize;
        originalColor = target.color;
        originalText = target.Text;
    }

    #region Text Tests

    [ContextMenu("Text/Set Test Text")]
    public void SetTestText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = testText;
        Log($"Text set to: \"{testText}\"");
    }

    [ContextMenu("Text/Set RTL Text")]
    public void SetRTLText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = rtlText;
        Log($"Text set to RTL: \"{rtlText}\"");
    }

    [ContextMenu("Text/Set Mixed Text")]
    public void SetMixedText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = mixedText;
        Log($"Text set to mixed: \"{mixedText}\"");
    }

    [ContextMenu("Text/Set Long Text")]
    public void SetLongText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = longText;
        Log($"Text set to long text ({longText.Length} chars)");
    }

    [ContextMenu("Text/Set Empty Text")]
    public void SetEmptyText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = "";
        Log("Text set to empty string");
    }

    [ContextMenu("Text/Set Null Text")]
    public void SetNullText()
    {
        if (target == null) return;
        SaveOriginalState();
        target.Text = null;
        Log("Text set to null");
    }

    [ContextMenu("Text/Animate Text (10 frames)")]
    public void AnimateText()
    {
        if (target == null) return;
        StartCoroutine(AnimateTextCoroutine());
    }

    private static readonly string[] BenchmarkTexts =
    {
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "مرحبا بالعالم. هذا نص تجريبي باللغة العربية لاختبار دعم اللغات من اليمين إلى اليسار. مرحبا بالعالم. هذا نص تجريبي باللغة العربية لاختبار دعم اللغات من اليمين إلى اليسار. مرحبا بالعالم. هذا نص تجريبي باللغة العربية لاختبار دعم اللغات من اليمين إلى اليسار. مرحبا بالعالم. هذا نص تجريبي باللغة العربية لاختبار دعم اللغات من اليمين إلى اليسار.",
        "שלום עולם. זהו טקסט לדוגמה בעברית לבדיקת תמיכה בשפות מימין לשמאל. שלום עולם. זהו טקסט לדוגמה בעברית לבדיקת תמיכה בשפות מימין לשמאל. שלום עולם. זהו טקסט לדוגמה בעברית לבדיקת תמיכה בשפות מימין לשמאל. שלום עולם. זהו טקסט לדוגמה בעברית לבדיקת תמיכה בשפות מימין לשמאל.",
        "Hello مرحبا World שלום 2024! Mixed text with numbers 12345. Hello مرحبا World שלום 2024! Mixed text with numbers 12345. Hello مرحبا World שלום 2024! Mixed text with numbers 12345. Hello مرحبا World שלום 2024! Mixed text with numbers 12345. Hello مرحبا World שלום 2024! Mixed text with numbers 12345."
    };

    private IEnumerator AnimateTextCoroutine()
    {
        Log("Text animation started (10 frames)");
        var originalText = target.Text;

        for (int i = 0; i < 10; i++)
        {
            target.Text = BenchmarkTexts[i % BenchmarkTexts.Length];
            yield return null;
        }

        target.Text = originalText;
        Log("Text animation completed");
    }

    [ContextMenu("Text/Toggle Empty-NonEmpty")]
    public void ToggleEmptyNonEmpty()
    {
        if (target == null) return;
        if (string.IsNullOrEmpty(target.Text))
        {
            target.Text = testText;
            Log($"Text restored: \"{testText}\"");
        }
        else
        {
            target.Text = "";
            Log("Text cleared");
        }
    }

    #endregion

    #region Font Tests

    [ContextMenu("Font/Set Alternate Font")]
    public void SetAlternateFont()
    {
        if (target == null || alternateFontStack == null) return;
        SaveOriginalState();
        target.FontStack = alternateFontStack;
        Log($"Fonts changed to: {alternateFontStack.name}");
    }

    [ContextMenu("Font/Restore Original Font")]
    public void RestoreOriginalFont()
    {
        if (target == null || originalFontStack == null) return;
        target.FontStack = originalFontStack;
        Log($"Fonts restored to: {originalFontStack.name}");
    }

    [ContextMenu("Font/Set Test Font Size")]
    public void SetTestFontSize()
    {
        if (target == null) return;
        SaveOriginalState();
        target.FontSize = testFontSize;
        Log($"FontSize set to: {testFontSize}");
    }

    [ContextMenu("Font/Increase Font Size")]
    public void IncreaseFontSize()
    {
        if (target == null) return;
        target.FontSize += 4f;
        Log($"FontSize increased to: {target.FontSize}");
    }

    [ContextMenu("Font/Decrease Font Size")]
    public void DecreaseFontSize()
    {
        if (target == null) return;
        target.FontSize = Mathf.Max(1f, target.FontSize - 4f);
        Log($"FontSize decreased to: {target.FontSize}");
    }

    [ContextMenu("Font/Animate FontSize (10 frames)")]
    public void AnimateFontSize()
    {
        if (target == null) return;
        StartCoroutine(AnimateFontSizeCoroutine());
    }

    private IEnumerator AnimateFontSizeCoroutine()
    {
        Log("FontSize animation started (10 frames)");
        var baseSize = target.FontSize;

        for (int i = 0; i < 10; i++)
        {
            target.FontSize = baseSize + (i % 2 == 0 ? 4f : -4f);
            yield return null;
        }

        target.FontSize = baseSize;
        Log("FontSize animation completed");
    }

    #endregion

    #region Direction Tests

    [ContextMenu("Direction/Set Auto")]
    public void SetDirectionAuto()
    {
        if (target == null) return;
        target.BaseDirection = TextDirection.Auto;
        Log("BaseDirection set to Auto");
    }

    [ContextMenu("Direction/Set LTR")]
    public void SetDirectionLTR()
    {
        if (target == null) return;
        target.BaseDirection = TextDirection.LeftToRight;
        Log("BaseDirection set to LeftToRight");
    }

    [ContextMenu("Direction/Set RTL")]
    public void SetDirectionRTL()
    {
        if (target == null) return;
        target.BaseDirection = TextDirection.RightToLeft;
        Log("BaseDirection set to RightToLeft");
    }

    [ContextMenu("Direction/Cycle Direction")]
    public void CycleDirection()
    {
        if (target == null) return;
        target.BaseDirection = (TextDirection)(((int)target.BaseDirection + 1) % 3);
        Log($"BaseDirection cycled to: {target.BaseDirection}");
    }

    #endregion

    #region Word Wrap Tests

    [ContextMenu("WordWrap/Enable")]
    public void EnableWordWrap()
    {
        if (target == null) return;
        target.WordWrap = true;
        Log("WordWrap enabled");
    }

    [ContextMenu("WordWrap/Disable")]
    public void DisableWordWrap()
    {
        if (target == null) return;
        target.WordWrap = false;
        Log("WordWrap disabled");
    }

    [ContextMenu("WordWrap/Toggle")]
    public void ToggleWordWrap()
    {
        if (target == null) return;
        target.WordWrap = !target.WordWrap;
        Log($"WordWrap toggled to: {target.WordWrap}");
    }

    #endregion

    #region Horizontal Alignment Tests

    [ContextMenu("HAlign/Set Left")]
    public void SetHAlignLeft()
    {
        if (target == null) return;
        target.HorizontalAlignment = HorizontalAlignment.Left;
        Log("HorizontalAlignment set to Left");
    }

    [ContextMenu("HAlign/Set Center")]
    public void SetHAlignCenter()
    {
        if (target == null) return;
        target.HorizontalAlignment = HorizontalAlignment.Center;
        Log("HorizontalAlignment set to Center");
    }

    [ContextMenu("HAlign/Set Right")]
    public void SetHAlignRight()
    {
        if (target == null) return;
        target.HorizontalAlignment = HorizontalAlignment.Right;
        Log("HorizontalAlignment set to Right");
    }

    [ContextMenu("HAlign/Cycle")]
    public void CycleHAlign()
    {
        if (target == null) return;
        target.HorizontalAlignment = (HorizontalAlignment)(((int)target.HorizontalAlignment + 1) % 3);
        Log($"HorizontalAlignment cycled to: {target.HorizontalAlignment}");
    }

    #endregion

    #region Vertical Alignment Tests

    [ContextMenu("VAlign/Set Top")]
    public void SetVAlignTop()
    {
        if (target == null) return;
        target.VerticalAlignment = VerticalAlignment.Top;
        Log("VerticalAlignment set to Top");
    }

    [ContextMenu("VAlign/Set Middle")]
    public void SetVAlignMiddle()
    {
        if (target == null) return;
        target.VerticalAlignment = VerticalAlignment.Middle;
        Log("VerticalAlignment set to Middle");
    }

    [ContextMenu("VAlign/Set Bottom")]
    public void SetVAlignBottom()
    {
        if (target == null) return;
        target.VerticalAlignment = VerticalAlignment.Bottom;
        Log("VerticalAlignment set to Bottom");
    }

    [ContextMenu("VAlign/Cycle")]
    public void CycleVAlign()
    {
        if (target == null) return;
        target.VerticalAlignment = (VerticalAlignment)(((int)target.VerticalAlignment + 1) % 3);
        Log($"VerticalAlignment cycled to: {target.VerticalAlignment}");
    }

    #endregion

    #region Color Tests

    [ContextMenu("Color/Set Test Color")]
    public void SetTestColor()
    {
        if (target == null) return;
        SaveOriginalState();
        target.color = testColor;
        Log($"Color set to: {testColor}");
    }

    [ContextMenu("Color/Set Red")]
    public void SetColorRed()
    {
        if (target == null) return;
        target.color = Color.red;
        Log("Color set to Red");
    }

    [ContextMenu("Color/Set Green")]
    public void SetColorGreen()
    {
        if (target == null) return;
        target.color = Color.green;
        Log("Color set to Green");
    }

    [ContextMenu("Color/Set Blue")]
    public void SetColorBlue()
    {
        if (target == null) return;
        target.color = Color.blue;
        Log("Color set to Blue");
    }

    [ContextMenu("Color/Set White")]
    public void SetColorWhite()
    {
        if (target == null) return;
        target.color = Color.white;
        Log("Color set to White");
    }

    [ContextMenu("Color/Set Random")]
    public void SetColorRandom()
    {
        if (target == null) return;
        target.color = Random.ColorHSV();
        Log($"Color set to random: {target.color}");
    }

    [ContextMenu("Color/Fade Alpha 50%")]
    public void FadeAlpha50()
    {
        if (target == null) return;
        var c = target.color;
        c.a = 0.5f;
        target.color = c;
        Log("Alpha set to 50%");
    }

    #endregion

    #region Component Lifecycle Tests

    [ContextMenu("Lifecycle/Disable Component")]
    public void DisableComponent()
    {
        if (target == null) return;
        target.enabled = false;
        Log("Component disabled");
    }

    [ContextMenu("Lifecycle/Enable Component")]
    public void EnableComponent()
    {
        if (target == null) return;
        target.enabled = true;
        Log("Component enabled");
    }

    [ContextMenu("Lifecycle/Toggle Component")]
    public void ToggleComponent()
    {
        if (target == null) return;
        target.enabled = !target.enabled;
        Log($"Component toggled to: {target.enabled}");
    }

    [ContextMenu("Lifecycle/Deactivate GameObject")]
    public void DeactivateGameObject()
    {
        if (target == null) return;
        target.gameObject.SetActive(false);
        Log("GameObject deactivated");
    }

    [ContextMenu("Lifecycle/Activate GameObject")]
    public void ActivateGameObject()
    {
        if (target == null) return;
        target.gameObject.SetActive(true);
        Log("GameObject activated");
    }

    #endregion

    #region Dirty Flag Tests

    [ContextMenu("Dirty/SetDirty All")]
    public void SetDirtyAll()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.All);
        Log("SetDirty(All)");
    }

    [ContextMenu("Dirty/SetDirty Text")]
    public void SetDirtyText()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.Text);
        Log("SetDirty(Text)");
    }

    [ContextMenu("Dirty/SetDirty Layout")]
    public void SetDirtyLayout()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.Layout);
        Log("SetDirty(Layout)");
    }

    [ContextMenu("Dirty/SetDirty Color")]
    public void SetDirtyColor()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.Color);
        Log("SetDirty(Color)");
    }

    [ContextMenu("Dirty/SetDirty Alignment")]
    public void SetDirtyAlignment()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.Alignment);
        Log("SetDirty(Alignment)");
    }

    [ContextMenu("Dirty/SetDirty Font")]
    public void SetDirtyFont()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.Font);
        Log("SetDirty(Font)");
    }

    [ContextMenu("Dirty/SetDirty FontSize")]
    public void SetDirtyFontSize()
    {
        if (target == null) return;
        target.SetDirty(UniText.DirtyFlags.FontSize);
        Log("SetDirty(FontSize)");
    }

    #endregion

    #region State Info

    [ContextMenu("Info/Log Current State")]
    public void LogCurrentState()
    {
        if (target == null) return;
        Debug.Log($"═══════════════════════════════════════\n" +
                  $"UniText State: {target.name}\n" +
                  $"───────────────────────────────────────\n" +
                  $"Text: \"{target.Text}\" ({target.Text?.Length ?? 0} chars)\n" +
                  $"Fonts: {(target.FontStack != null ? target.FontStack.name : "null")}\n" +
                  $"MainFont: {(target.MainFont != null ? target.MainFont.name : "null")}\n" +
                  $"FontSize: {target.FontSize}\n" +
                  $"Color: {target.color}\n" +
                  $"Direction: {target.BaseDirection}\n" +
                  $"WordWrap: {target.WordWrap}\n" +
                  $"HAlign: {target.HorizontalAlignment}\n" +
                  $"VAlign: {target.VerticalAlignment}\n" +
                  $"Enabled: {target.enabled}\n" +
                  $"LastResultSize: {target.ResultSize}\n" +
                  $"GlyphCount: {target.ResultGlyphs.Length}\n" +
                  $"═══════════════════════════════════════");
    }

    [ContextMenu("Info/Log Glyphs")]
    public void LogGlyphs()
    {
        if (target == null) return;
        var glyphs = target.ResultGlyphs;
        Debug.Log($"Glyphs ({glyphs.Length}):");
        for (var i = 0; i < glyphs.Length && i < 50; i++)
        {
            var g = glyphs[i];
            Debug.Log($"  [{i}] glyph={g.glyphId} cluster={g.cluster} pos=({g.x:F1}, {g.y:F1}) font={g.fontId}");
        }

        if (glyphs.Length > 50)
            Debug.Log($"  ... and {glyphs.Length - 50} more");
    }

    #endregion

    #region Restore

    [ContextMenu("Restore/Restore All")]
    public void RestoreAll()
    {
        if (target == null) return;
        if (originalFontStack != null) target.FontStack = originalFontStack;
        if (originalFontSize > 0) target.FontSize = originalFontSize;
        target.color = originalColor;
        if (originalText != null) target.Text = originalText;
        Log("All settings restored");
    }

    #endregion

    #region Stress Tests

    [ContextMenu("Stress/Rapid Text Changes (100x)")]
    public void RapidTextChanges()
    {
        if (target == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++) target.Text = $"Iteration {i}";
        sw.Stop();
        Log($"100 text changes in {sw.ElapsedMilliseconds}ms");
    }

    [ContextMenu("Stress/Rapid Property Changes (100x)")]
    public void RapidPropertyChanges()
    {
        if (target == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            target.FontSize = 20 + i % 20;
            target.color = Random.ColorHSV();
            target.HorizontalAlignment = (HorizontalAlignment)(i % 3);
        }

        sw.Stop();
        Log($"300 property changes in {sw.ElapsedMilliseconds}ms");
    }

    [ContextMenu("Stress/Toggle Empty 100x")]
    public void ToggleEmpty100x()
    {
        if (target == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++) target.Text = i % 2 == 0 ? "" : testText;
        sw.Stop();
        Log($"100 empty toggles in {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    private List<ModRegister> last;
    
    [ContextMenu("Modifiers/Set")]
    public void SetMods()
    {
        last ??= new((List<ModRegister>)target.ModRegisters);
        target.ClearModifiers();
        for (var i = 0; i < modifiers.Count; i++) target.RegisterModifier(modifiers[i]);
    }
    
    [ContextMenu("Modifiers/Restore")]
    public void RestoreMods()
    {
        if(last == null) return;
        target.ClearModifiers();
        for (var i = 0; i < last.Count; i++) target.RegisterModifier(last[i]);
    }
    
    [ContextMenu("Modifiers/Add")]
    public void AddMods()
    {
        RemovedMods();

        for (var i = 0; i < modifiers.Count; i++) target.RegisterModifier(modifiers[i]);
    }

    [ContextMenu("Modifiers/Remove")]
    public void RemovedMods()
    {
        for (var i = 0; i < modifiers.Count; i++) target.UnregisterModifier(modifiers[i]);
    }

    private void Log(string message)
    {
        Debug.Log($"[UniTextTest] {message}");
    }
}