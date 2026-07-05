using System;
using LightSide;
using UnityEngine;

[Serializable]
public abstract class BaseTestCase
{
    public abstract string TestName { get; }

    public TypedList<DoIt> before = new();
    public TypedList<DoIt> after = new();

    public abstract void ApplyTo(UniText uniText, RectTransform rectTransform);

    public virtual bool TryVerify(UniText uniText, out string error)
    {
        error = null;
        return false;
    }
}

[Serializable]
public class TestEntry
{
    [Tooltip("Target UniText component. Leave empty for dynamic test.")]
    public UniText targetUniText;

    public TypedList<BaseTestCase> testCases = new();
}
