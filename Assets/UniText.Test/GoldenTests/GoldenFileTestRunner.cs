using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LightSide;
using UnityEngine;

public class GoldenFileTestRunner : MonoBehaviour
{
    public enum TestMode { Generate, Test }

#if UNITY_EDITOR
    public bool disable;
    public TestMode mode;
#endif
    [Header("Golden Test Entries")]
    [SerializeField] private StyledList<TestEntry> testEntries;

    [Header("Emoji Tests")]
    [SerializeField] private UniText emojiTestTarget;
    [SerializeField] private List<EmojiTestCase> emojiTestCases;

    private const string ResourcesPath = "Assets/UniText.Test/Resources";
    private const string GoldenFilesSubfolder = "GoldenTests";

    private static GoldenFileTestRunner instance;
    private TestResultCollection results;
    private Canvas testCanvas;

    void Awake()
    {
        instance = this;
        Debug.Log("[GoldenFileTestRunner] Awake() called - scene loaded!");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnAppStart()
    {
        Debug.Log("[GoldenFileTestRunner] === APP STARTING ===");

#if UNITEXT_TESTS
        Debug.Log("[GoldenFileTestRunner] UNITEXT_TESTS is DEFINED");
#else
        Debug.Log("[GoldenFileTestRunner] UNITEXT_TESTS is NOT DEFINED");
#endif

#if UNITY_IOS && !UNITY_EDITOR
        FirebaseTestLabiOS.Initialize();
#elif UNITY_ANDROID && !UNITY_EDITOR
        FirebaseTestLabAndroid.Initialize();
#endif
    }

#if UNITEXT_TESTS
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnRuntimeStart()
    {
        Debug.Log("[GoldenFileTestRunner] OnRuntimeStart called");
        UnityEngine.Debug.unityLogger.Log("[GoldenFileTestRunner] Starting test runner...");

        try
        {
            var markerPath = Path.Combine(Application.persistentDataPath, "test_started.txt");
            File.WriteAllText(markerPath, $"Test started at {DateTime.UtcNow}");
            Debug.Log($"[GoldenFileTestRunner] Marker written to: {markerPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoldenFileTestRunner] Failed to write marker: {e.Message}");
        }

        var runner = FindObjectOfType<GoldenFileTestRunner>();
        if (runner == null)
        {
            Debug.LogError("[GoldenFileTestRunner] Runner not found on scene!");
            Application.Quit(1);
            return;
        }
        
#if UNITY_EDITOR
        if (runner.disable)
        {
            return;
        }
#endif
        
        Debug.Log("[GoldenFileTestRunner] Runner found, starting tests...");
        var mode = TestMode.Test;
#if UNITY_EDITOR
        mode = runner.mode;
#endif
        runner.StartTests(mode);
    }
#endif

    public void StartTests(TestMode mode)
    {
#if !UNITEXT_TESTS
        Debug.LogError("[GoldenFileTestRunner] UNITEXT_TESTS define is not set! Add it to Player Settings > Scripting Define Symbols");
        return;
#endif
        if(!Application.isPlaying) return;
        StartCoroutine(RunAllTests(mode));
    }

    IEnumerator RunAllTests(TestMode mode)
    {
        Debug.Log($"[GoldenFileTestRunner] Starting tests in {mode} mode...");

        EmojiFont.Disabled = true;

        results = new TestResultCollection();

        yield return null;

        if (testEntries == null || testEntries.Length == 0)
        {
            Debug.LogWarning("[GoldenFileTestRunner] No test entries configured!");
        }
        else
        {
            foreach (var entry in testEntries)
            {
                yield return RunTestEntry(entry, mode);
            }
        }

        EmojiFont.Disabled = false;

        yield return RunEmojiTests();

        TestScreenshot.Cleanup();
        OutputResults();
    }

    #region Emoji Tests

    IEnumerator RunEmojiTests()
    {
        if (emojiTestCases == null || emojiTestCases.Count == 0)
        {
            Debug.Log("[GoldenFileTestRunner] No emoji test cases configured, skipping emoji tests");
            yield break;
        }

        if (emojiTestTarget == null)
        {
            Debug.LogError("[GoldenFileTestRunner] emojiTestTarget is not assigned!");
            results.Add(new TestResult
            {
                ClassName = "EmojiTests",
                MethodName = "Setup",
                Passed = false,
                ErrorMessage = "emojiTestTarget is not assigned"
            });
            yield break;
        }

        Debug.Log("[GoldenFileTestRunner] Starting emoji tests...");

        var startTime = DateTime.Now;
        var emojiFont = EmojiFont.Instance;

        if (emojiFont == null)
        {
            results.Add(new TestResult
            {
                ClassName = "EmojiTests",
                MethodName = "EmojiFont_Available",
                Passed = false,
                StartTime = startTime,
                EndTime = DateTime.Now,
                ErrorMessage = "EmojiFont.Instance is null"
            });
            Debug.LogError("[GoldenFileTestRunner] EmojiFont not available, skipping emoji tests");
            yield break;
        }

        results.Add(new TestResult
        {
            ClassName = "EmojiTests",
            MethodName = "EmojiFont_Available",
            Passed = true,
            StartTime = startTime,
            EndTime = DateTime.Now
        });

        foreach (var testCase in emojiTestCases)
        {
            yield return RunEmojiTest(testCase);
        }

        TestScreenshot.Capture("emoji_tests_complete");
    }

    IEnumerator RunEmojiTest(EmojiTestCase testCase)
    {
        var testName = testCase.testName;
        var startTime = DateTime.Now;

        Debug.Log($"[GoldenFileTestRunner] Running emoji test: {testName}");

        yield return null;

        var errors = new List<string>();

        var emojiFont = EmojiFont.Instance;
        var atlasTextures = emojiFont.AtlasTextures;

        if (atlasTextures == null || atlasTextures.Count == 0)
        {
            errors.Add("Atlas textures array is null or empty");
        }
        else if (atlasTextures[0] == null)
        {
            errors.Add("First atlas texture is null");
        }
        else if (!HasNonEmptyPixels(atlasTextures[0]))
        {
            errors.Add("Atlas texture has no visible pixels");
        }

        var glyphTable = emojiFont.GlyphLookupTable;
        if (glyphTable == null || glyphTable.Count == 0)
        {
            errors.Add("No glyphs in EmojiFont");
        }

#if UNITEXT_TESTS
        var meshes = emojiTestTarget.TestMeshSnapshots;
        if (meshes == null || meshes.Count == 0)
        {
            errors.Add("No mesh generated");
        }
        else
        {
            int totalVertices = 0;
            foreach (var mesh in meshes)
            {
                if (mesh != null)
                    totalVertices += mesh.vertexCount;
            }

            if (totalVertices == 0)
            {
                errors.Add("Mesh has no vertices");
            }
            else if (testCase.expectedGlyphCount > 0)
            {
                int expectedVertices = testCase.expectedGlyphCount * 4;
                if (totalVertices < expectedVertices)
                {
                    errors.Add($"Expected at least {expectedVertices} vertices ({testCase.expectedGlyphCount} glyphs), got {totalVertices}");
                }
            }
        }
#endif

        TestScreenshot.Capture($"emoji_{testName}");

        bool passed = errors.Count == 0;
        results.Add(new TestResult
        {
            ClassName = "EmojiTests",
            MethodName = testName,
            Passed = passed,
            StartTime = startTime,
            EndTime = DateTime.Now,
            ErrorMessage = passed ? null : string.Join("; ", errors)
        });

        if (passed)
            Debug.Log($"  [OK] {testName}");
        else
            Debug.LogError($"  [FAIL] {testName}: {string.Join("; ", errors)}");
    }

    bool HasNonEmptyPixels(Texture2D texture)
    {
        if (texture == null)
            return false;

        var pixels = texture.GetPixels32();
        int step = Math.Max(1, pixels.Length / 10000);

        for (int i = 0; i < pixels.Length; i += step)
        {
            if (pixels[i].a > 0)
                return true;
        }

        return false;
    }

    #endregion

    IEnumerator RunTestEntry(TestEntry entry, TestMode mode)
    {
        if (entry.testCases == null || entry.testCases.Count == 0)
        {
            Debug.LogWarning("[GoldenFileTestRunner] Test entry has no test cases");
            yield break;
        }

        var uniText = entry.targetUniText;
        var rectTransform = uniText.GetComponent<RectTransform>();

        if (uniText == null)
        {
            Debug.LogError("[GoldenFileTestRunner] Static test has null UniText reference");
            yield break;
        }

        foreach (var testCase in entry.testCases)
        {
            if (testCase == null)
            {
                Debug.LogWarning("[GoldenFileTestRunner] Null test case in entry");
                continue;
            }

            yield return RunSingleTest(uniText, rectTransform, testCase, mode);
        }
    }

    IEnumerator RunSingleTest(UniText uniText, RectTransform rectTransform, BaseTestCase testCase, TestMode mode)
    {
        var testName = testCase.TestName;
        Debug.Log($"[GoldenFileTestRunner] Running test: {testName}");

        var startTime = DateTime.Now;

        testCase.ApplyTo(uniText, rectTransform);

        yield return null;

        var snapshot = CreateSnapshot(testName, uniText);

        ProcessSnapshot(testName, snapshot, mode, startTime, uniText);
    }

    void ProcessSnapshot(string testName, MeshDataSnapshot snapshot, TestMode mode, DateTime startTime, UniText uniText)
    {
        var filePath = GetGoldenFilePath(testName);

        if (mode == TestMode.Generate)
        {
            MeshDataSerializer.SaveToFile(snapshot, filePath);
            results.Add(new TestResult
            {
                ClassName = "GoldenTests",
                MethodName = testName,
                Passed = true,
                StartTime = startTime,
                EndTime = DateTime.Now,
                ErrorMessage = "Generated"
            });
            Debug.Log($"  ✓ Generated: {filePath}");
        }
        else
        {
            var golden = LoadGoldenFile(testName);
            if (golden == null)
            {
                results.Add(new TestResult
                {
                    ClassName = "GoldenTests",
                    MethodName = testName,
                    Passed = false,
                    StartTime = startTime,
                    EndTime = DateTime.Now,
                    ErrorMessage = $"Golden file not found: {testName}"
                });
                Debug.LogError($"  ✗ {testName}: Golden file not found");
                return;
            }

            var comparison = GoldenFileComparer.Compare(golden, snapshot);

            var fontErrors = VerifyActualUVsAgainstFont(snapshot, uniText);
            bool fontOk = fontErrors.Count == 0;

            bool passed = comparison.IsEqual && fontOk;
            string errorMsg = null;
            string stackTrace = null;

            if (!passed)
            {
                var allErrors = new List<string>();
                if (!comparison.IsEqual)
                    allErrors.Add(comparison.DifferenceDescription);
                if (!fontOk)
                {
                    allErrors.Add($"Font verification failed ({fontErrors.Count} error(s)):");
                    allErrors.AddRange(fontErrors);
                }
                errorMsg = string.Join("\n", allErrors);
                stackTrace = GoldenFileComparer.GenerateDiffReport(golden, snapshot);
            }

            results.Add(new TestResult
            {
                ClassName = "GoldenTests",
                MethodName = testName,
                Passed = passed,
                StartTime = startTime,
                EndTime = DateTime.Now,
                ErrorMessage = errorMsg,
                StackTrace = stackTrace
            });

            if (passed)
                Debug.Log($"  ✓ {testName}");
            else
                Debug.LogError($"  ✗ {testName}: {errorMsg}");
        }
    }

    MeshDataSnapshot CreateSnapshot(string testName, UniText uniText)
    {
        var snapshot = new MeshDataSnapshot
        {
            testName = testName,
            settings = new TestSettings
            {
                text = uniText.Text,
                fontSize = uniText.FontSize,
                alignment = $"{uniText.HorizontalAlignment}, {uniText.VerticalAlignment}",
                maxWidth = uniText.rectTransform.sizeDelta.x
            },
            generatedAt = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion
        };

#if UNITEXT_TESTS
        var meshSnapshots = uniText.TestMeshSnapshots;
        if (meshSnapshots != null)
        {
            var fontInfoList = uniText.TestSegmentFontInfoList;
            for (int i = 0; i < meshSnapshots.Count; i++)
            {
                var fontInfo = (fontInfoList != null && i < fontInfoList.Count)
                    ? fontInfoList[i]
                    : default;
                AddMeshToSnapshot(meshSnapshots[i], snapshot, fontInfo, uniText.FontProvider);
            }
        }
#endif

        return snapshot;
    }

    void AddMeshToSnapshot(Mesh mesh, MeshDataSnapshot snapshot,
        UniText.TestSegmentFontInfo fontInfo, LightSide.UniTextFontProvider fontProvider)
    {
        if (mesh == null) return;

        var segment = new MeshSegmentData();

        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            segment.vertices.Add(new SerializableVector3(vertices[i]));

        var uvs = new List<Vector4>();
        mesh.GetUVs(0, uvs);
        for (int i = 0; i < uvs.Count; i++)
            segment.uvs.Add(new SerializableVector4(uvs[i]));

        segment.triangles.AddRange(mesh.triangles);

        var colors = mesh.colors32;
        for (int i = 0; i < colors.Length; i++)
            segment.colors.Add(new SerializableColor32(colors[i]));

        BuildGlyphGroupsAndStableUVs(segment, fontInfo, fontProvider);

        snapshot.segments.Add(segment);
    }

    #region Reverse UV Identification

    void BuildGlyphGroupsAndStableUVs(MeshSegmentData segment,
        UniText.TestSegmentFontInfo fontInfo, LightSide.UniTextFontProvider fontProvider)
    {
        int vertexCount = segment.vertices.Count;
        if (vertexCount == 0) return;

        var font = fontProvider?.GetFontAsset(fontInfo.fontId);
        int atlasSize = font != null ? font.AtlasSize : 0;
        int padding = font != null ? font.AtlasPadding : 0;
        Dictionary<uint, LightSide.Glyph> glyphTable = font?.GlyphLookupTable;

        var primitives = FindPrimitives(segment.triangles, vertexCount);

        for (int i = 0; i < vertexCount; i++)
            segment.stableUVs.Add(segment.uvs[i]);

        for (int p = 0; p < primitives.Count; p++)
        {
            var prim = primitives[p];
            int glyphId = -1;

            if (glyphTable != null && atlasSize > 0)
            {
                float uvMinX = float.MaxValue, uvMinY = float.MaxValue;
                float uvMaxX = float.MinValue, uvMaxY = float.MinValue;

                for (int v = prim.vertexStart; v < prim.vertexStart + prim.vertexCount; v++)
                {
                    var uv = segment.uvs[v];
                    if (uv.x < uvMinX) uvMinX = uv.x;
                    if (uv.y < uvMinY) uvMinY = uv.y;
                    if (uv.x > uvMaxX) uvMaxX = uv.x;
                    if (uv.y > uvMaxY) uvMaxY = uv.y;
                }

                glyphId = IdentifyGlyphFromUVs(glyphTable, fontInfo.atlasIndex,
                    atlasSize, padding, uvMinX, uvMinY, uvMaxX, uvMaxY);

                if (glyphId >= 0 && glyphTable.TryGetValue((uint)glyphId, out var glyph))
                {
                    NormalizeUVs(segment.stableUVs, prim.vertexStart, prim.vertexCount,
                        glyph.glyphRect, atlasSize, padding);
                }
            }

            segment.glyphGroups.Add(new GlyphGroup
            {
                glyphId = glyphId,
                vertexStart = prim.vertexStart,
                vertexCount = prim.vertexCount
            });
        }
    }

    static int IdentifyGlyphFromUVs(Dictionary<uint, LightSide.Glyph> glyphTable,
        int atlasIndex, int atlasSize, int padding,
        float uvMinX, float uvMinY, float uvMaxX, float uvMaxY)
    {
        const float eps = 1e-3f;
        float invAtlas = 1f / atlasSize;

        foreach (var kvp in glyphTable)
        {
            var glyph = kvp.Value;
            if (glyph.atlasIndex != atlasIndex) continue;

            var rect = glyph.glyphRect;
            float gMinX = (rect.x - padding) * invAtlas;
            float gMinY = (rect.y - padding) * invAtlas;
            float gMaxX = (rect.x + rect.width + padding) * invAtlas;
            float gMaxY = (rect.y + rect.height + padding) * invAtlas;

            if (Mathf.Abs(uvMinX - gMinX) < eps && Mathf.Abs(uvMinY - gMinY) < eps &&
                Mathf.Abs(uvMaxX - gMaxX) < eps && Mathf.Abs(uvMaxY - gMaxY) < eps)
                return (int)glyph.index;
        }

        foreach (var kvp in glyphTable)
        {
            var glyph = kvp.Value;
            if (glyph.atlasIndex != atlasIndex) continue;

            var rect = glyph.glyphRect;
            float gMinX = (rect.x - padding) * invAtlas;
            float gMinY = (rect.y - padding) * invAtlas;
            float gMaxX = (rect.x + rect.width + padding) * invAtlas;
            float gMaxY = (rect.y + rect.height + padding) * invAtlas;

            if (uvMinX >= gMinX - eps && uvMaxX <= gMaxX + eps &&
                uvMinY >= gMinY - eps && uvMaxY <= gMaxY + eps)
                return (int)glyph.index;
        }

        return -1;
    }

    static void NormalizeUVs(List<SerializableVector4> stableUVs,
        int vertexStart, int vertexCount,
        LightSide.GlyphRect rect, int atlasSize, int padding)
    {
        float invAtlas = 1f / atlasSize;
        float gMinX = (rect.x - padding) * invAtlas;
        float gMinY = (rect.y - padding) * invAtlas;
        float rangeX = (rect.width + 2 * padding) * invAtlas;
        float rangeY = (rect.height + 2 * padding) * invAtlas;

        if (rangeX < 1e-6f || rangeY < 1e-6f) return;

        float invRangeX = 1f / rangeX;
        float invRangeY = 1f / rangeY;

        for (int i = vertexStart; i < vertexStart + vertexCount; i++)
        {
            var uv = stableUVs[i];
            stableUVs[i] = new SerializableVector4(new Vector4(
                (uv.x - gMinX) * invRangeX,
                (uv.y - gMinY) * invRangeY,
                uv.z, uv.w));
        }
    }

    #endregion

    #region Union-Find Primitive Discovery

    struct Primitive
    {
        public int vertexStart;
        public int vertexCount;
    }

    static List<Primitive> FindPrimitives(List<int> triangles, int vertexCount)
    {
        var parent = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            parent[i] = i;

        for (int t = 0; t < triangles.Count; t += 3)
        {
            Union(parent, triangles[t], triangles[t + 1]);
            Union(parent, triangles[t], triangles[t + 2]);
        }

        for (int i = 0; i < vertexCount; i++)
            Find(parent, i);

        var rootToIndex = new Dictionary<int, int>();
        var primitives = new List<Primitive>();

        for (int i = 0; i < vertexCount; i++)
        {
            int root = parent[i];
            if (!rootToIndex.TryGetValue(root, out var primIdx))
            {
                primIdx = primitives.Count;
                rootToIndex[root] = primIdx;
                primitives.Add(new Primitive { vertexStart = i, vertexCount = 1 });
            }
            else
            {
                var prim = primitives[primIdx];
                int newEnd = i + 1;
                int currentEnd = prim.vertexStart + prim.vertexCount;
                if (newEnd > currentEnd)
                    prim.vertexCount = newEnd - prim.vertexStart;
                primitives[primIdx] = prim;
            }
        }

        return primitives;
    }

    static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    static void Union(int[] parent, int a, int b)
    {
        a = Find(parent, a);
        b = Find(parent, b);
        if (a != b) parent[b] = a;
    }

    #endregion

    #region Font Verification

    List<string> VerifyActualUVsAgainstFont(MeshDataSnapshot actual, UniText uniText)
    {
        var errors = new List<string>();
#if UNITEXT_TESTS
        var fontInfoList = uniText.TestSegmentFontInfoList;
        var fontProvider = uniText.FontProvider;
        if (fontInfoList == null || fontProvider == null) return errors;

        for (int s = 0; s < actual.segments.Count; s++)
        {
            var segment = actual.segments[s];
            if (s >= fontInfoList.Count) break;

            var fontInfo = fontInfoList[s];
            var font = fontProvider.GetFontAsset(fontInfo.fontId);
            if (font == null) continue;

            var glyphTable = font.GlyphLookupTable;
            int atlasSize = font.AtlasSize;
            int atlasPadding = font.AtlasPadding;
            float invAtlas = 1f / atlasSize;
            const float eps = 2e-3f;

            foreach (var group in segment.glyphGroups)
            {
                if (group.glyphId < 0) continue;

                if (!glyphTable.TryGetValue((uint)group.glyphId, out var glyph))
                {
                    errors.Add($"Segment {s}: glyph {group.glyphId} not found in font");
                    continue;
                }

                var rect = glyph.glyphRect;
                float gMinX = (rect.x - atlasPadding) * invAtlas;
                float gMinY = (rect.y - atlasPadding) * invAtlas;
                float gMaxX = (rect.x + rect.width + atlasPadding) * invAtlas;
                float gMaxY = (rect.y + rect.height + atlasPadding) * invAtlas;

                for (int v = group.vertexStart; v < group.vertexStart + group.vertexCount; v++)
                {
                    if (v >= segment.uvs.Count) break;
                    var uv = segment.uvs[v];
                    if (uv.x < gMinX - eps || uv.x > gMaxX + eps ||
                        uv.y < gMinY - eps || uv.y > gMaxY + eps)
                    {
                        errors.Add($"Segment {s}, glyph {group.glyphId}, vertex {v}: UV ({uv.x:F4}, {uv.y:F4}) outside glyph rect [{gMinX:F4}..{gMaxX:F4}, {gMinY:F4}..{gMaxY:F4}]");
                    }
                }
            }
        }
#endif
        return errors;
    }

    #endregion

    string GetGoldenFilePath(string testName)
    {
        return Path.Combine(ResourcesPath, GoldenFilesSubfolder, $"{testName}.json");
    }

    MeshDataSnapshot LoadGoldenFile(string testName)
    {
        var resourcePath = $"{GoldenFilesSubfolder}/{testName}";
        var textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset == null) return null;
        return MeshDataSerializer.FromJson(textAsset.text);
    }

