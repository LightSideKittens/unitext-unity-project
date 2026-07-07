using System;
using LightSide;
using NUnit.Framework;

namespace UniText.Tests
{
    public class XxHash64Tests
    {
        [Test]
        public void Hash_IsDeterministic_AndContentSensitive()
        {
            var a = new[] { 1, 2, 3, 4, 5 };
            var b = new[] { 1, 2, 3, 4, 6 };

            var h1 = XxHash64.Hash<int>(a, 7);
            var h2 = XxHash64.Hash<int>(a, 7);
            Assert.AreEqual(h1, h2);
            Assert.AreNotEqual(h1, XxHash64.Hash<int>(b, 7));
            Assert.AreNotEqual(h1, XxHash64.Hash<int>(a, 8));
        }

        [Test]
        public void Hash_TailBytes_Differ()
        {
            var a = new byte[] { 1, 2, 3 };
            var b = new byte[] { 1, 2, 4 };
            Assert.AreNotEqual(XxHash64.HashBytes(a, 0), XxHash64.HashBytes(b, 0));
        }

        [Test]
        public void Hash_LongSpans_UseStripes()
        {
            var a = new int[100];
            var b = new int[100];
            for (var i = 0; i < 100; i++) a[i] = b[i] = i;
            b[97] = -1;
            Assert.AreEqual(XxHash64.Hash<int>(a, 1), XxHash64.Hash<int>(new ReadOnlySpan<int>(a), 1));
            Assert.AreNotEqual(XxHash64.Hash<int>(a, 1), XxHash64.Hash<int>(b, 1));
        }
    }

    public class ParagraphShapeCacheTests
    {
        private static ShapedGlyph Glyph(int cluster, float advance) => new() { cluster = cluster, advanceX = advance, glyphId = cluster + 100 };

        private static ShapedRun Run(int start, int length, int glyphStart, int glyphCount) => new()
        {
            range = new TextRange(start, length),
            glyphStart = glyphStart,
            glyphCount = glyphCount,
        };

        private static (PooledBuffer<ShapedRun> runs, PooledBuffer<ShapedGlyph> glyphs) ShapeParagraph(int cpStart, int cpCount, int glyphBase)
        {
            var runs = new PooledBuffer<ShapedRun>();
            var glyphs = new PooledBuffer<ShapedGlyph>();
            for (var i = 0; i < cpCount; i++)
                glyphs.Add(Glyph(cpStart + i, 10f + i));
            runs.Add(Run(cpStart, cpCount, glyphBase, cpCount));
            return (runs, glyphs);
        }

        [Test]
        public void StoreThenHit_RebasesToNewPosition()
        {
            var cache = new ParagraphShapeCache();

            cache.BeginPass(1);
            var (srcRuns, srcGlyphs) = ShapeParagraph(cpStart: 10, cpCount: 3, glyphBase: 5);
            cache.Store(hash: 42, cpStart: 10, cpCount: 3, srcRuns.Span, glyphBase: 5, srcGlyphs.Span);
            cache.EndPass();

            cache.BeginPass(1);
            var outRuns = new PooledBuffer<ShapedRun>();
            var outGlyphs = new PooledBuffer<ShapedGlyph>();
            outGlyphs.Add(Glyph(0, 1f));

            Assert.IsTrue(cache.TryAppend(hash: 42, cpCount: 3, cpStart: 20, ref outRuns, ref outGlyphs));

            Assert.AreEqual(4, outGlyphs.count);
            Assert.AreEqual(20, outGlyphs[1].cluster);
            Assert.AreEqual(22, outGlyphs[3].cluster);
            Assert.AreEqual(11f, outGlyphs[2].advanceX);

            Assert.AreEqual(1, outRuns.count);
            Assert.AreEqual(20, outRuns[0].range.start);
            Assert.AreEqual(3, outRuns[0].range.length);
            Assert.AreEqual(1, outRuns[0].glyphStart);
            cache.EndPass();
            cache.Clear();
        }

