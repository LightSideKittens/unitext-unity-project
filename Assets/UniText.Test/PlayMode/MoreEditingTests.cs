using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class MoreEditingTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator DeleteWordNext_RemovesFollowingWord()
        {
            yield return BuildField();
            yield return Seed("foo bar");
            editable.MoveCaretTo(0);
            editable.DeleteWordNext();
            Assert.IsFalse(editable.Text.Contains("foo"));
            Assert.IsTrue(editable.Text.EndsWith("bar"));
        }

        [UnityTest]
        public IEnumerator DeleteToLineEnd_RemovesRestOfLine()
        {
            yield return BuildField();
            yield return Seed("hello world");
            editable.MoveCaretTo(5);
            editable.DeleteToLineEnd();
            Assert.AreEqual("hello", editable.Text);
        }

        [UnityTest]
        public IEnumerator SetText_ReplacesAllAndClearsUndo()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.SetText("hello");
            Assert.AreEqual("hello", editable.Text);
            editable.Undo();
            Assert.AreEqual("hello", editable.Text);
        }
    }
}
