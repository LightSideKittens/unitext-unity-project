using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;

/// <summary>
/// Expands a clamped text when the link at its end is clicked, revealing the rest a unit at a time.
/// </summary>
/// <remarks>
/// Expects the text to carry a <see cref="RevealModifier"/> clamping the body — <c>Unit = Line</c>,
/// an absolute <c>Front</c>, <c>Collapse</c> on, and <c>Reserve For</c> naming the link's
/// <c>#label</c> — and a <see cref="LinkModifier"/> on that link, nested in a preset or not. Add a
/// second <see cref="RevealModifier"/> over the link, with <c>Collapse</c> on and a hide effect, and
/// the link fades out of the layout once the text is open; without one it simply stays.
/// Turn the link's <c>Auto Open Url</c> off, or the click also tries to open one.
/// </remarks>
[AddComponentMenu("UniText/Read More Text")]
public sealed class ReadMoreText : MonoBehaviour
{
    [SerializeField, Tooltip("Text to expand; the one on this object when unset.")]
    private UniTextBase text;

    [SerializeField, Tooltip("Granularity the rest arrives in — the clamp itself stays in lines.")]
    private TextUnit revealUnit = TextUnit.Word;

    [SerializeField, Min(0f), Tooltip("Units revealed per second; 0 opens the text at once.")]
    private float unitsPerSecond = 14f;

    private RevealModifier body;
    private RevealModifier affix;
    private LinkModifier link;
    private Coroutine running;

    private bool warned;

    private void Reset() => text = GetComponent<UniTextBase>();

    private void OnEnable()
    {
        if (text == null) text = GetComponent<UniTextBase>();
        if (text == null)
        {
            Debug.LogWarning($"[{nameof(ReadMoreText)}] No text to expand.", this);
            enabled = false;
            return;
        }

        text.LayoutCommitted += Bind;
    }

    private void OnDisable()
    {
        if (text != null) text.LayoutCommitted -= Bind;
        if (link != null) link.LinkClicked -= OnLinkClicked;
        link = null;
        body = null;
        affix = null;
    }

    /// <summary>
    /// Takes the modifiers once the text has built: a preset's graph exists only from the first
    /// build on, so a link inside one is not there to be found before it.
    /// </summary>
    private void Bind()
    {
        link = text.GetModifier<LinkModifier>();
        if (link == null)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning($"[{nameof(ReadMoreText)}] The text carries no link to click.", this);
            return;
        }

        ResolveReveals();
        link.LinkClicked += OnLinkClicked;
        text.LayoutCommitted -= Bind;
    }

    /// <summary>
    /// Sorts the text's reveals: the one naming a reserved range is the clamp, any other covers the
    /// link and plays it out once the text is open.
    /// </summary>
    private void ResolveReveals()
    {
        var reveals = new List<RevealModifier>();
        text.GetModifiers(reveals);

        for (var i = 0; i < reveals.Count; i++)
        {
            if (!string.IsNullOrEmpty(reveals[i].ReserveFor)) body = reveals[i];
            else affix ??= reveals[i];
        }

        body ??= affix;
        if (body == affix) affix = null;
    }

    private void OnLinkClicked(string _)
    {
        if (body == null || running != null) return;
        running = StartCoroutine(Expand());
    }

    /// <summary>
    /// Opens the text from where the clamp left it. The frontier is driven as a percentage, which
    /// means the same fraction whatever unit counts it — so switching the unit for the reveal costs
    /// no conversion and no frame spent waiting for one.
    /// </summary>
    private IEnumerator Expand()
    {
        var total = text.CountUnits(revealUnit);
        if (total <= 0) yield break;

        var shown = text.CountUnits(revealUnit, body.VisibleRange);
        var percent = 100f * shown / total;

        body.Unit = revealUnit;
        body.Front = UnitValue.Percent(percent);

        var percentPerSecond = unitsPerSecond * 100f / total;
        while (percentPerSecond > 0f && percent < 100f)
        {
            percent = Mathf.Min(100f, percent + Time.unscaledDeltaTime * percentPerSecond);
            body.Front = UnitValue.Percent(percent);
            yield return null;
        }

        body.Front = UnitValue.Percent(100f);
        if (affix != null) affix.Front = UnitValue.Percent(0f);
        running = null;
    }
}
