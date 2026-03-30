using System;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("RTL", 1)]
public class DirectionTest : BaseTestCase
{
    [SerializeField] private string testName = "RTL_Direction";
    [SerializeField] private TextDirection direction = TextDirection.RightToLeft;

    public override string TestName => testName;

    public override void ApplyTo(UniText uniText, RectTransform rectTransform)
    {
        uniText.BaseDirection = direction;
    }
}
