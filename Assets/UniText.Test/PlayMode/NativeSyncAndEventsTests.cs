using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class NativeSyncAndEventsTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator NativeSelectionChanged_UpdatesSelection()
        {
            yield return BuildField();
            yield return Seed("hello");
            UniTextNativeInput.RaiseSelectionChanged(1, 3);
            Assert.AreEqual(1, editable.SelectionStart);
            Assert.AreEqual(3, editable.SelectionEnd);
        }

        [UnityTest]
        public IEnumerator TextChanged_FiresOnInsert()
        {
            yield return BuildField();
            var fired = false;
            editable.TextChanged += () => fired = true;
            editable.InsertText("x");
            yield return null;
            Assert.IsTrue(fired);
        }

        [UnityTest]
        public IEnumerator ValueChanged_CarriesNewText()
        {
            yield return BuildField();
            string value = null;
            editable.ValueChanged += v => value = v;
            editable.InsertText("hi");
            yield return null;
            Assert.AreEqual("hi", value);
        }

        [UnityTest]
        public IEnumerator DocumentChanged_FiresWithReason()
        {
            yield return BuildField();
            string reason = null;
            editable.DocumentChanged += r => reason = r;
            editable.InsertText("x");
            yield return null;
            Assert.IsNotNull(reason);
        }
    }
}