        [Test]
        public void Hit_RejectsDifferentLength_AndIsSingleUsePerPass()
        {
            var cache = new ParagraphShapeCache();

            cache.BeginPass(1);
            var (runs, glyphs) = ShapeParagraph(0, 2, 0);
            cache.Store(7, 0, 2, runs.Span, 0, glyphs.Span);
            cache.EndPass();

            cache.BeginPass(2);
            var outRuns = new PooledBuffer<ShapedRun>();
            var outGlyphs = new PooledBuffer<ShapedGlyph>();

            Assert.IsFalse(cache.TryAppend(7, cpCount: 3, cpStart: 0, ref outRuns, ref outGlyphs), "length mismatch must miss");
            Assert.IsTrue(cache.TryAppend(7, cpCount: 2, cpStart: 0, ref outRuns, ref outGlyphs));
            Assert.IsFalse(cache.TryAppend(7, cpCount: 2, cpStart: 4, ref outRuns, ref outGlyphs), "entries are single-use per pass");
            cache.EndPass();
            cache.Clear();
        }

        [Test]
        public void UnconsumedEntries_AreEvictedAtEndPass()
        {
            var cache = new ParagraphShapeCache();

            cache.BeginPass(1);
            var (runs, glyphs) = ShapeParagraph(0, 2, 0);
            cache.Store(11, 0, 2, runs.Span, 0, glyphs.Span);
            cache.EndPass();

            cache.BeginPass(0);
            cache.EndPass();

            cache.BeginPass(1);
            var outRuns = new PooledBuffer<ShapedRun>();
            var outGlyphs = new PooledBuffer<ShapedGlyph>();
            Assert.IsFalse(cache.TryAppend(11, 2, 0, ref outRuns, ref outGlyphs), "entry not carried through a pass that skipped it");
            cache.EndPass();
            cache.Clear();
        }
    }

    /// <summary>
    /// Identity contract of the paragraph-sequenced wrap fold: splitting the same buffers into
    /// bidi paragraphs must produce exactly the lines the old single-pass wrap produced. Inputs
    /// are synthetic (no fonts, no shaping) — the breaker only reads spans.
    /// </summary>
    public class LineBreakerParagraphTests
    {
        private static void BuildText(string text, out int[] cps, out float[] widths, out LineBreakType[] breaks,
            out ShapedRun[] runs, out ShapedGlyph[] glyphs, out Paragraph[] paragraphs)
        {
            UnicodeData.EnsureInitialized();
            Assume.That(UnicodeData.IsInitialized, "UnicodeData tables unavailable in this editor context");

            var cpArr = new int[text.Length];
            var widthArr = new float[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                cpArr[i] = text[i];
                widthArr[i] = text[i] == '\n' ? 0f : 10f;
            }

            breaks = new LineBreakType[text.Length + 1];
            SharedPipelineComponents.LineBreakAlgorithm.GetBreakOpportunities(cpArr, breaks);

            var glyphList = new System.Collections.Generic.List<ShapedGlyph>();
            var runList = new System.Collections.Generic.List<ShapedRun>();
            var paraList = new System.Collections.Generic.List<Paragraph>();
            var paraStart = 0;
            var runStartCp = 0;
            var runGlyphStart = 0;

            void CloseRun(int endCp)
            {
                if (endCp <= runStartCp) return;
                var count = 0;
                for (var i = runStartCp; i < endCp; i++)
                {
                    glyphList.Add(new ShapedGlyph { cluster = i, advanceX = widthArr[i], glyphId = cpArr[i] });
                    count++;
                }
                runList.Add(new ShapedRun
                {
                    range = new TextRange(runStartCp, endCp - runStartCp),
                    glyphStart = runGlyphStart,
                    glyphCount = count,
                    width = count * 10f,
                    direction = TextDirection.LeftToRight,
                });
                runGlyphStart += count;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                CloseRun(i);
                runStartCp = i + 1;
                paraList.Add(new Paragraph { cpStart = paraStart, cpCount = i - paraStart + 1 });
                paraStart = i + 1;
            }
            CloseRun(text.Length);
            if (paraStart < text.Length || paraList.Count == 0)
                paraList.Add(new Paragraph { cpStart = paraStart, cpCount = text.Length - paraStart });

            cps = cpArr;
            widths = widthArr;
            runs = runList.ToArray();
            glyphs = glyphList.ToArray();
            paragraphs = paraList.ToArray();
        }

