using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace LightSide.Tests
{
    /// <summary>
    /// D3 regression: backspace/forward-delete adjacent to a width-bearing projected range
    /// (backslash escape — auto-registered on every editable) must delete the WHOLE source
    /// range, never silently no-op. Keys injected through the real native-input seam
    /// (universal keys only), matching how every platform backend delivers them.
    /// </summary>
    public class BackspaceAtomicRangeTests : LiveEditableFixture
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Build();
            yield return Settle();
            editable.Activate(showKeyboard: false);
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Teardown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackspaceAfterEscapeDeletesWholeRange()
        {
            editable.SetText("a\\*b");
            yield return Settle();
            editable.MoveCaretTo(3);
            yield return Settle();

            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Backspace, NativeModifiers.None);
            yield return Settle();

            Assert.AreEqual("ab", editable.Text, "backspace after the escape must delete '\\*' whole");
        }

        [UnityTest]
        public IEnumerator ForwardDeleteBeforeEscapeDeletesWholeRange()
        {
            editable.SetText("a\\*b");
            yield return Settle();
            editable.MoveCaretTo(1);
            yield return Settle();

            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Delete, NativeModifiers.None);
            yield return Settle();

            Assert.AreEqual("ab", editable.Text, "forward delete before the escape must delete '\\*' whole");
        }

        [UnityTest]
        public IEnumerator BackspaceAtEndDeletesSingleChar()
        {
            editable.SetText("a\\*b");
            yield return Settle();
            editable.MoveCaretTo(4);
            yield return Settle();

            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Backspace, NativeModifiers.None);
            yield return Settle();

            Assert.AreEqual("a\\*", editable.Text, "plain trailing char deletes normally");
        }
    }
}
