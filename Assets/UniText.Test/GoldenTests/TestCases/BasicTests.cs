using System;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("Basic", 0)]
public class TextTest : BaseTestCase
{
    [SerializeField] private string testName = "Basic_Text";
    [SerializeField] private string text = "Hello World";

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.Text = text;
    }
}

[Serializable, TypeGroup("Basic", 0)]
public class WordWrapTest : BaseTestCase
{
    [SerializeField] private string testName = "Basic_WordWrap";
    [SerializeField] private bool wordWrap = true;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.WordWrap = wordWrap;
    }
}
