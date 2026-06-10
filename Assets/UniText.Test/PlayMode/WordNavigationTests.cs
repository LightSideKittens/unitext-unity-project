using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class WordNavigationTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator WordRight_MovesToNextWordBoundary()
        {
            yield return BuildField();
            yield return Seed("foo bar");
            editable.MoveCaretTo(0);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, WordMod);
            Assert.AreEqual(4, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator WordLeft_MovesToPreviousWordBoundary()
        {
            yield return BuildField();
            yield return Seed("foo bar");
            editable.MoveCaretTo(7);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.LeftArrow, WordMod);
            Assert.AreEqual(4, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator WordRightShift_ExtendsSelectionByWord()
        {
            yield return BuildField();
            yield return Seed("foo bar");
            editable.MoveCaretTo(0);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, WordMod | NativeModifiers.Shift);
            Assert.AreEqual(0, editable.Selection.Anchor);
            Assert.AreEqual(4, editable.Selection.Focus);
        }
    }
}
