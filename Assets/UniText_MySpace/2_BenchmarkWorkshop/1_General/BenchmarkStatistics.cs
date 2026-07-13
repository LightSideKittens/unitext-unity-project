using System.Collections.Generic;

static class BenchmarkStatistics
{
    internal static float MedianSorted(IReadOnlyList<float> sorted)
    {
        int count = sorted?.Count ?? 0;
        if (count == 0) return 0;
        int upper = count / 2;
        return (count & 1) != 0 ? sorted[upper] : (sorted[upper - 1] + sorted[upper]) * 0.5f;
    }
}
