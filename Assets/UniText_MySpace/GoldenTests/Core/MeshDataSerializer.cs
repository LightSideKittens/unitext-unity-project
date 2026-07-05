using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public struct GlyphGroup
{
    /// <summary>Glyph index in the font, -1 = non-glyph geometry.</summary>
    public int glyphId;
    /// <summary>Start index in the segment's vertex/UV arrays.</summary>
    public int vertexStart;
    /// <summary>Number of vertices in this group.</summary>
    public int vertexCount;
}

[Serializable]
public class MeshSegmentData
{
    public List<SerializableVector3> vertices = new();
    public List<SerializableVector4> uvs = new();
    public List<int> triangles = new();
    public List<SerializableColor32> colors = new();
    public List<GlyphGroup> glyphGroups = new();
    public List<SerializableVector4> stableUVs = new();
}

[Serializable]
public class MeshDataSnapshot
{
    public string testName;
    public TestSettings settings;
    public List<MeshSegmentData> segments = new();
    public string generatedAt;
    public string unityVersion;
}

[Serializable]
public class TestSettings
{
    public string text;
    public float fontSize;
    public string alignment;
    public float maxWidth;
}

[Serializable]
public struct SerializableVector3
{
    public float x, y, z;

    public SerializableVector3(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3() => new(x, y, z);

    public override string ToString() => $"({x:F4}, {y:F4}, {z:F4})";
}

[Serializable]
public struct SerializableVector4
{
    public float x, y, z, w;

    public SerializableVector4(Vector4 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
        w = v.w;
    }

    public Vector4 ToVector4() => new(x, y, z, w);
}

[Serializable]
public struct SerializableColor32
{
    public byte r, g, b, a;

    public SerializableColor32(Color32 c)
    {
        r = c.r;
        g = c.g;
        b = c.b;
        a = c.a;
    }

    public Color32 ToColor32() => new(r, g, b, a);
}

public static class MeshDataSerializer
{
    public static string ToJson(MeshDataSnapshot snapshot)
    {
        return JsonUtility.ToJson(snapshot, true);
    }

    public static MeshDataSnapshot FromJson(string json)
    {
        return JsonUtility.FromJson<MeshDataSnapshot>(json);
    }

    public static void SaveToFile(MeshDataSnapshot snapshot, string filePath)
    {
        var json = ToJson(snapshot);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public static MeshDataSnapshot LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return FromJson(json);
    }
}
