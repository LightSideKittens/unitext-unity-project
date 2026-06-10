using System;
using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class ImeCompositionTests : LiveEditableTest
    {
        private void InjectComposition(string text, int cursor, CompositionClause[] clauses = null)
        {
            CompositionClause[] cl = clauses ?? Array.Empty<CompositionClause>();
            var data = new CompositionData
            {
                text = text.AsSpan(),
                clauses = cl,
                cursorPosition = cursor,
            };
            UniTextNativeInput.RaiseCompositionChanged(data);
        }

        private string Overlay() => editable.compositionOverlay.Text.ToString();

        [UnityTest]
        public IEnumerator Composition_IsNonDestructive_UntilCommit()
        {
            yield return BuildField();

            InjectComposition("に", 1);
            Assert.IsTrue(editable.isComposing, "composing after first composition event");
            Assert.AreEqual("に", Overlay(), "overlay holds the in-progress composition");
            Assert.AreEqual(string.Empty, editable.Text, "document is untouched during composition");

            InjectComposition("にほんご", 4);
            Assert.AreEqual("にほんご", Overlay());
            Assert.AreEqual(string.Empty, editable.Text);

            UniTextNativeInput.RaiseTextInput("にほんご");
            UniTextNativeInput.RaiseCompositionEnded();

            Assert.IsFalse(editable.isComposing, "composition ended after commit");
            Assert.IsFalse(editable.compositionOverlay.IsActive, "overlay cleared after commit");
            Assert.AreEqual("にほんご", editable.Text, "committed result inserted exactly once");
        }

        [UnityTest]
        public IEnumerator Composition_CancelLeavesDocumentUnchanged()
        {
            yield return BuildField();

            UniTextNativeInput.RaiseTextInput("abc");
            Assert.AreEqual("abc", editable.Text);

            InjectComposition("にほん", 3);
            Assert.IsTrue(editable.isComposing);
            Assert.AreEqual("abc", editable.Text, "composition does not mutate existing text");

            UniTextNativeInput.RaiseCompositionEnded();

            Assert.IsFalse(editable.isComposing);
            Assert.IsFalse(editable.compositionOverlay.IsActive);
            Assert.AreEqual("abc", editable.Text, "cancel must not leave composition text in the document");
        }

        [UnityTest]
        public IEnumerator Composition_CarriesClauseStyles()
        {
            yield return BuildField();

            var clauses = new[]
            {
                new CompositionClause { startOffset = 0, endOffset = 2, style = CompositionClauseStyle.Converted },
                new CompositionClause { startOffset = 2, endOffset = 4, style = CompositionClauseStyle.TargetConverted },
            };
            InjectComposition("にほんご", 4, clauses);

            Assert.IsTrue(editable.isComposing);
            Assert.AreEqual(2, editable.compositionOverlay.clauseCount);
            Assert.AreEqual(CompositionClauseStyle.TargetConverted,
                editable.compositionOverlay.Clauses[1].style);
        }

        [UnityTest]
        public IEnumerator Composition_ReplacesActiveSelection()
        {
            yield return BuildField();
            yield return Seed("abcdef");
            editable.Select(1, 4);
            InjectComposition("X", 1);
            Assert.IsTrue(editable.isComposing);
            Assert.AreEqual("aef", editable.Text);
            Assert.AreEqual("X", Overlay());
        }

        [UnityTest]
        public IEnumerator Composition_SuppressesMutatingKeys()
        {
            yield return BuildField();
            yield return Seed("abc");
            InjectComposition("XY", 2);
            UniTextNativeInput.RaiseKeyDown(NativeKeyCode.Backspace, NativeModifiers.None);
            Assert.IsTrue(editable.isComposing);
            Assert.AreEqual("abc", editable.Text);
        }
    }
}