    void OutputResults()
    {
        var passed = results.Passed;
        var total = results.Total;

        Debug.Log($"[GoldenFileTestRunner] Tests completed: {passed}/{total} passed");

        var xml = results.ToJUnitXml();

        var xmlPath = Path.Combine(Application.persistentDataPath, "testResults.xml");
        File.WriteAllText(xmlPath, xml);
        Debug.Log($"[GoldenFileTestRunner] Results saved to: {xmlPath}");

        Console.WriteLine($"TEST_RESULTS_PATH={xmlPath}");

#if UNITY_IOS && !UNITY_EDITOR
        FirebaseTestLabiOS.WriteResults("testResults.xml", xml);

        FirebaseTestLabiOS.NotifyTestComplete();

        System.Threading.Thread.Sleep(500);
#elif UNITY_ANDROID && !UNITY_EDITOR
        FirebaseTestLabAndroid.WriteResults("testResults.xml", xml);

        FirebaseTestLabAndroid.NotifyTestComplete();
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLReportResults();
#else
        if (Application.isBatchMode)
        {
            Application.Quit(results.AllPassed ? 0 : 1);
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ReportTestResults(string json);

    void WebGLReportResults()
    {
        var json = JsonUtility.ToJson(new WebGLTestResults
        {
            xml = results.ToJUnitXml(),
            allPassed = results.AllPassed,
            total = results.Total,
            passed = results.Passed,
            failed = results.Failed
        });
        ReportTestResults(json);
    }

    [Serializable]
    private class WebGLTestResults
    {
        public string xml;
        public bool allPassed;
        public int total;
        public int passed;
        public int failed;
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Generate Golden Files")]
    public void EditorGenerate()
    {
        StartTests(TestMode.Generate);
    }

    [ContextMenu("Run Tests")]
    public void EditorRunTests()
    {
        StartTests(TestMode.Test);
    }
#endif
}

[Serializable]
public class EmojiTestCase
{
    [Tooltip("Test name for reporting")]
    public string testName;
    
    [Tooltip("Expected glyph count (0 = don't verify count)")]
    public int expectedGlyphCount;
}
