using System.Collections.Generic;
using LightSide;
using NUnit.Framework;

namespace LightSide.Tests
{
    /// <summary>
    /// Regression coverage for the coordinate map's codepoint-space invariant: a projected range's
    /// visible width counts CODEPOINTS, so an astral replacement (a parse rule inserting "😊")
    /// must shift downstream offsets by its codepoint width, not its UTF-16 char count.
    /// </summary>
    public class CoordinateMapProjectionTests
    {
        private static CoordinateMap Build(int sourceLength, params ProjectedRange[] ranges)
        {
            var map = new CoordinateMap();
            map.Rebuild(sourceLength, new List<ProjectedRange>(ranges));
            return map;
        }

        [Test]
        public void AstralVisibleCountsOneCodepoint()
        {
            var range = new ProjectedRange(2, 7, "😊");
            Assert.AreEqual(1, range.VisibleWidth, "astral pair must count as one codepoint, not two chars");

            var map = Build(10, range);
            Assert.AreEqual(4, map.VisibleLength);
            Assert.AreEqual(2, map.SourceToVisible(5), "inside the range collapses to its visible start");
            Assert.AreEqual(3, map.SourceToVisible(9), "offset after the range shifts by hidden = length - codepointWidth");
            Assert.AreEqual(9, map.VisibleToSource(3, TagStick.Before));
            Assert.AreEqual(2, map.VisibleToSource(2, TagStick.Before), "width-bearing ranges never stick");
        }

        [Test]
        public void MixedAstralVisibleStringCountsCodepoints()
        {
            var range = new ProjectedRange(0, 3, "a😊b");
            Assert.AreEqual(3, range.VisibleWidth);
            var map = Build(3, range);
            Assert.AreEqual(3, map.VisibleLength, "3-codepoint visible over a 3-codepoint source hides nothing");
        }

        [Test]
        public void ZeroWidthTagSticksBySide()
        {
            var map = Build(10, new ProjectedRange(2, 7, string.Empty));
            Assert.AreEqual(3, map.VisibleLength);
            Assert.AreEqual(2, map.VisibleToSource(2, TagStick.Before));
            Assert.AreEqual(9, map.VisibleToSource(2, TagStick.After));
        }

        [Test]
        public void IsInsideTagBounds()
        {
            var map = Build(10, new ProjectedRange(2, 7, "😊"));
            Assert.IsTrue(map.IsInsideTag(5, out var region));
            Assert.AreEqual(2, region.start);
            Assert.AreEqual(9, region.End);
            Assert.IsFalse(map.IsInsideTag(1, out _));
            Assert.IsFalse(map.IsInsideTag(9, out _), "End is exclusive");
        }
    }
}
