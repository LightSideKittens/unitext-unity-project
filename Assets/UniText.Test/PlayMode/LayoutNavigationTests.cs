using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class LayoutNavigationTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Home_MovesToLineStart()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.MoveCaretTo(5);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Home, NativeModifiers.None);
            Assert.AreEqual(0, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator End_MovesToLineEnd()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.MoveCaretTo(0);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.End, NativeModifiers.None);
            Assert.AreEqual(5, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator DownArrow_MovesIntoNextLine()
        {
            yield return BuildField(singleLine: false);
            yield return Seed("aaaa\nbbbb");
            editable.MoveCaretTo(1);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.DownArrow, NativeModifiers.None);
            Assert.GreaterOrEqual(editable.CaretPosition, 5);
        }

        [UnityTest]
        public IEnumerator UpArrow_MovesIntoPreviousLine()
        {
            yield return BuildField(singleLine: false);
            yield return Seed("aaaa\nbbbb");
            editable.MoveCaretTo(7);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.UpArrow, NativeModifiers.None);
            Assert.LessOrEqual(editable.CaretPosition, 4);
        }
    }
}
