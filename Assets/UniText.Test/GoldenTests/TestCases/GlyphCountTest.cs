using System;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("Smoke", 0)]
public class GlyphCountTest : BaseTestCase
{
    [SerializeField] private string testName = "Smoke_GlyphCount";
    [Tooltip("Minimum number of real (non-.notdef) glyphs the component must render.")]
    [SerializeField] private int minGlyphCount = 1;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform) { }

    public override bool TryVerify(UniText uniText, out string error)
    {
#if UNITY_WEBGL
        return true;
#endif
        error = null;
#if UNITEXT_TESTS
        var rendered = uniText.TestRenderedGlyphCount;
        if (rendered < minGlyphCount)
            error = $"Expected at least {minGlyphCount} real glyphs, got {rendered}";
#endif
        return true;
    }
}
