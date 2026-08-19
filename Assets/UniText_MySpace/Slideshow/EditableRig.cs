#if UNITEXT_SLIDESHOW
using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;

/// <summary>
/// Configures one <see cref="UniTextEditable"/> from a blank slate for a single slideshow case.
/// Every call applies immediately: mutating <c>Behaviors</c> enables the behavior synchronously, and
/// the behaviors that reshape the native field raise their own session invalidation. The caller
/// configures a released field and opens it afterwards, which is also what makes word wrapping
/// take — it never reaches an already-open native field.
/// </summary>
internal sealed class EditableRig
{
    /// <summary>Hold between the pushes of a live text sequence.</summary>
    private const float livePushDwell = 1.2f;

    private readonly UniTextEditable field;
    private readonly List<string> live = new();

    private NativeKeyboardBehavior keyboard;
    private NativeFieldOverlayBehavior overlay;
    private UniText placeholderLabel;
    private UniText counterLabel;
    private UniText supportingLabel;

    internal EditableRig(UniTextEditable field) => this.field = field;

    internal UniTextEditable Field => field;

    /// <summary>
    /// Drops every behavior the previous case left — including the ones authored in the scene — and
    /// returns the field to the default shape: wrapping, line breaks accepted, visible replica, no
    /// text, no decorators.
    /// </summary>
    internal EditableRig Reset()
    {
        live.Clear();
        field.Behaviors.Clear();
        field.BehaviorPresets.Clear();
        field.ReadOnly = false;
        field.Text = string.Empty;
        HideLabels();
        Wrap(true);

        keyboard = new NativeKeyboardBehavior();
        overlay = new NativeFieldOverlayBehavior();
        field.Behaviors.Add(keyboard);
        field.Behaviors.Add(overlay);
        return this;
    }

    /// <summary>Drops the visible replica: the native field stays invisible and UniText renders.</summary>
    internal EditableRig Transparent()
    {
        if (overlay != null && field.Behaviors.Remove(overlay)) overlay = null;
        return this;
    }

    internal EditableRig NoWrap() => Wrap(false);

    internal EditableRig SingleLine()
    {
        field.Behaviors.Add(new SingleLineBehavior { KeepFocusOnSubmit = true });
        return this;
    }

    internal EditableRig Return(ReturnKeyType type)
    {
        keyboard.Keyboard.ReturnKeyType = type;
        return this;
    }

    internal EditableRig Layout(KeyboardType type)
    {
        keyboard.Keyboard.KeyboardType = type;
        return this;
    }

    internal EditableRig Autofill(AutofillHint hint)
    {
        keyboard.Keyboard.AutofillHint = hint;
        return this;
    }

    internal EditableRig Text(string value)
    {
        field.Text = value;
        return this;
    }

    /// <summary>Queues an authoritative text push applied after the keyboard is up.</summary>
    internal EditableRig Then(string value)
    {
        live.Add(value);
        return this;
    }

    internal EditableRig Placeholder(string text)
    {
        placeholderLabel = EnsureLabel(ref placeholderLabel, "SlideshowPlaceholder", 0f, 0.45f);
        placeholderLabel.Text = text;
        field.Behaviors.Add(new PlaceholderDecorator { Target = placeholderLabel });
        return this;
    }

    internal EditableRig Counter()
    {
        counterLabel = EnsureLabel(ref counterLabel, "SlideshowCounter", -1f, 0.7f);
        field.Behaviors.Add(new CharacterCounterDecorator { Target = counterLabel });
        return this;
    }

    internal EditableRig Supporting(string helper)
    {
        supportingLabel = EnsureLabel(ref supportingLabel, "SlideshowSupporting", -1f, 0.7f);
        field.Behaviors.Add(new SupportingTextDecorator { Target = supportingLabel, Helper = helper });
        return this;
    }

    internal EditableRig Password(bool revealed = false)
    {
        field.Behaviors.Add(new PasswordBehavior { Revealed = revealed });
        return this;
    }

    internal EditableRig ReadOnly()
    {
        field.ReadOnly = true;
        return this;
    }

    internal EditableRig Limit(int max)
    {
        field.Behaviors.Add(new LengthLimitBehavior { Limit = max });
        return this;
    }

    internal EditableRig Mask(string pattern)
    {
        field.Behaviors.Add(new InputMaskBehavior { Pattern = pattern });
        return this;
    }

    internal EditableRig Case(LetterCase letterCase)
    {
        field.Behaviors.Add(new CaseTransformBehavior { Case = letterCase });
        return this;
    }

    /// <summary>Replays the queued text pushes into the focused field, one hold apart.</summary>
    internal IEnumerator RunLive()
    {
        for (var i = 0; i < live.Count; i++)
        {
            field.Text = live[i];
            var until = Time.realtimeSinceStartup + livePushDwell;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }

    private EditableRig Wrap(bool on)
    {
        field.TextComponent.WordWrap = on;
        return this;
    }

    private void HideLabels()
    {
        if (placeholderLabel != null) placeholderLabel.gameObject.SetActive(false);
        if (counterLabel != null) counterLabel.gameObject.SetActive(false);
        if (supportingLabel != null) supportingLabel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Builds a label that borrows the field's own typeface, so a decorator target never depends on
    /// a scene slot the slideshow would have to author. <paramref name="row"/> 0 overlays the field,
    /// -1 sits one field height below it.
    /// </summary>
    private UniText EnsureLabel(ref UniText cached, string name, float row, float alpha)
    {
        if (cached == null)
        {
            var source = field.TextComponent;
            var host = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(source.rectTransform, false);
            var shift = row * source.rectTransform.rect.height;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, shift);
            rect.offsetMax = new Vector2(0f, shift);

            var label = host.AddComponent<UniText>();
            label.Font = source.Font;
            label.FontStack = source.FontStack;
            label.FontSize = source.FontSize;
            var tint = source.color;
            label.color = new Color(tint.r, tint.g, tint.b, alpha);
            cached = label;
        }

        cached.gameObject.SetActive(true);
        return cached;
    }
}
#endif
