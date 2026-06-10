using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class ModeTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator ReadOnly_BlocksTypedInput()
        {
            yield return BuildField();
            editable.InsertText("abc");
            editable.ReadOnly = true;
            UniTextNativeInput.RaiseTextInput("x");
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator ReadOnly_BlocksBackspaceKey()
        {
            yield return BuildField();
            editable.InsertText("abc");
            editable.ReadOnly = true;
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Backspace, NativeModifiers.None);
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator ReadOnly_AllowsSelection()
        {
            yield return BuildField();
            yield return Seed("abc");
            editable.ReadOnly = true;
            editable.SelectAll();
            Assert.AreEqual(0, editable.SelectionStart);
            Assert.AreEqual(3, editable.SelectionEnd);
        }

        [UnityTest]
        public IEnumerator CharacterLimit_TruncatesInsert()
        {
            yield return BuildField();
            editable.AddBehavior(new CharacterLimitBehavior { Limit = 3 });
            editable.InsertText("abcdef");
            Assert.AreEqual("abc", editable.Text);
        }

        [UnityTest]
        public IEnumerator PasswordMode_KeepsRealTextInModel()
        {
            yield return BuildField();
            editable.AddBehavior(new PasswordBehavior());
            editable.InsertText("secret");
            Assert.AreEqual("secret", editable.Text);
        }

        [UnityTest]
        public IEnumerator SingleLine_ReturnKeyFiresSubmit()
        {
            yield return BuildField();
            editable.InsertText("hi");
            string submitted = null;
            editable.Submit += t => submitted = t;
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Return, NativeModifiers.None);
            Assert.AreEqual("hi", submitted);
        }

        [UnityTest]
        public IEnumerator Document_ReturnKeyInsertsNewline()
        {
            yield return BuildField(singleLine: false);
            editable.InsertText("a");
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Return, NativeModifiers.None);
            editable.InsertText("b");
            Assert.AreEqual("a\nb", editable.Text);
        }

        [UnityTest]
        public IEnumerator EscapeKey_FiresCancelled()
        {
            yield return BuildField();
            bool cancelled = false;
            editable.Cancelled += () => cancelled = true;
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Escape, NativeModifiers.None);
            Assert.IsTrue(cancelled);
        }

        [UnityTest]
        public IEnumerator Tab_InsertsTab_WhenEnabled()
        {
            yield return BuildField(singleLine: false);
            editable.AddBehavior(new TabKeyBehavior());
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Tab, NativeModifiers.None);
            Assert.AreEqual("\t", editable.Text);
        }

        [UnityTest]
        public IEnumerator Tab_DoesNotInsert_WhenDisabled()
        {
            yield return BuildField(singleLine: false);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Tab, NativeModifiers.None);
            Assert.AreEqual(string.Empty, editable.Text);
        }
    }
}
