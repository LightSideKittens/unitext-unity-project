using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public readonly struct ComparisonResult
{
    public readonly bool IsEqual;
    public readonly string DifferenceDescription;

    private ComparisonResult(bool isEqual, string description)
    {
        IsEqual = isEqual;
        DifferenceDescription = description;
    }

    public static ComparisonResult Equal() => new(true, null);
    public static ComparisonResult Failed(string description) => new(false, description);
}

public static class GoldenFileComparer
{
    public const float DefaultEpsilon = 1e-3f;
    public const float UvWEpsilon = 0.01f;

    public static ComparisonResult Compare(MeshDataSnapshot golden, MeshDataSnapshot actual, float epsilon = DefaultEpsilon)
    {
        if (golden == null)
            return ComparisonResult.Failed("Golden snapshot is null");

        if (actual == null)
            return ComparisonResult.Failed("Actual snapshot is null");

        var errors = new List<string>();

        if (golden.segments.Count != actual.segments.Count)
            errors.Add($"Segment count mismatch: expected {golden.segments.Count}, got {actual.segments.Count}");

        var minSegments = Math.Min(golden.segments.Count, actual.segments.Count);
        for (int i = 0; i < minSegments; i++)
        {
            CompareSegment(golden.segments[i], actual.segments[i], i, epsilon, errors);
        }

        if (errors.Count == 0)
            return ComparisonResult.Equal();

        var sb = new StringBuilder();
        sb.AppendLine($"Found {errors.Count} error(s):");
        foreach (var error in errors)
        {
            sb.AppendLine($"  • {error}");
        }
        return ComparisonResult.Failed(sb.ToString());
    }

    private static void CompareSegment(MeshSegmentData golden, MeshSegmentData actual, int segmentIndex, float epsilon, List<string> errors)
    {
        if (golden.vertices.Count != actual.vertices.Count)
            errors.Add($"Segment {segmentIndex}: vertex count mismatch - expected {golden.vertices.Count}, got {actual.vertices.Count}");
        else
        {
            for (int v = 0; v < golden.vertices.Count; v++)
            {
                if (!ApproxEqual(golden.vertices[v], actual.vertices[v], epsilon))
                {
                    int glyphIndex = v / 4;
                    float dx = actual.vertices[v].x - golden.vertices[v].x;
                    float dy = actual.vertices[v].y - golden.vertices[v].y;
                    errors.Add($"Segment {segmentIndex}, glyph {glyphIndex}, vertex {v}: delta ({dx:F2}, {dy:F2})");
                }
            }
        }

        CompareStableUVs(golden, actual, segmentIndex, epsilon, errors);

        if (golden.triangles.Count != actual.triangles.Count)
            errors.Add($"Segment {segmentIndex}: triangle count mismatch - expected {golden.triangles.Count}, got {actual.triangles.Count}");
        else
        {
            for (int t = 0; t < golden.triangles.Count; t++)
            {
                if (golden.triangles[t] != actual.triangles[t])
                    errors.Add($"Segment {segmentIndex}, triangle index {t}: mismatch - expected {golden.triangles[t]}, got {actual.triangles[t]}");
            }
        }

        if (golden.colors.Count != actual.colors.Count)
            errors.Add($"Segment {segmentIndex}: color count mismatch - expected {golden.colors.Count}, got {actual.colors.Count}");
        else
        {
            for (int c = 0; c < golden.colors.Count; c++)
            {
                var gc = golden.colors[c];
                var ac = actual.colors[c];
                if (gc.r != ac.r || gc.g != ac.g || gc.b != ac.b || gc.a != ac.a)
                    errors.Add($"Segment {segmentIndex}, color {c}: mismatch - expected ({gc.r}, {gc.g}, {gc.b}, {gc.a}), got ({ac.r}, {ac.g}, {ac.b}, {ac.a})");
            }
        }
    }

