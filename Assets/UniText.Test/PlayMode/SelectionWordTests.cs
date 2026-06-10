using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class SelectionWordTests : LiveEditableTest
    {
        private UniTextSelectable Selectable => editable.GetComponent<UniTextSelectable>();

        [UnityTest]
        public IEnumerator SelectWord_PlainWord()
        {
            yield return BuildField();
            yield return Seed("foo bar baz");
            Selectable.SelectWord(5);
            Assert.AreEqual("bar", editable.SelectedText);
        }

        [UnityTest]
        public IEnumerator SelectWord_ApostropheJoinsContraction()
        {
            yield return BuildField();
            yield return Seed("I don't care");
            Selectable.SelectWord(3);
            Assert.AreEqual("don't", editable.SelectedText);
        }

        [UnityTest]
        public IEnumerator SelectWord_PeriodJoinsDecimal()
        {
            yield return BuildField();
            yield return Seed("pi is 3.14 yes");
            Selectable.SelectWord(8);
            Assert.AreEqual("3.14", editable.SelectedText);
        }
    }
}
