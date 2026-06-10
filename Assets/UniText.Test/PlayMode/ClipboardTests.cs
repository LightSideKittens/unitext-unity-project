using System.Collections;
using System.Collections.Generic;
using System.Text;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class ClipboardTests : LiveEditableTest
    {
        private sealed class FakeClipboard : IClipboardProvider
        {
            public readonly List<ClipboardItem> items = new();
            public bool HasFormat(ClipboardFormat format) => items.Exists(i => i.Format == format);
            public string GetText(ClipboardFormat format)
            {
                foreach (var i in items)
                    if (i.Format == format) return Encoding.UTF8.GetString(i.Data);
                return null;
            }
            public byte[] GetData(ClipboardFormat format)
            {
                foreach (var i in items)
                    if (i.Format == format) return i.Data;
                return null;
            }
            public void SetItems(IReadOnlyList<ClipboardItem> newItems)
            {
                items.Clear();
                items.AddRange(newItems);
            }
        }

        [UnityTest]
        public IEnumerator Copy_WritesSelectedTextToClipboard()
        {
            var fake = new FakeClipboard();
            UniTextClipboard.Provider = fake;
            try
            {
                yield return BuildField();
                yield return Seed("hello world");
                editable.Select(0, 5);
                editable.Copy();
                Assert.AreEqual("hello", fake.GetText(ClipboardFormat.PlainText));
            }
            finally
            {
                UniTextClipboard.Provider = null;
            }
        }

        [UnityTest]
        public IEnumerator Cut_RemovesSelectionAndCopies()
        {
            var fake = new FakeClipboard();
            UniTextClipboard.Provider = fake;
            try
            {
                yield return BuildField();
                yield return Seed("hello");
                editable.Select(0, 2);
                editable.Cut();
                Assert.AreEqual("llo", editable.Text);
                Assert.AreEqual("he", fake.GetText(ClipboardFormat.PlainText));
            }
            finally
            {
                UniTextClipboard.Provider = null;
            }
        }

        [UnityTest]
        public IEnumerator Paste_InsertsClipboardTextAtCaret()
        {
            yield return BuildField();
            yield return Seed("ab");
            editable.MoveCaretTo(1);
            editable.PasteFromItems(new[] { new ClipboardItem(ClipboardFormat.PlainText, "XY") });
            Assert.AreEqual("aXYb", editable.Text);
        }

        [UnityTest]
        public IEnumerator SingleLinePaste_StripsNewlines()
        {
            yield return BuildField();
            editable.PasteFromItems(new[] { new ClipboardItem(ClipboardFormat.PlainText, "a\nb\nc") });
            Assert.AreEqual("abc", editable.Text);
        }
    }
}
