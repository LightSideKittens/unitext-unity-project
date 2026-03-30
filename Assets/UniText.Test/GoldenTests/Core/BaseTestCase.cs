using System;
using LightSide;
using UnityEngine;

[Serializable]
public abstract class BaseTestCase
{
        public abstract string TestName { get; }

        public abstract void ApplyTo(UniText uniText, RectTransform rectTransform);
}

[Serializable]
public class TestEntry
{
    [Tooltip("Target UniText component. Leave empty for dynamic test.")]
    public UniText targetUniText;

    public TypedList<BaseTestCase> testCases = new();
}