    private static void CompareStableUVs(MeshSegmentData golden, MeshSegmentData actual,
        int segmentIndex, float epsilon, List<string> errors)
    {
        if (golden.glyphGroups.Count != actual.glyphGroups.Count)
        {
            errors.Add($"Segment {segmentIndex}: glyph group count mismatch - expected {golden.glyphGroups.Count}, got {actual.glyphGroups.Count}");
            return;
        }

        for (int g = 0; g < golden.glyphGroups.Count; g++)
        {
            var gg = golden.glyphGroups[g];
            var ag = actual.glyphGroups[g];

            if (gg.glyphId != ag.glyphId)
                errors.Add($"Segment {segmentIndex}, group {g}: glyphId mismatch - expected {gg.glyphId}, got {ag.glyphId}");

            if (gg.vertexCount != ag.vertexCount)
            {
                errors.Add($"Segment {segmentIndex}, group {g} (glyph {gg.glyphId}): vertexCount mismatch - expected {gg.vertexCount}, got {ag.vertexCount}");
                continue;
            }

            for (int v = 0; v < gg.vertexCount; v++)
            {
                int gi = gg.vertexStart + v;
                int ai = ag.vertexStart + v;

                if (gi >= golden.stableUVs.Count || ai >= actual.stableUVs.Count) break;

                var guv = golden.stableUVs[gi];
                var auv = actual.stableUVs[ai];

                if (!ApproxEqual(guv, auv, epsilon))
                {
                    errors.Add($"Segment {segmentIndex}, group {g} (glyph {gg.glyphId}), vertex {v}: stableUV mismatch - expected ({guv.x:F4}, {guv.y:F4}, {guv.z:F4}, {guv.w:F4}), got ({auv.x:F4}, {auv.y:F4}, {auv.z:F4}, {auv.w:F4})");
                }
            }
        }
    }

    private static bool ApproxEqual(SerializableVector3 a, SerializableVector3 b, float epsilon)
    {
        return Mathf.Abs(a.x - b.x) < epsilon &&
               Mathf.Abs(a.y - b.y) < epsilon &&
               Mathf.Abs(a.z - b.z) < epsilon;
    }

    private static bool ApproxEqual(SerializableVector4 a, SerializableVector4 b, float epsilon)
    {
        return Mathf.Abs(a.x - b.x) < epsilon &&
               Mathf.Abs(a.y - b.y) < epsilon &&
               Mathf.Abs(a.z - b.z) < epsilon &&
               Mathf.Abs(a.w - b.w) < UvWEpsilon;
    }

    public static string GenerateDiffReport(MeshDataSnapshot golden, MeshDataSnapshot actual, float epsilon = DefaultEpsilon)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Golden File Diff Report ===");
        sb.AppendLine();

        if (golden == null)
        {
            sb.AppendLine("ERROR: Golden snapshot is null");
            return sb.ToString();
        }

        if (actual == null)
        {
            sb.AppendLine("ERROR: Actual snapshot is null");
            return sb.ToString();
        }

        sb.AppendLine($"Test: {golden.testName}");
        sb.AppendLine($"Text length: {golden.settings?.text?.Length ?? 0} chars");
        sb.AppendLine();

        sb.AppendLine($"Segments: Golden={golden.segments.Count}, Actual={actual.segments.Count}");

        var minSegments = Math.Min(golden.segments.Count, actual.segments.Count);
        for (int i = 0; i < minSegments; i++)
        {
            var g = golden.segments[i];
            var a = actual.segments[i];

            sb.AppendLine($"\n--- Segment {i} ---");
            sb.AppendLine($"  Vertices: Golden={g.vertices.Count}, Actual={a.vertices.Count}");
            sb.AppendLine($"  UVs: Golden={g.uvs.Count}, Actual={a.uvs.Count}");
            sb.AppendLine($"  Triangles: Golden={g.triangles.Count}, Actual={a.triangles.Count}");
            sb.AppendLine($"  Colors: Golden={g.colors.Count}, Actual={a.colors.Count}");

            var minVerts = Math.Min(g.vertices.Count, a.vertices.Count);
            int diffCount = 0;
            for (int v = 0; v < minVerts; v++)
            {
                if (!ApproxEqual(g.vertices[v], a.vertices[v], epsilon))
                {
                    if (diffCount < 3)
                    {
                        sb.AppendLine($"  Vertex diff at {v}: Golden={g.vertices[v]}, Actual={a.vertices[v]}");
                    }
                    diffCount++;
                }
            }
            if (diffCount > 3)
            {
                sb.AppendLine($"  ... and {diffCount - 3} more vertex diffs");
            }
            else if (diffCount == 0 && minVerts > 0)
            {
                sb.AppendLine($"  All {minVerts} vertices match");
            }
        }

        return sb.ToString();
    }
}
