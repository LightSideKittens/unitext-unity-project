#if UNITEXT_SLIDESHOW
using System;
using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;

/// <summary>
/// Drives one <see cref="UniTextEditable"/> through every native configuration the package can
/// produce, holding each just long enough to be unmistakable when the device screen recording is
/// scrubbed. The recording is the only artifact that contains the OS-owned layers — the soft
/// keyboard, the native replica and its presenter surface — so nothing here captures a screenshot;
/// each state contributes a pass/fail record instead.
/// </summary>
internal static class EditableCaseSlideshow
{
    /// <summary>Hold per state. Long enough to read a frame off the recording, no longer.</summary>
    private const float stateDwell = 1.5f;
    private const int settleFrames = 8;
    private const float keyboardTimeout = 8f;
    private const float orientationTimeout = 8f;

    private const string Short = "Hello";

    private const string Long =
        "The quick brown fox jumps over the lazy dog, then turns around and does it again, " +
        "because a docked composer only shows what it can fit and the rest has to scroll.";

    private const string Paragraphs = "First paragraph.\nSecond paragraph.\nThird paragraph.";
    private const string Rtl = "مرحبا بالعالم، هذا نص عربي طويل بما يكفي للالتفاف على عدة أسطر.";
    private const string Cjk = "こんにちは世界。これは日本語の長い文章で、折り返しの確認に使います。";
    private const string Emoji = "Family 👨‍👩‍👧‍👦 flag 🏴󠁧󠁢󠁳󠁣󠁴󠁿 skin 👍🏽 done";

    /// <summary>
    /// Runs the whole matrix in portrait, then a short landscape tail. Every case reconfigures the
    /// field from a blank slate, so a case can never inherit a neighbour's behaviors.
    /// </summary>
    internal static IEnumerator Run(UniTextEditable field, TestResultCollection results)
    {
        var rig = new EditableRig(field);

        Screen.orientation = ScreenOrientation.Portrait;
        yield return WaitForOrientation(portrait: true);

        foreach (var state in Portrait())
            yield return Play(rig, results, state);

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return WaitForOrientation(portrait: false);

        foreach (var state in Landscape())
            yield return Play(rig, results, state);

        field.Defocus();
        Screen.orientation = ScreenOrientation.Portrait;
        yield return WaitForOrientation(portrait: true);
    }

