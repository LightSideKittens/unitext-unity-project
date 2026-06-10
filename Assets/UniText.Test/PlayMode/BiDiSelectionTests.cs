using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class BiDiSelectionTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator DragPastVisualEnd_SelectsToLineEnd()
        {
            yield return BuildField();
            yield return Seed("abc" + (char)0x05D0 + (char)0x05D1 + (char)0x05D2);

            var lines = uniText.Buffers.lines;
            Assert.GreaterOrEqual(lines.count, 1);

            var glyphs = uniText.ResultGlyphs;
            var hasHebrewGlyph = false;
            for (var i = 0; i < glyphs.Length; i++)
                if (glyphs[i].cluster >= 3) { hasHebrewGlyph = true; break; }
            if (!hasHebrewGlyph)
                Assert.Inconclusive("Default font produced no Hebrew glyphs; the BiDi selection test needs them.");

            var pastRight = SelectionHitTest.FindCodepointAtX(uniText, 0, 100000f, lines);
            var pastLeft = SelectionHitTest.FindCodepointAtX(uniText, 0, -100000f, lines);

            Assert.AreEqual(6, pastRight,
                "drag/click past the visual end of an LTR line must reach the logical line end, not the rightmost glyph's logical edge");
            Assert.AreEqual(0, pastLeft, "drag/click past the visual start must reach the logical line start");

            editable.Select(0, pastRight);
            Assert.AreEqual(0, editable.SelectionStart);
            Assert.AreEqual(6, editable.SelectionEnd);
            Assert.AreEqual(6, editable.SelectedText.Length);
        }
    }
}
