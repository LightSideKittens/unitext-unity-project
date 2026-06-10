using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class CharacterLimitTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Limit_TruncatesPaste()
        {
            yield return BuildField();
            editable.AddBehavior(new CharacterLimitBehavior { Limit = 3 });
            editable.PasteFromItems(new[] { new ClipboardItem(ClipboardFormat.PlainText, "abcdef") });
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator Limit_BlocksInputBeyondCap()
        {
            yield return BuildField();
            editable.AddBehavior(new CharacterLimitBehavior { Limit = 3 });
            yield return Seed("abc");
            editable.InsertText("d");
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator Limit_AllowsReplaceWithinCap()
        {
            yield return BuildField();
            editable.AddBehavior(new CharacterLimitBehavior { Limit = 3 });
            yield return Seed("abc");
            editable.Select(0, 3);
            editable.InsertText("XYZ");
            Assert.AreEqual("XYZ", editable.Text);
        }
    }
}
