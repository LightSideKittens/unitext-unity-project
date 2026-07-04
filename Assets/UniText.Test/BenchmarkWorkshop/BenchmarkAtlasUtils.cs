using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Atlas-content checksum for benchmark honesty diagnostics: reads a corner region of the
/// texture through GPU readback, so it works for RenderTextures and non-CPU-readable
/// dynamic atlases alike. Returns 0 when readback is unsupported or fails.
/// </summary>
static class BenchmarkAtlasUtils
{
    public static long Checksum(Texture tex)
    {
        if (tex == null || !SystemInfo.supportsAsyncGPUReadback) return 0;

        int w = Mathf.Min(tex.width, 256);
        int h = Mathf.Min(tex.height, 256);
        var req = AsyncGPUReadback.Request(tex, 0, 0, w, 0, h, 0, 1);
        req.WaitForCompletion();
        if (req.hasError) return 0;

        var data = req.GetData<byte>();
        long sum = 0;
        for (int i = 0; i < data.Length; i++) sum += data[i];
        return sum;
    }
}
