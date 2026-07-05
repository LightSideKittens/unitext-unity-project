using System;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("Font", 3)]
public class FontSizeTest : BaseTestCase
{
    [SerializeField] private string testName = "Font_Size";
    [SerializeField] private float fontSize = 48f;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.FontSize = fontSize;
    }
}

[Serializable, TypeGroup("Font", 3)]
public class FontColorTest : BaseTestCase
{
    [SerializeField] private string testName = "Font_Color";
    [SerializeField] private Color color = Color.red;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.color = color;
    }
}

[Serializable, TypeGroup("Font", 3)]
public class AutoSizeTest : BaseTestCase
{
    [SerializeField] private string testName = "Font_AutoSize";
    [SerializeField] private bool autoSize = true;
    [SerializeField] private float minSize = 12f;
    [SerializeField] private float maxSize = 72f;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.AutoSize = autoSize;
        uniText.MinFontSize = minSize;
        uniText.MaxFontSize = maxSize;
    }
}
