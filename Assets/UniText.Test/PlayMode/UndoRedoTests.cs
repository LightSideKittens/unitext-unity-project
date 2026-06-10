using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class UndoRedoTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Undo_RevertsInsert()
        {
            yield return BuildField();
            editable.InsertText("hello");
            editable.Undo();
            Assert.AreEqual(string.Empty, editable.Text);
        }

        [UnityTest]
        public IEnumerator UndoThenRedo_RestoresInsert()
        {
            yield return BuildField();
            editable.InsertText("hello");
            editable.Undo();
            editable.Redo();
            Assert.AreEqual("hello", editable.Text);
        }

        [UnityTest]
        public IEnumerator Undo_RevertsReplace()
        {
            yield return BuildField();
            yield return Seed("abcdef");
            editable.Select(0, 3);
            editable.InsertText("X");
            Assert.AreEqual("Xdef", editable.Text);
            editable.Undo();
            Assert.AreEqual("abcdef", editable.Text);
        }

        [UnityTest]
        public IEnumerator ClearUndoHistory_PreventsUndo()
        {
            yield return BuildField();
            editable.InsertText("hello");
            editable.ClearUndoHistory();
            editable.Undo();
            Assert.AreEqual("hello", editable.Text);
        }
    }
}
