using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class BiDiModelTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator WritingDirection_LtrText_IsLeftToRight()
        {
            yield return BuildField();
            yield return Seed("abc");
            Assert.AreEqual(1, editable.WritingDirectionAtCharIndex(0));
        }

        [UnityTest]
        public IEnumerator WritingDirection_HebrewText_IsRightToLeft()
        {
            yield return BuildField();
            yield return Seed("" + (char)0x05D0 + (char)0x05D1 + (char)0x05D2);
            Assert.AreEqual(2, editable.WritingDirectionAtCharIndex(0));
        }
    }
}
