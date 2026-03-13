# Input Field Plans — Critical Review

> Deep analysis of all five input field planning documents.
> Every issue is evidence-backed by web research across multiple sources.
>
> Documents reviewed:
> - [`input-field-research.md`](input-field-research.md)
> - [`input-field-architecture.md`](input-field-architecture.md)
> - [`native-clipboard-research.md`](native-clipboard-research.md)
> - [`native-ime-research.md`](native-ime-research.md)
> - [`native-mobile-input-research.md`](native-mobile-input-research.md)

---

## Summary by Severity

| Severity | Count | Issues |
|----------|-------|--------|
| **CRITICAL** | 7 | #3, #4, #6, #7, #11, #18, #34 |
| **BUG** | 6 | #8, #9, #21, #23, #32, #33 |
| **MEDIUM** | 17 | #1, #5, #12, #13, #14, #17, #24, #26, #27, #28, #29, #30, #31, #35, #37, #38, #41 |
| **PERFORMANCE** | 2 | #10, #16 |
| **LOW** | 4 | #2, #15, #20, #25 |

## Resolution Status

| Status | Issues |
|--------|--------|
| **RESOLVED** | #1, #2, #3, #4, #5, #6, #7, #8, #9, #10, #11, #13, #14, #18, #21, #23, #28, #33, #38, #39 |
| **NOT A PROBLEM** | #12 (Unity UI known limitation), #15 (negligible for input field buffers), #19 (confirmed correct), #25 (cosmetic), #39 (project uses .NET Standard 2.1) |
| **DEFERRED TO v1.1** | #16 (CodepointToCharIndex O(n)), #17 (testing strategy), #20 (magnifier RenderTexture) |
| **NOTED — will address during implementation** | #24 (kUTTypePlainText deprecated), #26 (P/Invoke wrappers), #27 (GetActiveWindow), #29 (SetWindowSubclass + disable Unity IME), #30 (Android overlay View), #31 (BaseInputConnection context), #32 (iOS keyboard show), #34 (Android keyboard height), #35 (iOS three-finger gestures), #37 (UnitySendMessage), #41 (disable Unity IME) |
| **NEEDS DECISION** | #40 (testing strategy — scope and timing) |

---

## I. `input-field-research.md`

### Issue #1 — UAX#14 vs UAX#29 contradiction (MEDIUM)

Line 587 says UAX#14 line break is "needed for word-level navigation (Ctrl+Arrow)." But architecture Section 5.5 explicitly says "UAX #14 line break opportunities are NOT suitable" for word navigation and instead uses character-class transitions. The research file needs to be updated to say UAX#14 is used for **line wrapping**, not word navigation.

### Issue #2 — Section 8.3 virtualized rendering is premature (LOW)

Lines 561-570 describe virtualized text rendering for "thousands of lines" — this is listed as a non-goal in architecture Section 1.2. Should be clearly marked as out-of-scope for v1.

### Issue #3 — Missing Event.PopEvent input system caveat (CRITICAL)

Research confirmed: `Event.PopEvent` works with Legacy and "Both" Active Input Handling modes, but does **NOT** receive keyboard events when Active Input Handling is set to "Input System Package" only. The IMGUI event queue is not populated when the legacy pipeline is disabled.

`Event.PopEvent` is declared as `public static extern bool PopEvent(Event outEvent)` in `Modules/IMGUI/Event.bindings.cs`. It reads from Unity's internal IMGUI event queue, which is populated by the native runtime from OS-level events. This is NOT the OS event queue directly — it's Unity's processed/translated version. When the legacy input pipeline is disabled, this queue is not fed.

The research file should note this requirement: the project must use "Both" or "Legacy" Active Input Handling mode.

