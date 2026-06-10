using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class EditingTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Insert_AppendsAtCaret()
        {
            yield return BuildField();
            editable.InsertText("hello");
            Assert.AreEqual("hello", editable.Text);
            Assert.AreEqual(5, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator Insert_ReplacesActiveSelection()
        {
            yield return BuildField();
            yield return Seed("abcdef");
            editable.Select(2, 4);
            editable.InsertText("X");
            Assert.AreEqual("abXef", editable.Text);
            Assert.AreEqual(3, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator DeletePrevious_RemovesCharBeforeCaret()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.DeletePrevious();
            Assert.AreEqual("ab", editable.Text);
            Assert.AreEqual(2, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator DeletePrevious_RemovesWholeGraphemeCluster()
        {
            yield return BuildField();
            yield return Seed("ae" + (char)0x0301);
            editable.DeletePrevious();
            Assert.AreEqual("a", editable.Text);
        }

        [UnityTest]
        public IEnumerator DeleteNext_RemovesCharAfterCaret()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(1);
            editable.DeleteNext();
            Assert.AreEqual("ac", editable.Text);
            Assert.AreEqual(1, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator DeleteSelection_RemovesRangeAndCollapses()
        {
            yield return BuildField();
            yield return Seed("abcdef");
            editable.Select(1, 4);
            editable.DeleteSelection();
            Assert.AreEqual("aef", editable.Text);
            Assert.IsTrue(editable.Selection.IsCollapsed);
            Assert.AreEqual(1, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator DeleteWordPrevious_RemovesPrecedingWord()
        {
            yield return BuildField();
            yield return Seed("hello world");
            editable.DeleteWordPrevious();
            Assert.IsTrue(editable.Text.StartsWith("hello"));
            Assert.IsFalse(editable.Text.Contains("world"));
        }

        [UnityTest]
        public IEnumerator TransposeCharacters_SwapsTwoBeforeCaret()
        {
            yield return BuildField();
            yield return Seed("ab");
            editable.TransposeCharacters();
            Assert.AreEqual("ba", editable.Text);
        }
    }
}
