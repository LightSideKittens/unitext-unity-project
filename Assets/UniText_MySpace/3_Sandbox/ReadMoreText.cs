using System.Collections;
using LightSide;
using UnityEngine;

/// <summary>
/// Expands a clamped text when the link at its end is clicked, revealing the rest a unit at a time.
/// </summary>
/// <remarks>
/// Expects one <see cref="RevealModifier"/> range clamping the body — labelled, counting
/// <c>Line</c>s, collapsing, and reserving for the link's own label — and a
/// <see cref="LinkModifier"/> on that link:
/// <code>
/// &lt;reveal #body=,4abs,line,true,more&gt;…&lt;/reveal&gt; &lt;link #more=&gt;More...&lt;/link&gt;
/// </code>
/// Label a second reveal range over the link and name it in <c>Affix Label</c>, and the link plays
/// out once the text is open; without one it simply stays. Turn the link's <c>Auto Open Url</c> off,
/// or the click also tries to open one.
/// </remarks>
[AddComponentMenu("UniText/Read More Text")]
public sealed class ReadMoreText : MonoBehaviour
{
    [SerializeField, Tooltip("Text to expand; the one on this object when unset.")]
    private UniTextBase text;

    [SerializeField, Tooltip("#label of the reveal range holding the clamped body.")]
    private string bodyLabel = "body";

    [SerializeField, Tooltip("#label of a reveal range to play out once open; none when empty.")]
    private string affixLabel;

    [SerializeField, Tooltip("Granularity the rest arrives in — the clamp itself stays in lines.")]
    private TextUnit revealUnit = TextUnit.Word;

    [SerializeField, Min(0f), Tooltip("Units revealed per second; 0 opens the text at once.")]
    private float unitsPerSecond = 14f;

    private RevealModifier reveal;
    private LinkModifier link;
    private OwnedParameterSet<RevealModifier, UnitValue> bodyFront;
    private OwnedParameterSet<RevealModifier, TextUnit> bodyUnit;
    private OwnedParameterSet<RevealModifier, UnitValue> affixFront;
    private TextRange bodySpan;
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
        bodyFront?.Release();
        bodyUnit?.Release();
        affixFront?.Release();
        bodyFront = null;
        bodyUnit = null;
        affixFront = null;
        link = null;
        reveal = null;
    }

    /// <summary>
    /// Takes the modifiers once the text has built: a preset's graph exists only from the first
    /// build on, so a modifier inside one is not there to be found before it.
    /// </summary>
    private void Bind()
    {
        reveal = text.GetModifier<RevealModifier>();
        link = text.GetModifier<LinkModifier>();
        if (reveal == null || link == null || !text.TryGetLabeled(bodyLabel, out bodySpan))
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(
                $"[{nameof(ReadMoreText)}] Needs a link and a reveal range labelled '{bodyLabel}'.",
                this);
            return;
        }

        bodyFront = RangeQuery.For(reveal).WhereLabel(bodyLabel)
            .Own(RevealModifier.Param.Front);
        bodyUnit = RangeQuery.For(reveal).WhereLabel(bodyLabel)
            .Own(RevealModifier.Param.Unit);
        if (!string.IsNullOrEmpty(affixLabel))
            affixFront = RangeQuery.For(reveal).WhereLabel(affixLabel)
                .Own(RevealModifier.Param.Front);

        link.LinkClicked += OnLinkClicked;
        text.LayoutCommitted -= Bind;
    }

    private void OnLinkClicked(string _)
    {
        if (bodyFront == null || running != null) return;
        running = StartCoroutine(Expand());
    }

    /// <summary>
    /// Opens the body from where the clamp left it. The frontier is driven as a percentage, which
    /// means the same fraction whatever unit counts it — so switching the unit for the reveal costs
    /// no conversion and no frame spent waiting for one.
    /// </summary>
    private IEnumerator Expand()
    {
        var total = text.CountUnits(revealUnit, bodySpan);
        if (total <= 0) yield break;

        var shown = text.CountUnits(revealUnit, reveal.VisibleRangeIn(bodySpan));
        var percent = 100f * shown / total;

        bodyUnit.Value = revealUnit;
        bodyFront.Value = UnitValue.Percent(percent);

        var percentPerSecond = unitsPerSecond * 100f / total;
        while (percentPerSecond > 0f && percent < 100f)
        {
            percent = Mathf.Min(100f, percent + Time.unscaledDeltaTime * percentPerSecond);
            bodyFront.Value = UnitValue.Percent(percent);
            yield return null;
        }

        bodyFront.Value = UnitValue.Percent(100f);
        if (affixFront != null) affixFront.Value = UnitValue.Percent(0f);
        running = null;
    }
}
