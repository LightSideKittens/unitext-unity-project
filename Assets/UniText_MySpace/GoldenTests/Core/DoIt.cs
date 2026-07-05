using System;
using System.Collections;
using LightSide;
using UnityEngine;

[Serializable]
public abstract class DoIt
{
    public abstract IEnumerator Do(UniText uniText, RectTransform rectTransform);
}

[Serializable]
public class SetActiveSystemFont : DoIt
{
    public bool disabled;
    
    public override IEnumerator Do(UniText uniText, RectTransform rectTransform)
    {
        SystemFont.Disabled = disabled;
        yield return null;
    }
}