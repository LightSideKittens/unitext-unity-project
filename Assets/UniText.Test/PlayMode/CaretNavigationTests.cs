using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class CaretNavigationTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator MoveCaretTo_ClampsOutOfRange()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(99);
            Assert.AreEqual(3, editable.CaretPosition);
            editable.MoveCaretTo(-5);
            Assert.AreEqual(0, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator Select_SetsStartEndAndText()
        {
            yield return BuildField();
            yield return Seed("abcdef");
            editable.Select(1, 4);
            Assert.AreEqual(1, editable.SelectionStart);
            Assert.AreEqual(4, editable.SelectionEnd);
            Assert.AreEqual("bcd", editable.SelectedText);
        }

        [UnityTest]
        public IEnumerator SelectAll_CoversWholeDocument()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.SelectAll();
            Assert.AreEqual(0, editable.SelectionStart);
            Assert.AreEqual(5, editable.SelectionEnd);
        }

        [UnityTest]
        public IEnumerator RightArrow_MovesPastWholeGraphemeCluster()
        {
            yield return BuildField();
            yield return Seed("ae" + (char)0x0301);
            editable.MoveCaretTo(1);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, NativeModifiers.None);
            Assert.AreEqual(3, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator LeftArrow_MovesBackOneGrapheme()
        {
            yield return BuildField();
            yield return Seed("abc");
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.LeftArrow, NativeModifiers.None);
            Assert.AreEqual(2, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator ShiftRightArrow_ExtendsSelectionByGrapheme()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(0);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, NativeModifiers.Shift);
            Assert.AreEqual(0, editable.Selection.Anchor);
            Assert.AreEqual(1, editable.Selection.Focus);
            Assert.IsFalse(editable.Selection.IsCollapsed);
        }
    }
}
