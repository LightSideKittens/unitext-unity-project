using System;
using System.Collections;
using LightSide;
using UnityEngine;

[Serializable, TypeGroup("Action")]
public class TakeScreenshot : DoIt
{
    [Tooltip("Screenshot name without extension. Empty uses the target's GameObject name.")]
    [SerializeField] private string screenshotName;

    public override IEnumerator Do(UniText uniText, RectTransform rectTransform)
    {
        TestScreenshot.Capture(string.IsNullOrEmpty(screenshotName) ? uniText.name : screenshotName);
        yield break;
    }
}

[Serializable, TypeGroup("Action")]
public class MoveTransform : DoIt
{
    [Tooltip("Transform to move. Empty moves the test target's RectTransform.")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 position;

    public override IEnumerator Do(UniText uniText, RectTransform rectTransform)
    {
        var t = target != null ? target : rectTransform;
        t.localPosition = position;
        yield break;
    }
}

[Serializable, TypeGroup("Action")]
public class WaitFrames : DoIt
{
    [SerializeField] private int frames = 1;

    public override IEnumerator Do(UniText uniText, RectTransform rectTransform)
    {
        for (int i = 0; i < frames; i++)
            yield return null;
    }
}
