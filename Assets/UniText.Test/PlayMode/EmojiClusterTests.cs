using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class EmojiClusterTests : LiveEditableTest
    {
        private static string Family() =>
            "a"
            + (char)0xD83D + (char)0xDC68
            + (char)0x200D
            + (char)0xD83D + (char)0xDC69
            + (char)0x200D
            + (char)0xD83D + (char)0xDC67
            + "b";

        [UnityTest]
        public IEnumerator RightArrow_MovesOverWholeZwjEmojiCluster()
        {
            yield return BuildField();
            yield return Seed(Family());
            editable.MoveCaretTo(1);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.RightArrow, NativeModifiers.None);
            Assert.AreEqual(6, editable.CaretPosition);
        }

        [UnityTest]
        public IEnumerator Backspace_DeletesWholeZwjEmojiCluster()
        {
            yield return BuildField();
            yield return Seed(Family());
            editable.MoveCaretTo(6);
            editable.DeletePrevious();
            Assert.AreEqual("ab", editable.Text);
        }
    }
}