    /// <summary>
    /// Applies one case, raises the keyboard, records whether it came up, and holds the result. A
    /// configuration failure is recorded and the run continues: one broken case must not cost the
    /// remaining coverage.
    /// </summary>
    private static IEnumerator Play(EditableRig rig, TestResultCollection results, EditableCase state)
    {
        var start = DateTime.UtcNow;
        string error = null;
        try
        {
            state.Configure(rig.Reset());
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            error = e.ToString();
        }

        if (error != null)
        {
            Record(results, state.Name, start, error);
            yield break;
        }

        if (rig.NeedsReopen)
        {
            rig.Field.Defocus();
            for (var f = 0; f < settleFrames; f++) yield return null;
        }

        rig.Field.Activate();
        yield return SettleKeyboard();
        yield return rig.RunLive();

        Record(results, state.Name, start,
            UniTextNativeInput.IsKeyboardVisible ? null : "Soft keyboard never became visible");

        var until = Time.realtimeSinceStartup + stateDwell;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    private static IEnumerable<EditableCase> Portrait()
    {
        // Line shape: wrapping and newline acceptance are independent, and every pairing produces a
        // different native control.
        yield return new("shape-single-line", r => r.NoWrap().SingleLine().Text(Short));
        yield return new("shape-single-line-long", r => r.NoWrap().SingleLine().Text(Long));
        yield return new("shape-wrapping-no-paragraphs",
            r => r.SingleLine().Return(ReturnKeyType.Send).Text(Long));
        yield return new("shape-multiline", r => r.Text(Long));
        yield return new("shape-multiline-no-wrap", r => r.NoWrap().Text(Paragraphs));

        // The declared action lands on the key itself while the key is free to carry it.
        yield return new("return-default", r => r.NoWrap().SingleLine().Text(Short));
        yield return new("return-go", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Go).Text(Short));
        yield return new("return-search", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Search).Text(Short));
        yield return new("return-send", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Send).Text(Short));
        yield return new("return-next", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Next).Text(Short));
        yield return new("return-previous", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Previous).Text(Short));
        yield return new("return-done", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Done).Text(Short));
        yield return new("return-enter", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Enter).Text(Short));

        // A field that accepts line breaks spends the key on them, so the same declaration has to
        // surface as the presenter's own control instead.
        yield return new("action-none", r => r.Text(Short));
        yield return new("action-go", r => r.Return(ReturnKeyType.Go).Text(Short));
        yield return new("action-search", r => r.Return(ReturnKeyType.Search).Text(Short));
        yield return new("action-send", r => r.Return(ReturnKeyType.Send).Text(Short));
        yield return new("action-done", r => r.Return(ReturnKeyType.Done).Text(Short));
        yield return new("action-next", r => r.Return(ReturnKeyType.Next).Text(Short));
        yield return new("action-previous", r => r.Return(ReturnKeyType.Previous).Text(Short));
        yield return new("action-send-empty", r => r.Return(ReturnKeyType.Send).Placeholder("Message"));

        yield return new("keyboard-ascii", r => r.NoWrap().SingleLine().Layout(KeyboardType.ASCIICapable));
        yield return new("keyboard-numbers-punctuation",
            r => r.NoWrap().SingleLine().Layout(KeyboardType.NumbersAndPunctuation));
        yield return new("keyboard-url", r => r.NoWrap().SingleLine().Layout(KeyboardType.URL));
        yield return new("keyboard-number-pad", r => r.NoWrap().SingleLine().Layout(KeyboardType.NumberPad));
        yield return new("keyboard-phone-pad", r => r.NoWrap().SingleLine().Layout(KeyboardType.PhonePad));
        yield return new("keyboard-email", r => r.NoWrap().SingleLine().Layout(KeyboardType.EmailAddress));
        yield return new("keyboard-decimal-pad", r => r.NoWrap().SingleLine().Layout(KeyboardType.DecimalPad));
        yield return new("keyboard-web-search", r => r.NoWrap().SingleLine().Layout(KeyboardType.WebSearch));

        yield return new("content-empty-placeholder", r => r.Placeholder("Type a message"));
        yield return new("content-paragraphs", r => r.Text(Paragraphs));
        yield return new("content-rtl", r => r.Text(Rtl));
        yield return new("content-cjk", r => r.Text(Cjk));
        yield return new("content-emoji", r => r.Text(Emoji));

        // The authoritative push into a focused replica — the path a filter rejection also takes.
        yield return new("live-grow", r => r.Placeholder("Type a message").Then(Short).Then(Long));
        yield return new("live-clear", r => r.Placeholder("Type a message").Text(Long).Then(string.Empty));

        yield return new("policy-password", r => r.NoWrap().SingleLine().Password().Text(Short));
        yield return new("policy-password-revealed",
            r => r.NoWrap().SingleLine().Password(revealed: true).Text(Short));
        yield return new("policy-readonly", r => r.ReadOnly().Text(Long));
        yield return new("policy-length-limit", r => r.NoWrap().SingleLine().Limit(12).Counter().Text(Short));
        yield return new("policy-input-mask", r => r.NoWrap().SingleLine().Mask("(###) ###-####")
            .Layout(KeyboardType.PhonePad));
        yield return new("policy-case-upper", r => r.NoWrap().SingleLine().Case(LetterCase.Upper).Text(Short));
        yield return new("policy-autofill-username",
            r => r.NoWrap().SingleLine().Autofill(AutofillHint.Username).Layout(KeyboardType.EmailAddress));
        yield return new("policy-autofill-one-time-code",
            r => r.NoWrap().SingleLine().Autofill(AutofillHint.OneTimeCode).Layout(KeyboardType.NumberPad));

        yield return new("decorator-supporting-text", r => r.NoWrap().SingleLine().Supporting("Required"));

        // Without the overlay behavior the native field is invisible and UniText renders instead.
        yield return new("transparent-single-line", r => r.Transparent().NoWrap().SingleLine().Text(Short));
        yield return new("transparent-multiline", r => r.Transparent().Text(Long));
        yield return new("transparent-password",
            r => r.Transparent().NoWrap().SingleLine().Password().Text(Short));
    }

    private static IEnumerable<EditableCase> Landscape()
    {
        yield return new("landscape-single-line", r => r.NoWrap().SingleLine().Return(ReturnKeyType.Done).Text(Short));
        yield return new("landscape-composer", r => r.SingleLine().Return(ReturnKeyType.Send).Text(Long));
        yield return new("landscape-multiline-long", r => r.Text(Long));
        yield return new("landscape-number-pad", r => r.NoWrap().SingleLine().Layout(KeyboardType.NumberPad));
    }

    private static IEnumerator SettleKeyboard()
    {
        var deadline = Time.realtimeSinceStartup + keyboardTimeout;
        while (!UniTextNativeInput.IsKeyboardVisible && Time.realtimeSinceStartup < deadline)
            yield return null;

        for (var f = 0; f < settleFrames; f++) yield return null;
    }

    private static IEnumerator WaitForOrientation(bool portrait)
    {
        var deadline = Time.realtimeSinceStartup + orientationTimeout;
        while ((Screen.height > Screen.width) != portrait && Time.realtimeSinceStartup < deadline)
            yield return null;

        for (var f = 0; f < settleFrames; f++) yield return null;
    }

    private static void Record(TestResultCollection results, string name, DateTime start, string error)
    {
        results.Add(new TestResult
        {
            ClassName = "EditableCaseSlideshow",
            MethodName = name,
            Passed = error == null,
            ErrorMessage = error,
            StartTime = start,
            EndTime = DateTime.UtcNow
        });
    }

    private readonly struct EditableCase
    {
        internal readonly string Name;
        internal readonly Action<EditableRig> Configure;

        internal EditableCase(string name, Action<EditableRig> configure)
        {
            Name = name;
            Configure = configure;
        }
    }
}
#endif