        private static (TextLine[] lines, int count, Paragraph[] table) Break(string text, float maxWidth, bool splitParagraphs)
        {
            BuildText(text, out var cps, out var widths, out var breaks, out var runs, out var glyphs, out var paragraphs);

            if (!splitParagraphs)
            {
                var single = new[] { new Paragraph { cpStart = 0, cpCount = cps.Length } };
                paragraphs = single;
            }

            var breaker = new LineBreaker();
            TextLine[] lines = null;
            ShapedRun[] ordered = null;
            var lineCount = 0;
            var orderedCount = 0;
            breaker.BreakLines(cps, runs, glyphs, widths, breaks, ReadOnlySpan<TextRange>.Empty,
                maxWidth, paragraphs, ref lines, ref lineCount, ref ordered, ref orderedCount,
                ReadOnlySpan<float>.Empty);
            return (lines, lineCount, paragraphs);
        }

        [Test]
        public void SplitByParagraphs_ProducesIdenticalLines()
        {
            const string text = "aaa bbb ccc\ndd ee\n\nfff ggg hhh iii";
            foreach (var width in new[] { 55f, 80f, 200f, 10000f })
            {
                var (wholeLines, wholeCount, _) = Break(text, width, splitParagraphs: false);
                var (splitLines, splitCount, _) = Break(text, width, splitParagraphs: true);

                Assert.AreEqual(wholeCount, splitCount, $"line count @ width {width}");
                for (var i = 0; i < wholeCount; i++)
                {
                    Assert.AreEqual(wholeLines[i].range.start, splitLines[i].range.start, $"line {i} start @ {width}");
                    Assert.AreEqual(wholeLines[i].range.length, splitLines[i].range.length, $"line {i} length @ {width}");
                    Assert.AreEqual(wholeLines[i].width, splitLines[i].width, 1e-3f, $"line {i} width @ {width}");
                    Assert.AreEqual(wholeLines[i].endedByMandatoryBreak, splitLines[i].endedByMandatoryBreak, $"line {i} mandatory @ {width}");
                }
            }
        }

        [Test]
        public void ParagraphLineSlices_PartitionAllLines()
        {
            var (lines, lineCount, table) = Break("aa bb\ncc\n\ndd ee ff", 45f, splitParagraphs: true);

            var covered = 0;
            for (var p = 0; p < table.Length; p++)
            {
                Assert.AreEqual(covered, table[p].lineStart, $"paragraph {p} lineStart contiguity");
                covered += table[p].lineCount;
                for (var l = table[p].lineStart; l < table[p].lineStart + table[p].lineCount; l++)
                    Assert.GreaterOrEqual(lines[l].range.start, table[p].cpStart, $"line {l} belongs to paragraph {p}");
            }
            Assert.AreEqual(lineCount, covered, "line slices cover every line");
        }

        [Test]
        public void TrailingNewline_EmitsSyntheticEmptyLine()
        {
            var (lines, lineCount, _) = Break("abc\n", 10000f, splitParagraphs: true);
            Assert.AreEqual(2, lineCount);
            Assert.AreEqual(4, lines[1].range.start);
            Assert.AreEqual(0, lines[1].range.length);
        }

        [Test]
        public void TrailingEmptyLine_AdoptsLastParagraphDirection()
        {
            BuildText("ab\ncd\n", out var cps, out var widths, out var breaks, out var runs, out var glyphs, out var paragraphs);
            paragraphs[1].baseLevel = 1;

            var breaker = new LineBreaker();
            TextLine[] lines = null;
            ShapedRun[] ordered = null;
            var lineCount = 0;
            var orderedCount = 0;
            breaker.BreakLines(cps, runs, glyphs, widths, breaks, ReadOnlySpan<TextRange>.Empty,
                10000f, paragraphs, ref lines, ref lineCount, ref ordered, ref orderedCount,
                ReadOnlySpan<float>.Empty);

            Assert.AreEqual(3, lineCount);
            Assert.AreEqual(1, lines[2].paragraphBaseLevel, "trailing caret line continues the adjacent paragraph's direction");
        }
    }
}