**Sources:**
- [Unity Scripting API: Event.PopEvent](https://docs.unity3d.com/ScriptReference/Event.PopEvent.html)
- [UnityCsReference: Event.bindings.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/IMGUI/Event.bindings.cs)
- [Unity Manual: FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-faq-event-and-input-system.html)

---

## II. `input-field-architecture.md`

### Issue #4 — Zero-allocation violation: `new[] { c }` per keystroke (CRITICAL)

Lines 832-844, `InsertCharacter()`: `charsToInsert = new[] { pendingHighSurrogate, c }` and `charsToInsert = new[] { c }` allocate a `char[]` on every keystroke. This directly violates the "Zero-allocation hot path" principle (Section 1.1, item 4).

**Fix**: Use a `Span<char>` from stackalloc or a preallocated 2-element buffer:

```csharp
Span<char> chars = stackalloc char[2];
int len;
if (char.IsLowSurrogate(c) && pendingHighSurrogate != 0) {
    chars[0] = pendingHighSurrogate; chars[1] = c; len = 2;
} else {
    chars[0] = c; len = 1;
}
gapBuffer.Insert(charIndex, chars.Slice(0, len));
```

### Issue #5 — SelectionState doc/comments mismatch (MEDIUM)

Lines 271-274: XML comments say "grapheme cluster index" but Section 5.2 prose (line 298) says the selection model operates on "codepoint indices". The code examples throughout (e.g., `CodepointToCharIndex(selection.focus)`) confirm **codepoint indices**. The XML comments are wrong and misleading.

### Issue #6 — ValidateChar return type vs usage (CRITICAL)

Line 847: `if (validator != null && !validator.ValidateChar(charsToInsert[0], caretCharIndex, text))` — the `!` operator is applied to a `char` return value. `ValidateChar` returns `char` (line 1350: `public abstract char ValidateChar(...)`) where `'\0'` means reject. The `!` operator doesn't compile on `char`.

**Fix**: `validator.ValidateChar(...) == '\0'`

### Issue #7 — `string.Concat(ReadOnlySpan<char>)` will NOT compile (CRITICAL)

Lines 1128-1131:

```csharp
return string.Concat(
    text.AsSpan(0, caretCharIndex),
    compositionString.AsSpan(),
    text.AsSpan(caretCharIndex));
```

`string.Concat(ReadOnlySpan<char>, ReadOnlySpan<char>, ReadOnlySpan<char>)` does NOT exist in .NET Standard 2.1. It was added to **.NET Core 3.0 runtime** only, not the standard specification. `string.AsSpan()` itself (from `MemoryExtensions`) IS available in .NET Standard 2.1, but the `string.Concat` overload that accepts `ReadOnlySpan<char>` arguments is NOT.

Unity's Mono runtime implements .NET Standard 2.1, not .NET Core 3.0+. This code **will not compile in any Unity version**.

**Fix options**:
- `string.Concat(text.Substring(0, caretCharIndex), compositionString, text.Substring(caretCharIndex))` — less efficient but always works
- `string.Create(totalLen, state, (span, s) => { ... })` — zero-copy but requires .NET Standard 2.1
- `StringBuilder` with pooling

**Sources:**
- [dotnet/runtime#28310: API proposal string.Concat(ReadOnlySpan\<char\>)](https://github.com/dotnet/runtime/issues/28310)
- [Microsoft Learn: String.Concat Method](https://learn.microsoft.com/en-us/dotnet/api/system.string.concat?view=net-8.0)

### Issue #8 — Undeclared variable `deletedText` (BUG)

Line 897: `undoStack.RecordDelete(charStart, deletedText)` — `deletedText` is never declared in `DeletePrevious()`. Must extract the text being deleted before calling `gapBuffer.Delete`:

```csharp
var deletedText = gapBuffer.ToString().Substring(charStart, charEnd - charStart);
gapBuffer.Delete(charStart, charEnd - charStart);
undoStack.RecordDelete(charStart, deletedText);
```

Or better: add a `GapBuffer.GetRange(start, count)` method that returns the text without full `ToString()`.

### Issue #9 — Undeclared variable `compositionStartCodepoint` (BUG)

Line 1231: `CalculateCaretRect(compositionStartCodepoint)` — should be `compositionCaretCodepoint` (declared on line 1109).

### Issue #10 — Double `ToString()` per keystroke (PERFORMANCE)

Line 872: `OnValueChanged?.Invoke(gapBuffer.ToString())` and line 866: `SyncToUniText()` which internally calls `gapBuffer.ToString()` (line 244). Two separate string allocations per keystroke.

**Fix**: Cache the result:

```csharp
var text = gapBuffer.ToString();
SyncToUniText(text);
OnValueChanged?.Invoke(text);
```

### Issue #11 — Event.PopEvent doesn't work with "Input System only" mode (CRITICAL)

Lines 721-722: "TMP_InputField processes input via `Event.PopEvent()` ... which works with both systems." This is misleading.

`Event.PopEvent` reads from the IMGUI native event queue, which is NOT populated when Active Input Handling is set to "Input System Package" only. It ONLY works with "Legacy" or "Both" modes. This is the same underlying issue as #3, but this instance is in the architecture document where it directly affects implementation.

Must document this requirement explicitly and consider: what happens if the user's project uses "Input System Package" only? Either:
- Document it as a hard requirement (use "Both" mode)
- Or provide a secondary input path using `Keyboard.current` from the new Input System

**Sources:**
- [Unity Manual: Runtime UI event system](https://docs.unity3d.com/2021.3/Documentation/Manual/UIE-Runtime-Event-System.html)
- [Input System UI Support docs](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/UISupport.html)

### Issue #12 — Static class design lacks platform dispatch mechanism (MEDIUM)

`UniTextIME`, `UniTextKeyboard`, `UniTextClipboard` are all static classes (lines 1073, 1495, 1999-2001) with per-platform backend files. But the architecture never shows HOW platform dispatch happens. With static classes, you need either:
- `#if UNITY_STANDALONE_WIN` / `#if UNITY_ANDROID` etc. inside each static method
- Or delegate to internal platform-specific instances selected at startup via a factory

This is a significant architectural decision left unspecified.

### Issue #13 — Two input paths with duplication risk (MEDIUM)

Desktop uses `Event.PopEvent` → `keyMap.Resolve()` → `Execute()` (Section 8.3-8.4). Mobile Mode B uses `UniTextKeyboard.OnTextInput` → `InsertCharacter()` directly (Section 16.4, lines 1589-1607). The `OnMobileCompositionChanged` handler (line 1601-1606) bypasses the normal IME composition handler in Section 11.

These are parallel paths doing similar things — risk of behavior divergence. Any behavior added to `Execute()` (logging, analytics, `BeforeTextChange` event, undo break detection) must be manually duplicated in the mobile handlers.

**Fix**: Either route mobile callbacks through the same `Execute()` method, or extract shared logic into methods that both paths call.

### Issue #14 — IME on mobile intertwined with keyboard but treated separately (MEDIUM)

`UniTextIME` (Section 11) and `UniTextKeyboard` (Section 16) are separate abstractions, but on mobile, IME IS the keyboard. Android `InputConnection.setComposingText()` is both the IME handler AND the keyboard handler. The architecture should clarify: on mobile Mode B, `UniTextKeyboard.OnCompositionChanged` IS the IME path. Is `UniTextIME` desktop-only? Or does it wrap the mobile keyboard's IME events too?

### Issue #15 — Growth factor: "always double" is suboptimal (LOW)

Line 237: "Growth: always double (geometric)." Research shows 1.5x is preferred by many production implementations (Java ArrayList, MSVC std::vector, Facebook Folly). With 2x growth, freed blocks can NEVER be reused by the allocator — 2x is mathematically the worst possible value for memory reuse (sum of all previous allocations is always less than the next allocation).

The golden ratio (~1.618) is the theoretical boundary: any factor below it allows eventual memory reuse. 1.5x allows reuse after 4 reallocations.

For an input field's small buffers this barely matters, but the blanket assertion and citation of Nethercote conflates "geometric is better than linear" (true) with "2x is optimal" (false).

**Sources:**
- [Facebook Folly FBVector documentation](https://github.com/facebook/folly/blob/main/folly/docs/FBVector.md)
- [Daniel Lemire: How fast should your dynamic arrays grow?](https://lemire.me/blog/2013/02/06/how-fast-should-your-dynamic-arrays-grow/)

### Issue #16 — `CodepointToCharIndex` calls `gapBuffer.ToString()` (PERFORMANCE)

Lines 919-930: `CodepointToCharIndex` calls `gapBuffer.ToString()` on every invocation. This method is called multiple times per edit operation (line 859, line 893-894). Combined with the `ToString()` calls in `SyncToUniText()` and `OnValueChanged`, a single backspace could produce 4+ string allocations.

**Fix**: Add a `gapBuffer.CharAt(index)` method that reads directly from the buffer (O(1) with gap check), and iterate the gap buffer directly instead of converting to string first.

### Issue #17 — No testing strategy (MEDIUM)

350KB of plans across 5 documents, not a single mention of testing. Unit tests for GapBuffer, SelectionState, GraphemeNavigator, UndoStack coalescing, and CodepointToCharIndex/CharToCodepointIndex are essential. Integration tests for surrogate pair handling, BiDi caret movement, IME composition lifecycle. Should at least have a section on test approach.

### Issue #18 — iPad hardware keyboard scenario missing (CRITICAL)

Section 16 never addresses iPad with hardware keyboard. Research confirms: when a hardware keyboard is connected, `becomeFirstResponder` does NOT show the virtual keyboard — only the QuickType shortcut bar (~55pt tall) appears. `UIKeyboardWillShowNotification` IS still fired, but `UIKeyboardFrameEndUserInfoKey` reports a very small height (just the shortcut bar, ~55pt instead of 300+pt).

Implications:
- `KeyboardArea` will report a very small height — keyboard area avoidance code must handle this
- Mode B still works fine — hardware key events go to firstResponder
- Mode A overlay positioning needs to account for the tiny shortcut bar
- Users can manually show the software keyboard by pressing the eject key on hardware keyboard or tapping the globe icon — `UIKeyboardWillChangeFrameNotification` fires with the full keyboard height

Detection: If `keyboardFrame.size.height < 100`, treat as "hardware keyboard present." `GCKeyboard.coalesced` (iOS 14+ / GameController framework) returns non-nil if a hardware keyboard is connected.

**Sources:**
- [WWDC 2020 - Support hardware keyboards in your app](https://developer.apple.com/videos/play/wwdc2020/10109/)
- [Capacitor PR - prevent black QuickType bar with Magic Keyboard](https://github.com/ionic-team/capacitor-plugins/pull/2403)

### Issue #19 — Undo coalescing on whitespace: correct decision (CONFIRMED OK)

Line 1058: "We do NOT break coalescing on whitespace/punctuation after letters." Research confirms this matches VS Code, Sublime, and macOS NSTextView behavior. VS Code defines `EditOperationType` enum with 6 types (Other, DeletingLeft, DeletingRight, TypingOther, TypingFirstSpace, TypingConsecutiveSpace) and breaks on type change — but continuous typing of letters and then punctuation is still `TypingOther` and coalesces.

macOS NSTextView uses `NSUndoManager.groupsByEvent` (default: true) which groups all undo operations within a single run loop iteration. Developers call `breakUndoCoalescing()` manually for finer control.

The architecture's cursor-movement-as-boundary (line 1051) is the strongest heuristic per research — this is correct and prominently documented.

**Sources:**
- [VS Code cursorCommon.ts](https://github.com/microsoft/vscode/blob/main/src/vs/editor/common/cursorCommon.ts)
- [VS Code Issue #29036](https://github.com/microsoft/vscode/issues/29036)

### Issue #20 — Magnifier uses RenderTexture unnecessarily (LOW)

Line 1688: `IMagnifier.Show(Vector2 focusPoint, RenderTexture textAreaCapture)` — research shows three better approaches:

1. **Scaled mesh re-render** (simplest, recommended): render same text mesh at 2x scale with circular clip shader. No RenderTexture, no extra camera — just one extra draw call. Nearly free.
2. **iOS UITextInteraction** (Mode B with UITextInput): provides the magnifier/loupe, cursor rendering, and selection handles **for free** — no Unity-side implementation needed at all.
3. **Android Magnifier widget** (API 28+): native `android.widget.Magnifier` that can be attached to any view.

Consider changing the interface to not require RenderTexture:

```csharp
void Show(Vector2 focusPoint, RectTransform textArea); // instead of RenderTexture
```

**Sources:**
- [PSPDFKit/Nutrient - Replicating iOS Text Magnifying Glass](https://www.nutrient.io/blog/replicating-ios-text-magnifying-glass/)
- [Apple - UITextInteraction](https://developer.apple.com/documentation/uikit/uitextinteraction)

---

## III. `native-clipboard-research.md`

### Issue #21 — macOS: strdup memory leak (BUG)

Line 107: `return html ? strdup([html UTF8String]) : NULL;` — `strdup` allocates memory that must be freed by the caller. The C# side calling via `[DllImport("__Internal")]` would need to call `Marshal.FreeHGlobal` or a custom `UniText_FreeString()` function, but the document never mentions this.

**Fix**: Either use a static buffer (limits string size), or document the `free()` requirement, or provide a `UniText_FreeString(const char* ptr)` export that the C# side calls after copying the string.

### Issue #22 — Windows: Missing CF_HTML read path (INCOMPLETE)

Section 1 only shows writing CF_HTML. Reading CF_HTML (for paste) requires:
1. Calling `GetClipboardData(cfHtml)` to get the raw UTF-8 bytes
2. Parsing the header to extract `StartFragment`/`EndFragment` byte offsets
3. Extracting the HTML fragment between the sentinel comments
4. Handling the fact that offsets are **byte positions** (UTF-8), not character positions

The byte-offset pitfall is confirmed: when HTML contains multi-byte UTF-8 characters (e.g., `é` = 2 bytes `C3 A9`), using `string.Length` or character counting in C# gives wrong offsets. Must use `Encoding.UTF8.GetByteCount()`.

**Sources:**
- [HTML Clipboard Format - Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)
- [Setting HTML/Text to Clipboard Revisited](https://theartofdev.com/2014/06/12/setting-htmltext-to-clipboard-revisited/)

### Issue #23 — WebGL: Unhandled Promise rejection (BUG)

Lines 218-224: `navigator.clipboard.write(...)` returns a Promise but the `.jslib` code ignores it. If the write fails (no user gesture, HTTPS violation, Safari restriction), the error is silently swallowed.

Additional finding: Unity canvas clicks DO provide transient activation (~5 second window). Synchronous jslib calls from the same frame as the click event work. But Safari has been reported to fail on clipboard operations even when other browsers succeed. Safari may have a shorter activation window.

**Fix**: Add `.catch()` with error reporting back to Unity, or at minimum log the error:

```javascript
navigator.clipboard.write([...]).catch(function(err) {
    console.error('UniText clipboard write failed:', err);
});
```

**Sources:**
- [W3C Clipboard APIs Issue #52](https://github.com/w3c/clipboard-apis/issues/52)
- [WebGL copyToClipboard Safari Issue - Unity Discussions](https://discussions.unity.com/t/webgl-copytoclipboard-does-not-work-for-safari/1708731)

### Issue #24 — iOS: `kUTTypePlainText` deprecated (MEDIUM)

Line 122: `(NSString *)kUTTypePlainText` is deprecated since iOS 15. Should use `UTType.plainText` from `UniformTypeIdentifiers` framework (iOS 14+).

Timeline:
- iOS 14 / Xcode 12: `UniformTypeIdentifiers` framework introduced with `UTType` struct. Old constants marked `API_TO_BE_DEPRECATED`.
- iOS 15: Formal deprecation with compiler warnings.
- Current: Constants still compile and function but produce deprecation warnings.

Since minimum iOS for Unity 2021 is iOS 13, need `#if @available(iOS 14.0, *)` guards, or raise minimum to iOS 14 (effectively universal in 2026).

**Sources:**
- [Apple Forums: kUTTypeData Deprecated](https://developer.apple.com/forums/thread/690388)
- [React Native PR: Migrate to UniformTypeIdentifiers](https://github.com/facebook/react-native/pull/48623)

### Issue #25 — Android: Magic string `"clipboard"` (LOW)

Line 158: `activity.Call<AndroidJavaObject>("getSystemService", "clipboard")` — should reference `Context.CLIPBOARD_SERVICE` constant. While the string `"clipboard"` works, it's fragile.

---

## IV. `native-ime-research.md`

### Issue #26 — P/Invoke signature for ImmGetCompositionStringW (MEDIUM)

Line 84: `static extern int ImmGetCompositionStringW(IntPtr hIMC, uint dwIndex, byte[] lpBuf, uint dwBufLen)` — using `byte[]` for all flags is a simplification that works with manual marshaling but is error-prone.

Different `dwIndex` flags return different data types:
- `GCS_COMPSTR` → UTF-16 chars (should use `char[]` or `StringBuilder`)
- `GCS_COMPATTR` → bytes (attribute values 0-5)
- `GCS_COMPCLAUSE` → `uint[]` (DWORD offsets)
- `GCS_CURSORPOS` → no buffer (returns value directly, `lpBuf` = null)

Should show separate wrappers or note that manual conversion from `byte[]` is needed for each type.

### Issue #27 — `GetActiveWindow` unreliable for HWND (MEDIUM)

Line 73: "Get HWND via `GetActiveWindow()`" — `GetActiveWindow()` can return `IntPtr.Zero` if the Unity window isn't the foreground window (e.g., during startup, when a dialog is up, or during alt-tab).

**Alternatives:**
- `FindWindow` with Unity's window class name
- Cache HWND from first successful `GetActiveWindow` call
- `Process.GetCurrentProcess().MainWindowHandle`

### Issue #28 — iOS UITextInput complexity underestimated (MEDIUM)

Line 249: "iOS: UITextInput (.mm plugin): ~400-500 lines" — UITextInput requires implementing:
- `UITextPosition` and `UITextRange` subclasses (opaque objects, not integer offsets)
- 15+ required protocol methods (`textInRange:`, `replaceRange:withText:`, `selectedTextRange`, `markedTextRange`, `setMarkedText:selectedRange:`, `unmarkText`, `firstRectForRange:`, `caretRectForPosition:`, `positionFromPosition:offset:`, `comparePosition:toPosition:`, `offsetFromPosition:toPosition:`, and more)
- `UITextInputTokenizer` for word/line boundary queries

Realistic estimate: **800-1200 lines**, not 400-500.

### Issue #29 — Window subclassing conflict risk + Unity IME disable (MEDIUM)

Lines 73-77: `SetWindowLongPtr` for window subclassing can conflict with other plugins that also subclass Unity's window.

**Two sub-issues:**

1. **Use `SetWindowSubclass`** (from `comctl32.dll`) instead of `SetWindowLongPtr`. It allows chaining, clean teardown without strict order requirements, and per-subclass reference data.

2. **Must disable Unity's built-in IME handling**: When using custom IMM32 subclassing, you MUST set `Input.imeCompositionMode = IMECompositionMode.Off` to prevent Unity from also processing `WM_IME_*` messages. Without this, both Unity's handler and our custom handler will respond to the same messages, causing duplicate composition strings and unpredictable behavior.

**Sources:**
- [Subclassing Controls - Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/controls/subclassing-overview)
- [Unity IME Documentation](https://docs.unity3d.com/Manual/IMEInput.html)
- [Input.imeCompositionMode](https://docs.unity3d.com/ScriptReference/Input-imeCompositionMode.html)

### Issue #30 — Android `onCreateInputConnection` — approach unresolved (MEDIUM)

Lines 166-169 list three approaches for overriding `View.onCreateInputConnection()` but don't recommend one:
1. Subclass `UnityPlayerActivity`
2. Create invisible overlay `View` that receives focus
3. Reflection on Unity's internal view

The invisible overlay View approach (#2) is the most reliable — it doesn't depend on Unity internals and doesn't require reflection. Reflection (#3) is fragile across Unity versions.

**Recommendation**: Commit to approach #2 (invisible overlay View) and document it as the canonical approach.

---

## V. `native-mobile-input-research.md`

### Issue #31 — `BaseInputConnection(this, false)` and IME context (MEDIUM)

Line 121: `new BaseInputConnection(this, false)` — `fullEditor=false` means `mFallbackMode=true`. In fallback mode:
- `commitText()` replaces composing text, then sends a key event for the committed text, and clears the editable buffer
- `finishComposingText()` also sends key events and clears
- The Editable is transient — cleared after each commit

This is correct for Mode B (we manage our own text). But some IMEs rely on `getTextBeforeCursor()`/`getTextAfterCursor()` for predictions. In fallback mode, these return empty or minimal text. Should override these methods to return our actual text from the gap buffer for better IME predictions.

**Sources:**
- [BaseInputConnection AOSP Source](https://github.com/aosp-mirror/platform_frameworks_base/blob/master/core/java/android/view/inputmethod/BaseInputConnection.java)

### Issue #32 — Mode B keyboard show mechanism missing for iOS (BUG)

The file shows `UIKeyInput` implementation (lines 56-67) but doesn't explain how to actually SHOW the keyboard. Requirements:
1. Create the UIView subclass implementing UIKeyInput
2. Add it to the view hierarchy: `[UnityGetGLView() addSubview:keyReceiver]` (or use `[UIApplication sharedApplication].keyWindow.rootViewController.view`)
3. The view must be in the window and capable of receiving events
4. Call `[keyReceiver becomeFirstResponder]`

Known gotcha: some developers report that `becomeFirstResponder` on a plain UIView implementing UIKeyInput does NOT always show the keyboard on all devices. Unity's own internal iOS keyboard implementation (in `Classes/UI/Keyboard.mm`) uses a **hidden `UITextField`** specifically because this approach is more reliable.

**Sources:**
- [Unity iOS Keyboard.mm Source](https://github.com/gimmie/unity-ios-sample/blob/master/Classes/UI/Keyboard.mm)
- [Hacking with Swift - UIKeyInput](https://www.hackingwithswift.com/example-code/uikit/how-to-create-custom-text-input-using-uikeyinput)

### Issue #33 — Multi-line Mode A needs UITextView, not UITextField (BUG)

Lines 44-49: Mode A shows `UITextField` which is SINGLE-LINE only. iOS `UITextField` does not support multi-line text. For multi-line input in Mode A, must use `UITextView` instead.

Should document both:
- Single-line Mode A → `UITextField`
- Multi-line Mode A → `UITextView`

### Issue #34 — Android keyboard height detection unreliable (CRITICAL)

Lines 144-153: `getWindowVisibleDisplayFrame` has known issues:

**On Android 11+ (API 30+) with edge-to-edge display:**
- Navigation bar, display cutouts/notches, and status bar all affect the visible frame calculation
- Android 15 (SDK 35) enforces edge-to-edge by default, making `getWindowVisibleDisplayFrame` calculations even less reliable

**Floating keyboard (Samsung, Gboard):**
- A floating keyboard does NOT resize the window and does NOT push the visible display frame
- `getWindowVisibleDisplayFrame` returns full screen height
- `WindowInsetsCompat.Type.ime()` returns **zero** bottom inset
- The keyboard is visible but the system reports no IME inset because it's not docked

**Recommended approach (version-gated):**
- API < 30: `getWindowVisibleDisplayFrame` (legacy, works for docked keyboard)
- API 30+: `WindowInsetsCompat.Type.ime()` with `adjustResize`
- Floating keyboard: accept that height is unknowable (this is actually fine — floating keyboard doesn't overlap the bottom in a predictable way, so no layout adjustment needed)

**Sources:**
- [Android - Control and animate the software keyboard](https://developer.android.com/develop/ui/views/layout/sw-keyboard)
- [PSPDFKit - Keyboard Handling on Android](https://pspdfkit.com/blog/2016/keyboard-handling-on-android/)
- [Google Issue Tracker - IME window insets incorrect](https://issuetracker.google.com/issues/203677547)

### Issue #35 — iOS three-finger gestures are NOT automatic for custom views (MEDIUM)

Lines 308-313: States these are "system-level gestures on iOS 13+" implying they work automatically. Research proves they require proper **responder chain setup**:

1. The view must be firstResponder (`canBecomeFirstResponder` returns YES)
2. Must have a non-nil `undoManager` somewhere in the responder chain (override `undoManager` property to return an `NSUndoManager` and register undo/redo actions on it)
3. Must respond to `canPerformAction:withSender:` for `copy:`, `paste:`, `cut:` selectors
4. `editingInteractionConfiguration` property on `UIResponder` must return `.default` (not `.none`)

UIKeyInput alone does NOT provide an `undoManager`. The architecture says "must be implemented in Unity for Mode B" — partially correct, but the responder chain requirements must be documented.

**Sources:**
- [Apple Developer Forums - iOS 16 three finger undo tap gesture](https://developer.apple.com/forums/thread/714425)
- [Hacking with Swift - editingInteractionConfiguration](https://www.hackingwithswift.com/example-code/uikit/how-to-disable-undo-redo-copy-and-paste-gestures-using-editinginteractionconfiguration)

### Issue #36 — Missing iPad hardware keyboard scenario (CRITICAL)

No mention of iPad + hardware keyboard anywhere in the mobile input research. See Issue #18 for full details. Key facts:
- `becomeFirstResponder` does NOT show virtual keyboard — only ~55pt shortcut bar
- `UIKeyboardWillShowNotification` IS still fired but reports very small height
- Mode B still works (hardware key events go to firstResponder)
- Must document detection and handling

### Issue #37 — UnitySendMessage vs JNI callback tradeoff not discussed (MEDIUM)

Lines 124, 128: Comments say "→ deliver to C# via UnitySendMessage or JNI callback" but don't recommend one.

Research confirms:
- **UnitySendMessage**: 1-frame delay (~16ms at 60fps), string-only, async but safe (delivers on Unity main thread). ~8μs per call. Thread-safe — can be called from any thread.
- **AndroidJavaProxy**: Synchronous, typed parameters, but runs on **calling thread** (NOT Unity main thread). Any Unity API calls from the callback will crash without manual thread dispatch.
- **Direct JNI callback**: Fastest but most complex. Requires native C++ plugin, function pointer management, proper thread attachment.

For keyboard input where human typing speed is ~70-100ms between keystrokes, UnitySendMessage's 1-frame delay is invisible. The string overhead for single characters is negligible.

**Recommendation**: Use `UnitySendMessage` as default. Reserve `AndroidJavaProxy` only for time-critical events like keyboard animation synchronization where 1-frame delay matters.

**Sources:**
- [Unity - iOS native plugin callbacks (documents 1-frame delay)](https://docs.unity3d.com/Manual/ios-native-plugin-call-back.html)
- [5argon/UnitySendMessageEfficiencyTest](https://github.com/5argon/UnitySendMessageEfficiencyTest)

---

## VI. Cross-Document Architectural Issues

### Issue #38 — Desktop vs mobile input path divergence (MEDIUM)

Desktop: `Event.PopEvent` → `keyMap.Resolve()` → `Execute()` → editing operations.
Mobile Mode B: `UniTextKeyboard.OnTextInput` → `InsertCharacter()` directly, `OnDeleteBack` → `DeletePrevious()` directly.

These are two completely separate input paths. The mobile path bypasses `InputPlatformKeyMap`, `EditAction`, and the `Execute()` switch — it goes straight to editing methods. This means any behavior added to `Execute()` (logging, analytics, `BeforeTextChange` event, undo break detection on operation type change) must be duplicated in the mobile handlers.

**Fix options:**
- Route mobile callbacks through the same `Execute()` method: `OnTextInput` → `Execute(EditAction.InsertChar, ...)`
- Or extract shared logic (undo recording, BeforeTextChange, validation) into methods that both paths call

### Issue #39 — `ReadOnlySpan` used throughout but compatibility unclear (CRITICAL → requires decision)

`ReadOnlySpan<char>` appears in: GapBuffer API (line 219), GraphemeNavigator (lines 321-337), UniTextIME.Clauses (line 1086), GetDisplayText (lines 1128-1131).

**Unity 2021 compatibility matrix:**
| Version | .NET Standard 2.0 | .NET Standard 2.1 | ReadOnlySpan |
|---------|-------------------|-------------------|--------------|
| 2021.1  | Yes               | **No**            | Only via System.Memory polyfill (IL2CPP issues) |
| 2021.2+ | Yes               | Yes               | Yes, but IL2CPP had bugs through 2021.3.x |

If minimum is truly 2021.1, none of these compile without polyfills. If minimum is 2021.2+, they work with .NET Standard 2.1 API compatibility level set by the user.

**Required decision**: Either raise minimum to 2021.2 and require .NET Standard 2.1, or provide fallback APIs using arrays instead of spans. The architecture must explicitly state this requirement.

**Sources:**
- [Unity Forum: 2021.2.0b6 and System.Memory/ReadOnlySpan](https://forum.unity.com/threads/2021-2-0b6-and-system-memory-readonlyspan-under-net-4-8.1152104/)
- [Unity Issue Tracker: IL2CPP doesn't support ReadOnlySpan parameters](https://discussions.unity.com/t/il2cpp-doesnt-support-readonlymemory-t-or-readonlyspan-byte-method-parameters/914544)

### Issue #40 — No testing strategy across 350KB of plans (MEDIUM)

Five documents, no mention of: unit tests, integration tests, platform-specific test approaches, automated testing on mobile, IME testing methodology.

Minimum test coverage should include:
- **Unit tests**: GapBuffer (insert, delete, gap movement, surrogate pairs, growth), UndoStack (coalescing rules, undo/redo correctness), GraphemeNavigator (emoji sequences, ZWJ, combining marks), CodepointToCharIndex/CharToCodepointIndex (BMP, supplementary, mixed), SelectionState
- **Integration tests**: Surrogate pair insertion/deletion, BiDi caret movement at direction boundaries, IME composition lifecycle (start → update → commit, Korean continuous composition), password masking with emoji
- **Platform tests**: Native clipboard round-trip (write HTML + read HTML), IME on Windows with Japanese/Chinese IME, iOS simulator keyboard show/hide, Android emulator InputConnection
- **Edge case tests**: Empty text operations, max character limit with multi-codepoint graphemes, paste truncation, concurrent IME + mouse click

### Issue #41 — Must disable Unity's IME when using custom IMM32 (MEDIUM)

Not mentioned in any of the five documents. When using custom IMM32 window subclassing for rich IME (Section 11 of architecture, Section 1 of native-ime-research), you MUST set:

```csharp
Input.imeCompositionMode = IMECompositionMode.Off;
```

Without this, Unity's built-in IME handler will ALSO process `WM_IME_STARTCOMPOSITION`, `WM_IME_COMPOSITION`, `WM_IME_ENDCOMPOSITION` messages, causing:
- Duplicate composition strings
- `Input.compositionString` being populated alongside our custom handler
- Potential cursor position conflicts (Unity may call `ImmSetCompositionWindow` with its own position)

Must be set when our input field gains focus and restored when it loses focus.

**Sources:**
- [Unity IME Documentation](https://docs.unity3d.com/Manual/IMEInput.html)
- [Input.imeCompositionMode](https://docs.unity3d.com/ScriptReference/Input-imeCompositionMode.html)

---

## Appendix: Gap Buffer Encoding Validation

Research confirms `GapBuffer<char>` (UTF-16 code units) is the correct choice for Unity:

| Editor | Encoding | Data Structure |
|--------|----------|---------------|
| Emacs | Variable-width bytes (extended UTF-8) | Gap buffer |
| Scintilla | UTF-8 bytes | Gap buffer |
| Xi-editor | UTF-8 bytes | Rope |
| VS Code | UTF-16 (JavaScript strings) | Piece table |
| Zed | UTF-8 bytes | Rope (SumTree) |

Production implementations overwhelmingly use variable-width code units (bytes or UTF-16), NOT `int[]` (codepoints). `int[]` would double memory for BMP text (~99% of real-world text).

Using `char[]` requires surrogate-pair awareness for gap positioning. VS Code had a real bug ([#20624](https://github.com/microsoft/vscode/issues/20624)) where split surrogates caused corruption. The architecture's surrogate handling in `InsertCharacter()` (Section 9.2) addresses this for insertion, but `MoveGap()` and `Delete()` should also be documented as surrogate-aware.

**Sources:**
- [Scintilla gap buffer docs](https://www.scintilla.org/gapbuffer.html)
- [Xi-editor Rope Science Part 2](https://xi-editor.io/docs/rope_science_02.html)
- [VS Code surrogate pair bug #20624](https://github.com/microsoft/vscode/issues/20624)
