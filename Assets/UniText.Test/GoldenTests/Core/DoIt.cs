using System;
using System.Collections;
using LightSide;
using UnityEngine;

[Serializable]
public abstract class DoIt
{
    public abstract IEnumerator Do(UniText uniText, RectTransform rectTransform);
}
