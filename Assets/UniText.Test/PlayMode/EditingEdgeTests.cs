using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class EditingEdgeTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Backspace_AtStart_NoOp()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(0);
            editable.DeletePrevious();
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator Delete_AtEnd_NoOp()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.MoveCaretTo(3);
            editable.DeleteNext();
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator Backspace_OnEmpty_NoOp()
        {
            yield return BuildField();
            editable.DeletePrevious();
            Assert.AreEqual(string.Empty, editable.Text);
        }

        [UnityTest]
        public IEnumerator Backspace_DeletesSurrogatePairAsOne()
        {
            yield return BuildField();
            yield return Seed("a😀b");
            editable.DeletePrevious();
            Assert.AreEqual("a😀", editable.Text);
            editable.DeletePrevious();
            Assert.AreEqual("a", editable.Text);
        }

        [UnityTest]
        public IEnumerator Backspace_AcrossNewline_MergesLines()
        {
            yield return BuildField(singleLine: false);
            yield return Seed("ab\ncd");
            editable.MoveCaretTo(3);
            editable.DeletePrevious();
            Assert.AreEqual("abcd", editable.Text);
        }
    }
}
