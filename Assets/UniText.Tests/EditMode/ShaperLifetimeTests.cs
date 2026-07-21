using LightSide;
using NUnit.Framework;
using UnityEditor;

namespace UniText.Tests
{
    public class ShaperLifetimeTests
    {
        private UniTextFont.Core font;

        [SetUp]
        public void SetUp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniTextFont>("Assets/UniText/Defaults/NotoSans-Regular.asset");
            Assert.NotNull(asset);
            font = asset.Runtime;
            Assert.NotNull(font);
            Shaper.ClearCache(font);
        }

        [TearDown]
        public void TearDown()
        {
            Shaper.ClearCache(font);
            font = null;
        }

        [Test]
        public void ClearCache_DefersNativeDestructionUntilActiveLeaseEnds()
        {
            var entry = Shaper.GetOrCreateCoreCache(font);
            Assert.IsTrue(entry.TryAcquire(out var lease));
            try
            {
                Shaper.ClearCache(font);

                Assert.IsFalse(entry.IsValid);
                Assert.IsFalse(entry.IsDestroyed);
                Assert.IsTrue(HB.TryGetGlyph(lease.Font, 'A', out var glyph));
                Assert.AreNotEqual(0u, glyph);
            }
            finally { lease.Dispose(); }

            Assert.IsTrue(entry.IsDestroyed);
            Assert.AreNotSame(entry, Shaper.GetOrCreateCoreCache(font));
        }

        [Test]
        public void RetireVariation_DefersSubFontDestructionUntilItsLeaseEnds()
        {
            var entry = Shaper.GetOrCreateCoreCache(font);
            Assert.IsTrue(entry.TryAcquire(out var fontLease));
            var variation = new[]
            {
                new HB.hb_variation_t { tag = HB.MakeTag('w', 'g', 'h', 't'), value = 700f }
            };
            var subFontLease = entry.AcquireSubFont(fontLease, 17, variation);
            var released = false;
            try
            {
                Assert.AreNotEqual(System.IntPtr.Zero, subFontLease.Pointer);
                entry.RetireSubFont(17);
                Assert.AreNotEqual(System.IntPtr.Zero, subFontLease.Pointer);
                subFontLease.Dispose();
                released = true;
                Assert.AreEqual(System.IntPtr.Zero, subFontLease.Pointer);
            }
            finally
            {
                if (!released) subFontLease.Dispose();
                fontLease.Dispose();
            }
        }
    }
}
