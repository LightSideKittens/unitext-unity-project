using LightSide;
using NUnit.Framework;

namespace LightSide.Tests
{
    /// <summary>
    /// Contract coverage for <see cref="ContourUnionBurst.TryResolve"/> on synthetic quadratic
    /// geometry: clean input passes through resolved, holes survive, redundant same-winding
    /// shells are removed, degenerate input and the field valve bail to the legacy path, and
    /// resolution is idempotent. Numeric SDF-field comparison against the legacy rasterizer
    /// lives with the PlayMode suite; these guard the resolver's outward promises.
    /// </summary>
    public unsafe class ContourUnionTests
    {
        private static void AddLine(ref PooledBuffer<GlyphCurveCache.Segment> buf,
            float x0, float y0, float x1, float y1, byte contour)
        {
            buf.Add(new GlyphCurveCache.Segment
            {
                p0x = x0, p0y = y0,
                p1x = (x0 + x1) * 0.5f, p1y = (y0 + y1) * 0.5f,
                p2x = x1, p2y = y1,
                contourIndex = contour,
            });
        }

        private static void AddSquare(ref PooledBuffer<GlyphCurveCache.Segment> buf,
            float min, float max, bool ccw, byte contour)
        {
            if (ccw)
            {
                AddLine(ref buf, min, min, max, min, contour);
                AddLine(ref buf, max, min, max, max, contour);
                AddLine(ref buf, max, max, min, max, contour);
                AddLine(ref buf, min, max, min, min, contour);
            }
            else
            {
                AddLine(ref buf, min, min, min, max, contour);
                AddLine(ref buf, min, max, max, max, contour);
                AddLine(ref buf, max, max, max, min, contour);
                AddLine(ref buf, max, min, min, min, contour);
            }
        }

        private static bool Resolve(ref PooledBuffer<GlyphCurveCache.Segment> buf,
            ref int segCount, int[] contourEnds, ref int contourCount)
        {
            fixed (int* rawContours = contourEnds)
            {
                return ContourUnionBurst.TryResolve(ref buf, 0, ref segCount, rawContours, ref contourCount);
            }
        }

        [Test]
        public void PlainSquare_ResolvesAndStampsFlag()
        {
            var buf = new PooledBuffer<GlyphCurveCache.Segment>();
            try
            {
                buf.Rent(16);
                AddSquare(ref buf, 0f, 10f, ccw: true, contour: 0);
                int segCount = 4, contourCount = 1;
                var ends = new[] { 3, 0, 0, 0 };

                Assert.IsTrue(Resolve(ref buf, ref segCount, ends, ref contourCount));
                Assert.AreEqual(4, segCount, "clean geometry must pass through unchanged");
                Assert.AreEqual(1, contourCount);
                for (int i = 0; i < segCount; i++)
                    Assert.AreNotEqual(0, buf.data[i].rasterFlags & GlyphCurveCache.Segment.FlagResolved,
                        "every segment of a resolved glyph carries FlagResolved");
            }
            finally { buf.Return(); }
        }

        [Test]
        public void OppositeWindingHole_Survives()
        {
            var buf = new PooledBuffer<GlyphCurveCache.Segment>();
            try
            {
                buf.Rent(16);
                AddSquare(ref buf, 0f, 10f, ccw: true, contour: 0);
                AddSquare(ref buf, 3f, 7f, ccw: false, contour: 1);
                int segCount = 8, contourCount = 2;
                var ends = new[] { 3, 7, 0, 0 };

                Assert.IsTrue(Resolve(ref buf, ref segCount, ends, ref contourCount));
                Assert.AreEqual(2, contourCount, "a hole (opposite winding) must never be removed");
                Assert.AreEqual(8, segCount);
            }
            finally { buf.Return(); }
        }

        [Test]
        public void SameWindingNestedShell_IsRemoved()
        {
            var buf = new PooledBuffer<GlyphCurveCache.Segment>();
            try
            {
                buf.Rent(16);
                AddSquare(ref buf, 0f, 10f, ccw: true, contour: 0);
                AddSquare(ref buf, 3f, 7f, ccw: true, contour: 1);
                int segCount = 8, contourCount = 2;
                var ends = new[] { 3, 7, 0, 0 };

                Assert.IsTrue(Resolve(ref buf, ref segCount, ends, ref contourCount),
                    "the redundant-shell case is the resolver's core job");
                Assert.AreEqual(1, contourCount, "a buried same-winding shell must be removed");
                Assert.AreEqual(4, segCount);
            }
            finally { buf.Return(); }
        }

        [Test]
        public void Idempotent_SecondResolveKeepsResult()
        {
            var buf = new PooledBuffer<GlyphCurveCache.Segment>();
            try
            {
                buf.Rent(16);
                AddSquare(ref buf, 0f, 10f, ccw: true, contour: 0);
                AddSquare(ref buf, 3f, 7f, ccw: true, contour: 1);
                int segCount = 8, contourCount = 2;
                var ends = new[] { 3, 7, 0, 0 };
                Assert.IsTrue(Resolve(ref buf, ref segCount, ends, ref contourCount));

                var endsAfter = new[] { segCount - 1, 0, 0, 0 };
                int segCount2 = segCount, contourCount2 = contourCount;
                Assert.IsTrue(Resolve(ref buf, ref segCount2, endsAfter, ref contourCount2));
                Assert.AreEqual(segCount, segCount2, "resolving resolved geometry must be a no-op");
                Assert.AreEqual(contourCount, contourCount2);
            }
            finally { buf.Return(); }
        }

        [Test]
        public void DegenerateInput_BailsToLegacy()
        {
            var buf = new PooledBuffer<GlyphCurveCache.Segment>();
            try
            {
                buf.Rent(4);
                int segCount = 0, contourCount = 0;
                var ends = new[] { 0 };
                Assert.IsFalse(Resolve(ref buf, ref segCount, ends, ref contourCount));
            }
            finally { buf.Return(); }
        }
    }
}
