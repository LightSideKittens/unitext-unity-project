using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class EditingSanitizationTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator Insert_StripsNullAndC0Controls()
        {
            yield return BuildField();
            UniTextNativeInput.RaiseTextInput("a" + (char)0x00 + "b" + (char)0x01 + "c");
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator Insert_StripsDel()
        {
            yield return BuildField();
            UniTextNativeInput.RaiseTextInput("x" + (char)0x7F + "y");
            Assert.AreEqual("xy", editable.Text);
        }

        [UnityTest]
        public IEnumerator Insert_KeepsTab()
        {
            yield return BuildField();
            UniTextNativeInput.RaiseTextInput("a\tb");
            Assert.AreEqual("a\tb", editable.Text);
        }

        [UnityTest]
        public IEnumerator SingleLine_StripsNewline()
        {
            yield return BuildField();
            UniTextNativeInput.RaiseTextInput("a\nb");
            Assert.AreEqual("ab", editable.Text);
        }

        [UnityTest]
        public IEnumerator Document_KeepsNewline()
        {
            yield return BuildField(singleLine: false);
            UniTextNativeInput.RaiseTextInput("a\nb");
            Assert.AreEqual("a\nb", editable.Text);
        }

        [UnityTest]
        public IEnumerator Insert_DropsLoneSurrogate_KeepsEmojiPair()
        {
            yield return BuildField();
            UniTextNativeInput.RaiseTextInput("a" + (char)0xD800 + "b");
            Assert.AreEqual("ab", editable.Text);

            UniTextNativeInput.RaiseTextInput("😀");
            Assert.AreEqual("ab😀", editable.Text);
        }
    }
}
