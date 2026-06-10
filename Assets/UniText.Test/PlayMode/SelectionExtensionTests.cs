using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class SelectionExtensionTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator ShiftLeft_ExtendsBackward()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(3);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.LeftArrow, NativeModifiers.Shift);
            Assert.AreEqual(3, editable.Selection.Anchor);
            Assert.AreEqual(2, editable.Selection.Focus);
            Assert.IsFalse(editable.Selection.IsCollapsed);
        }

        [UnityTest]
        public IEnumerator SelectAllThenType_ReplacesEverything()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.SelectAll();
            editable.InsertText("X");
            Assert.AreEqual("X", editable.Text);
        }

        [UnityTest]
        public IEnumerator ShiftHome_SelectsToLineStart()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.MoveCaretTo(5);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Home, NativeModifiers.Shift);
            Assert.AreEqual(0, editable.SelectionStart);
            Assert.AreEqual(5, editable.SelectionEnd);
            Assert.IsFalse(editable.Selection.IsCollapsed);
        }

        [UnityTest]
        public IEnumerator UnshiftedArrow_CollapsesSelectionToEdge()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.Select(0, 3);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, NativeModifiers.None);
            Assert.IsTrue(editable.Selection.IsCollapsed);
            Assert.AreEqual(3, editable.CaretPosition);
        }
    }
}
