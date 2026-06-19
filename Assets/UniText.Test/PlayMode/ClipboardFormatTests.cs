using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class ClipboardFormatTests : LiveEditableTest
    {
        private static ClipboardCopyContext Ctx(UniTextEditable e, string source, string plain)
            => new ClipboardCopyContext(e, source, source, plain, 0, plain.Length);

        private static string Html(ClipboardCopyContext c) => TagHtmlClipboardAdapter.instance.SerializeCopy(c);
        private static string Md(ClipboardCopyContext c) => MarkdownClipboardAdapter.instance.SerializeCopy(c);

        [UnityTest]
        public IEnumerator Bold_ToHtmlAndMarkdown()
        {
            yield return BuildField();
            uniText.EnsureStyleFor(new BoldModifier(), new TagRule("b"));
            var ctx = Ctx(editable, "<b>x</b>", "x");
            Assert.AreEqual("<b>x</b>", Html(ctx));
            Assert.AreEqual("**x**", Md(ctx));
        }

        [UnityTest]
        public IEnumerator NestedBoldItalic_PreservesNesting()
        {
            yield return BuildField();
            uniText.EnsureStyleFor(new BoldModifier(), new TagRule("b"));
            uniText.EnsureStyleFor(new ItalicModifier(), new TagRule("i"));
            var ctx = Ctx(editable, "<b>a<i>b</i></b>", "ab");
            Assert.AreEqual("<b>a<i>b</i></b>", Html(ctx));
            Assert.AreEqual("**a*b***", Md(ctx));
        }

        [UnityTest]
        public IEnumerator LiteralAngleBracket_EscapedInHtml()
        {
            yield return BuildField();
            uniText.EnsureStyleFor(new BoldModifier(), new TagRule("b"));
            var ctx = Ctx(editable, "<b>a < b</b>", "a < b");
            Assert.AreEqual("<b>a &lt; b</b>", Html(ctx));
        }

        [UnityTest]
        public IEnumerator UnmappedFormat_DropsTagsKeepsText()
        {
            yield return BuildField();
            uniText.EnsureStyleFor(new ShadowModifier(), new TagRule("shadow"));
            var ctx = Ctx(editable, "<shadow>x</shadow>", "x");
            Assert.AreEqual("x", Html(ctx));
            Assert.AreEqual("x", Md(ctx));
        }
    }
}
