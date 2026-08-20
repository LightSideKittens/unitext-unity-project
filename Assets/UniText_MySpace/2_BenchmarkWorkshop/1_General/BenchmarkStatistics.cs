using System;
using System.Collections.Generic;

static class BenchmarkStatistics
{
    internal static float MedianSorted(IReadOnlyList<float> sorted) => PercentileSorted(sorted, 0.5f);

    internal static float PercentileSorted(IReadOnlyList<float> sorted, float percentile)
    {
        int count = sorted?.Count ?? 0;
        if (count == 0) return 0;
        if (float.IsNaN(percentile) || percentile < 0f || percentile > 1f)
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile,
                "A percentile must be between zero and one.");

        float position = (count - 1) * percentile;
        int lower = (int)position;
        int upper = Math.Min(lower + 1, count - 1);
        float fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }
}
