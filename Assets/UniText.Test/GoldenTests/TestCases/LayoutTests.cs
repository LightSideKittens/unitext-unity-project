using System;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("Layout", 2)]
public class HorizontalAlignmentTest : BaseTestCase
{
    [SerializeField] private string testName = "Layout_HAlign";
    [SerializeField] private HorizontalAlignment alignment = HorizontalAlignment.Center;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.HorizontalAlignment = alignment;
    }
}

[Serializable, TypeGroup("Layout", 2)]
public class VerticalAlignmentTest : BaseTestCase
{
    [SerializeField] private string testName = "Layout_VAlign";
    [SerializeField] private VerticalAlignment alignment = VerticalAlignment.Middle;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.VerticalAlignment = alignment;
    }
}
