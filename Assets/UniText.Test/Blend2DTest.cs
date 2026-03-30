using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LightSide
{
    /// <summary>
    /// Stress test for Blend2D: renders 4000 emoji-like elements.
    /// Each emoji has ~8 layers: base circle, face features, gradients, shadows.
    /// Total operations: ~32000 paths, ~8000 gradients.
    /// </summary>
    public class Blend2DTest : MonoBehaviour
    {
        [SerializeField] private int emojiCount = 4000;
        [SerializeField] private int emojiSize = 64;
        [SerializeField] private int atlasColumns = 64;

        private Texture2D texture;
        private Material material;

        private IntPtr reusablePath;

        private void Start()
        {
            if (!BL.IsSupported)
            {
                Debug.LogError("[Blend2DTest] Blend2D not supported on this platform");
                return;
            }

            int atlasSize = atlasColumns * emojiSize;
            texture = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(8, 8, 1);

            var renderer = go.GetComponent<MeshRenderer>();
            material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTexture = texture;
            renderer.material = material;

            RenderStressTest();
        }

        [ContextMenu("Render Stress Test")]
        private void RenderStressTest()
        {
            if (!BL.IsSupported) return;

            int atlasSize = atlasColumns * emojiSize;
            int rows = (emojiCount + atlasColumns - 1) / atlasColumns;

            Debug.Log($"[Blend2DTest] Starting stress test: {emojiCount} emojis, {emojiSize}x{emojiSize} each, atlas {atlasSize}x{atlasSize}");

            var swTotal = Stopwatch.StartNew();
            var swCreate = Stopwatch.StartNew();

            IntPtr img = BL.ImageCreate(atlasSize, atlasSize, BL.FORMAT_PRGB32);
            if (img == IntPtr.Zero)
            {
                Debug.LogError("[Blend2DTest] Failed to create image");
                return;
            }

            IntPtr ctx = BL.ContextCreate(img);
            if (ctx == IntPtr.Zero)
            {
                Debug.LogError("[Blend2DTest] Failed to create context");
                BL.ImageDestroy(img);
                return;
            }

            reusablePath = BL.PathCreate();

            swCreate.Stop();
            var swClear = Stopwatch.StartNew();

            BL.ContextSetFillStyleRgba32(ctx, BL.Rgba32(0, 0, 0, 0));
            BL.ContextFillAll(ctx);

            swClear.Stop();
            var swRender = Stopwatch.StartNew();

            int rendered = 0;
            for (int i = 0; i < emojiCount; i++)
            {
                int col = i % atlasColumns;
                int row = i / atlasColumns;
                float x = col * emojiSize;
                float y = row * emojiSize;

                RenderEmoji(ctx, x, y, emojiSize, i);
                rendered++;
            }

            swRender.Stop();
            var swFlush = Stopwatch.StartNew();

            BL.ContextEnd(ctx);

            swFlush.Stop();
            var swCopy = Stopwatch.StartNew();

            IntPtr dataPtr = BL.ImageGetData(img, out int stride);

            if (dataPtr != IntPtr.Zero)
            {
                int pixelCount = atlasSize * atlasSize;
                int byteCount = pixelCount * 4;

                var result = new byte[byteCount];

                unsafe
                {
                    fixed (byte* resultPtr = result)
                    {
                        byte* src = (byte*)dataPtr;
                        byte* dst = resultPtr;
                        int width = atlasSize;
                        int height = atlasSize;
                        int srcStride = stride;

                        Parallel.For(0, height, y =>
                        {
                            int srcY = height - 1 - y;
                            byte* srcRow = src + srcY * srcStride;
                            byte* dstRow = dst + y * width * 4;

                            int x = 0;

                            for (; x + 3 < width; x += 4)
                            {
                                byte* s = srcRow + x * 4;
                                byte* d = dstRow + x * 4;

                                d[0] = s[2]; d[1] = s[1]; d[2] = s[0]; d[3] = s[3];
                                d[4] = s[6]; d[5] = s[5]; d[6] = s[4]; d[7] = s[7];
                                d[8] = s[10]; d[9] = s[9]; d[10] = s[8]; d[11] = s[11];
                                d[12] = s[14]; d[13] = s[13]; d[14] = s[12]; d[15] = s[15];
                            }

                            for (; x < width; x++)
                            {
                                byte* s = srcRow + x * 4;
                                byte* d = dstRow + x * 4;
                                d[0] = s[2]; d[1] = s[1]; d[2] = s[0]; d[3] = s[3];
                            }
                        });
                    }
                }

                texture.LoadRawTextureData(result);
                texture.Apply();
            }

            swCopy.Stop();
            var swCleanup = Stopwatch.StartNew();

            BL.PathDestroy(reusablePath);
            BL.ContextDestroy(ctx);
            BL.ImageDestroy(img);

            swCleanup.Stop();
            swTotal.Stop();

            Debug.Log($"[Blend2DTest] Rendered {rendered} emojis in {swTotal.ElapsedMilliseconds}ms");
            Debug.Log($"[Blend2DTest] Breakdown: Create={swCreate.ElapsedMilliseconds}ms, Clear={swClear.ElapsedMilliseconds}ms, " +
                      $"Render={swRender.ElapsedMilliseconds}ms, Flush={swFlush.ElapsedMilliseconds}ms, " +
                      $"Copy={swCopy.ElapsedMilliseconds}ms, Cleanup={swCleanup.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Renders a single emoji-like element with realistic complexity:
        /// - 1 background circle with radial gradient (face base)
        /// - 2 eye circles
        /// - 2 eye highlights
        /// - 1 mouth (filled arc)
        /// - 2 blush circles with gradient
        /// Total: ~8 drawing operations, 3 gradients, 8 paths per emoji
        /// </summary>
        private void RenderEmoji(IntPtr ctx, float x, float y, int size, int seed)
        {
            float cx = x + size * 0.5f;
            float cy = y + size * 0.5f;
            float radius = size * 0.45f;

            uint baseHue = (uint)(seed * 37) % 360;
            uint faceColor = HslToRgba32(baseHue, 80, 70, 255);
            uint faceColorDark = HslToRgba32(baseHue, 80, 50, 255);
            uint eyeColor = BL.Rgba32(40, 40, 40, 255);
            uint eyeHighlight = BL.Rgba32(255, 255, 255, 255);
            uint mouthColor = BL.Rgba32(80, 40, 40, 255);
            uint blushColor = BL.Rgba32(255, 100, 100, 100);

            IntPtr faceGrad = BL.GradientCreateRadial(cx - radius * 0.3f, cy - radius * 0.3f, cx, cy, radius);
            BL.GradientAddStop(faceGrad, 0.0, faceColor);
            BL.GradientAddStop(faceGrad, 1.0, faceColorDark);
            BL.ContextSetFillStyleGradient(ctx, faceGrad);
            FillCircle(ctx, cx, cy, radius);
            BL.GradientDestroy(faceGrad);

            float eyeOffsetX = radius * 0.35f;
            float eyeOffsetY = radius * 0.15f;
            float eyeRadius = radius * 0.15f;
            BL.ContextSetFillStyleRgba32(ctx, eyeColor);
            FillCircle(ctx, cx - eyeOffsetX, cy - eyeOffsetY, eyeRadius);

            FillCircle(ctx, cx + eyeOffsetX, cy - eyeOffsetY, eyeRadius);

            BL.ContextSetFillStyleRgba32(ctx, eyeHighlight);
            FillCircle(ctx, cx - eyeOffsetX - eyeRadius * 0.3f, cy - eyeOffsetY - eyeRadius * 0.3f, eyeRadius * 0.35f);

            FillCircle(ctx, cx + eyeOffsetX - eyeRadius * 0.3f, cy - eyeOffsetY - eyeRadius * 0.3f, eyeRadius * 0.35f);

            float mouthY = cy + radius * 0.3f;
            float mouthWidth = radius * 0.4f;
            float mouthHeight = radius * 0.15f * ((seed % 3));
            if (mouthHeight > 0)
            {
                BL.ContextSetFillStyleRgba32(ctx, mouthColor);
                BL.PathClear(reusablePath);
                BL.PathMoveTo(reusablePath, cx - mouthWidth, mouthY);
                BL.PathCubicTo(reusablePath,
                    cx - mouthWidth * 0.5f, mouthY + mouthHeight * 2,
                    cx + mouthWidth * 0.5f, mouthY + mouthHeight * 2,
                    cx + mouthWidth, mouthY);
                BL.PathCubicTo(reusablePath,
                    cx + mouthWidth * 0.3f, mouthY + mouthHeight * 0.5f,
                    cx - mouthWidth * 0.3f, mouthY + mouthHeight * 0.5f,
                    cx - mouthWidth, mouthY);
                BL.PathClose(reusablePath);
                BL.ContextFillPath(ctx, reusablePath);
            }

            IntPtr blushGrad = BL.GradientCreateRadial(
                cx - radius * 0.55f, cy + radius * 0.1f,
                cx - radius * 0.55f, cy + radius * 0.1f,
                radius * 0.18f);
            BL.GradientAddStop(blushGrad, 0.0, blushColor);
            BL.GradientAddStop(blushGrad, 1.0, BL.Rgba32(255, 100, 100, 0));
            BL.ContextSetFillStyleGradient(ctx, blushGrad);
            FillCircle(ctx, cx - radius * 0.55f, cy + radius * 0.1f, radius * 0.18f);

            BL.GradientResetStops(blushGrad);
            BL.GradientAddStop(blushGrad, 0.0, blushColor);
            BL.GradientAddStop(blushGrad, 1.0, BL.Rgba32(255, 100, 100, 0));
            FillCircle(ctx, cx + radius * 0.55f, cy + radius * 0.1f, radius * 0.18f);
            BL.GradientDestroy(blushGrad);
        }

        private void FillCircle(IntPtr ctx, float cx, float cy, float radius)
        {
            BL.PathClear(reusablePath);
            const float k = 0.5522847498f;
            float kRadius = k * radius;

            BL.PathMoveTo(reusablePath, cx + radius, cy);
            BL.PathCubicTo(reusablePath, cx + radius, cy + kRadius, cx + kRadius, cy + radius, cx, cy + radius);
            BL.PathCubicTo(reusablePath, cx - kRadius, cy + radius, cx - radius, cy + kRadius, cx - radius, cy);
            BL.PathCubicTo(reusablePath, cx - radius, cy - kRadius, cx - kRadius, cy - radius, cx, cy - radius);
            BL.PathCubicTo(reusablePath, cx + kRadius, cy - radius, cx + radius, cy - kRadius, cx + radius, cy);
            BL.PathClose(reusablePath);

            BL.ContextFillPath(ctx, reusablePath);
        }

        private static uint HslToRgba32(uint h, uint s, uint l, byte a)
        {
            float hf = h / 360f;
            float sf = s / 100f;
            float lf = l / 100f;

            float c = (1 - Math.Abs(2 * lf - 1)) * sf;
            float x = c * (1 - Math.Abs((hf * 6) % 2 - 1));
            float m = lf - c / 2;

            float r, g, b;
            int hi = (int)(hf * 6) % 6;
            switch (hi)
            {
                case 0: r = c; g = x; b = 0; break;
                case 1: r = x; g = c; b = 0; break;
                case 2: r = 0; g = c; b = x; break;
                case 3: r = 0; g = x; b = c; break;
                case 4: r = x; g = 0; b = c; break;
                default: r = c; g = 0; b = x; break;
            }

            return BL.Rgba32(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255),
                a);
        }

        private void OnDestroy()
        {
            if (texture != null)
                Destroy(texture);
            if (material != null)
                Destroy(material);
        }
    }
}
