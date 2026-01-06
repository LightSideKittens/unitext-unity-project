#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LightSide
{
    /// <summary>
    /// Test for COLRv1 emoji rendering using FreeType + Blend2D.
    /// </summary>
    internal class COLRv1Test : MonoBehaviour
    {
        [SerializeField] private int emojiSize = 128;
        [SerializeField] private string testFontPath = "";

        private Texture2D texture;
        private Material material;
        private COLRv1Renderer renderer;
        private IntPtr face;

        private void Start()
        {
            if (!BL.IsSupported)
            {
                Debug.LogError("[COLRv1Test] Blend2D not supported");
                return;
            }

            string fontPath = testFontPath;
            if (string.IsNullOrEmpty(fontPath))
            {
                fontPath = SystemEmojiFont.GetDefaultEmojiFont();
            }

            if (string.IsNullOrEmpty(fontPath))
            {
                Debug.LogError("[COLRv1Test] No emoji font found");
                return;
            }

            Debug.Log($"[COLRv1Test] Testing font: {fontPath}");

            byte[] fontData;
            try
            {
                fontData = System.IO.File.ReadAllBytes(fontPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[COLRv1Test] Failed to load font: {ex.Message}");
                return;
            }

            face = FT.LoadFace(fontData, 0);
            if (face == IntPtr.Zero)
            {
                Debug.LogError("[COLRv1Test] Failed to create FreeType face");
                return;
            }

            var faceInfo = FT.GetFaceInfo(face);
            Debug.Log($"[COLRv1Test] Font loaded: glyphs={faceInfo.numGlyphs}, upem={faceInfo.unitsPerEm}, " +
                      $"hasColor={faceInfo.HasColor}, hasSVG={faceInfo.HasSVG}, hasSbix={faceInfo.HasSbix}");

            uint testCodepoint = 0x1F600;
            uint glyphIndex = FT.GetCharIndex(face, testCodepoint);

            if (glyphIndex == 0)
            {
                Debug.LogError($"[COLRv1Test] Glyph not found for U+{testCodepoint:X}");
                return;
            }

            FT.DebugColorGlyphPaint(face, glyphIndex, out bool hasColr, out bool hasCpal, out int ftResult);
            Debug.Log($"[COLRv1Test] Debug: hasColr={hasColr}, hasCpal={hasCpal}, ftResult={ftResult}");

            bool hasCOLRv1 = FT.HasColorGlyphPaint(face, glyphIndex);
            Debug.Log($"[COLRv1Test] Glyph {glyphIndex} (U+{testCodepoint:X}): hasCOLRv1={hasCOLRv1}");

            if (!hasCOLRv1)
            {
                Debug.LogWarning("[COLRv1Test] Font does not have COLRv1 data. Using standard rendering.");
                TestStandardRendering(glyphIndex);
                return;
            }

            renderer = new COLRv1Renderer(face);

            bool stillHasCOLRv1 = FT.HasColorGlyphPaint(face, glyphIndex);
            Debug.Log($"[COLRv1Test] After renderer creation: hasCOLRv1={stillHasCOLRv1}");

            bool gotPaint = FT.GetColorGlyphPaint(face, glyphIndex, true, out var testPaint);
            Debug.Log($"[COLRv1Test] Direct GetColorGlyphPaint: success={gotPaint}, p={testPaint.p}, insert={testPaint.insert_root_transform}");

            texture = new Texture2D(emojiSize, emojiSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(4, 4, 1);

            var meshRenderer = go.GetComponent<MeshRenderer>();
            material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTexture = texture;
            meshRenderer.material = material;

            TestCOLRv1Rendering(glyphIndex);
        }

        [ContextMenu("Test COLRv1 Rendering")]
        private void TestCOLRv1Rendering(uint glyphIndex)
        {
            if (renderer == null)
            {
                Debug.LogError("[COLRv1Test] Renderer not initialized");
                return;
            }

            var sw = Stopwatch.StartNew();

            if (renderer.TryRenderGlyph(glyphIndex, emojiSize, out var pixels, out int width, out int height, out _, out _))
            {
                sw.Stop();
                Debug.Log($"[COLRv1Test] Rendered glyph {glyphIndex} in {sw.ElapsedMilliseconds}ms ({width}x{height})");

                if (texture.width != width || texture.height != height)
                {
                    texture.Reinitialize(width, height);
                }

                var flipped = new byte[pixels.Length];
                int rowBytes = width * 4;
                for (int y = 0; y < height; y++)
                {
                    int srcY = height - 1 - y;
                    Array.Copy(pixels, srcY * rowBytes, flipped, y * rowBytes, rowBytes);
                }

                texture.LoadRawTextureData(flipped);
                texture.Apply();
            }
            else
            {
                sw.Stop();
                Debug.LogError($"[COLRv1Test] Failed to render glyph {glyphIndex} ({sw.ElapsedMilliseconds}ms)");
            }
        }

        private void TestStandardRendering(uint glyphIndex)
        {
            Debug.Log("[COLRv1Test] Using standard FreeType rendering");

            FT.SetPixelSize(face, emojiSize);

            if (!FT.LoadGlyph(face, glyphIndex, FT.LOAD_COLOR))
            {
                Debug.LogError("[COLRv1Test] Failed to load glyph");
                return;
            }

            if (!FT.RenderGlyph(face, FT.RENDER_MODE_NORMAL))
            {
                Debug.LogError("[COLRv1Test] Failed to render glyph");
                return;
            }

            var bitmap = FT.GetBitmapData(face);
            Debug.Log($"[COLRv1Test] Standard render: {bitmap.width}x{bitmap.height}, mode={bitmap.pixelMode}");

            if (bitmap.width > 0 && bitmap.height > 0)
            {
                var pixels = FT.GetBitmapRGBA(face, out _);
                if (pixels != null)
                {
                    texture = new Texture2D(bitmap.width, bitmap.height, TextureFormat.RGBA32, false);
                    texture.filterMode = FilterMode.Bilinear;

                    var flipped = new byte[bitmap.width * bitmap.height * 4];
                    int rowBytes = bitmap.width * 4;
                    for (int y = 0; y < bitmap.height; y++)
                    {
                        int srcY = bitmap.height - 1 - y;
                        Array.Copy(pixels, srcY * rowBytes, flipped, y * rowBytes, rowBytes);
                    }

                    texture.LoadRawTextureData(flipped);
                    texture.Apply();

                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.transform.SetParent(transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = new Vector3(4, 4, 1);

                    var meshRenderer = go.GetComponent<MeshRenderer>();
                    material = new Material(Shader.Find("Unlit/Transparent"));
                    material.mainTexture = texture;
                    meshRenderer.material = material;
                }
            }
        }

        [ContextMenu("Test Batch Rendering")]
        private void TestBatchRendering()
        {
            if (renderer == null) return;

            uint[] testCodepoints = {
                0x1F600, 0x1F601, 0x1F602, 0x1F603, 0x1F604, 0x1F605, 0x1F606, 0x1F607, 0x1F608, 0x1F609, 0x2764, 0x1F496, 0x1F389,
            };

            var sw = Stopwatch.StartNew();
            int rendered = 0;

            foreach (uint cp in testCodepoints)
            {
                uint glyph = FT.GetCharIndex(face, cp);
                if (glyph == 0) continue;

                if (renderer.TryRenderGlyph(glyph, emojiSize, out _, out _, out _, out _, out _))
                {
                    rendered++;
                }
            }

            sw.Stop();
            Debug.Log($"[COLRv1Test] Batch rendered {rendered}/{testCodepoints.Length} emojis in {sw.ElapsedMilliseconds}ms " +
                      $"({sw.ElapsedMilliseconds / (float)rendered:F2}ms per emoji)");
        }
        
        private void OnDestroy()
        {
            renderer?.Dispose();

            if (face != IntPtr.Zero)
            {
                FT.UnloadFace(face);
                face = IntPtr.Zero;
            }

            if (texture != null)
                Destroy(texture);
            if (material != null)
                Destroy(material);
        }
    }
}
#endif
