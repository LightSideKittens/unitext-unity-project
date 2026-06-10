using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class SelectionVisualTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Caret_HiddenWhileSelectionActive()
        {
            yield return BuildField();
            yield return Seed("hello");
            Assert.IsNotNull(editable.CaretRenderer);
            editable.Select(0, 3);
            yield return null;
            Assert.IsFalse(editable.CaretRenderer.Enabled);
        }

        [UnityTest]
        public IEnumerator Caret_ShownWhenSelectionCollapses()
        {
            yield return BuildField();
            yield return Seed("hello");
            editable.Select(0, 3);
            yield return null;
            editable.MoveCaretTo(2);
            yield return null;
            Assert.IsTrue(editable.CaretRenderer.Enabled);
        }
    }
}
