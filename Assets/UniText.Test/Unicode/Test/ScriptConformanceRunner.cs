using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LightSide;


internal sealed class ScriptConformanceRunner
{
    private readonly UnicodeDataProvider dataProvider;

    public ScriptConformanceRunner(UnicodeDataProvider dataProvider)
    {
        this.dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }

    #region Data Tests - Scripts.txt validation

    public ScriptConformanceSummary RunDataTests(string scriptsFileContent, int maxFailuresToLog = 20)
    {
        var summary = new ScriptConformanceSummary();

        if (string.IsNullOrEmpty(scriptsFileContent))
        {
            summary.sampleFailures = "Scripts.txt content is empty or null.";
            return summary;
        }

        var failures = new StringBuilder();
        var failureCount = 0;

        foreach (var (lineNumber, rangePart, scriptPart) in ParseDataFile(scriptsFileContent))
        {
            if (!TryParseScript(scriptPart, out var expectedScript))
            {
                summary.skippedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine($"Line {lineNumber}: Unknown script '{scriptPart}'");
                continue;
            }

            if (!TryParseRange(rangePart, out var start, out var end))
            {
                summary.skippedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine($"Line {lineNumber}: Invalid range '{rangePart}'");
                continue;
            }

            foreach (var cp in GetSamplePoints(start, end))
            {
                summary.totalEvaluatedTests++;

                try
                {
                    var actual = dataProvider.GetScript(cp);
                    if (actual == expectedScript)
                    {
                        summary.passedTests++;
                    }
                    else
                    {
                        summary.failedTests++;
                        if (failureCount++ < maxFailuresToLog)
                            failures.AppendLine($"U+{cp:X4}: expected {expectedScript}, got {actual}");
                    }
                }
                catch (Exception ex)
                {
                    summary.failedTests++;
                    if (failureCount++ < maxFailuresToLog)
                        failures.AppendLine($"U+{cp:X4}: Exception - {ex.Message}");
                }
            }
        }

        summary.sampleFailures = failures.ToString();
        return summary;
    }

    #endregion

    #region Analyzer Tests - ScriptAnalyzerTest.txt validation

    public ScriptAnalyzerTestSummary RunAnalyzerTests(ScriptAnalyzer analyzer, string testFileContent,
        int maxFailuresToLog = 20)
    {
        if (analyzer == null)
            throw new ArgumentNullException(nameof(analyzer));

        var summary = new ScriptAnalyzerTestSummary();

        if (string.IsNullOrEmpty(testFileContent))
        {
            summary.sampleFailures = "Test file content is empty or null.";
            return summary;
        }

        var failures = new StringBuilder();
        var failureCount = 0;

        var scriptsBuffer = new UnicodeScript[256];

        foreach (var (lineNumber, codepointsPart, scriptsPart) in ParseDataFile(testFileContent))
        {
            summary.totalTests++;

            if (!TryParseCodepoints(codepointsPart, out var codepoints))
            {
                summary.failedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine($"Line {lineNumber}: Invalid codepoints '{codepointsPart}'");
                continue;
            }

            if (!TryParseScriptList(scriptsPart, out var expectedScripts))
            {
                summary.failedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine($"Line {lineNumber}: Invalid scripts '{scriptsPart}'");
                continue;
            }

            if (codepoints.Length != expectedScripts.Length)
            {
                summary.failedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine(
                        $"Line {lineNumber}: Length mismatch - {codepoints.Length} codepoints vs {expectedScripts.Length} scripts");
                continue;
            }

            if (scriptsBuffer.Length < codepoints.Length)
                scriptsBuffer = new UnicodeScript[codepoints.Length];

            try
            {
                analyzer.Analyze(codepoints, scriptsBuffer);
                var actualScripts = scriptsBuffer.AsSpan(0, codepoints.Length);

                if (actualScripts.Length != expectedScripts.Length)
                {
                    summary.failedTests++;
                    if (failureCount++ < maxFailuresToLog)
                        failures.AppendLine(
                            $"Line {lineNumber}: Result length {actualScripts.Length}, expected {expectedScripts.Length}");
                    continue;
                }

                var passed = true;
                for (var i = 0; i < actualScripts.Length; i++)
                    if (actualScripts[i] != expectedScripts[i])
                    {
                        passed = false;
                        if (failureCount++ < maxFailuresToLog)
                            failures.AppendLine(
                                $"Line {lineNumber}: Index {i} (U+{codepoints[i]:X4}) - expected {expectedScripts[i]}, got {actualScripts[i]}");
                        break;
                    }

                if (passed)
                    summary.passedTests++;
                else
                    summary.failedTests++;
            }
            catch (Exception ex)
            {
                summary.failedTests++;
                if (failureCount++ < maxFailuresToLog)
                    failures.AppendLine($"Line {lineNumber}: Exception - {ex.Message}");
            }
        }

        summary.sampleFailures = failures.ToString();
        return summary;
    }

    #endregion

    #region Parsing helpers

    private IEnumerable<(int lineNumber, string field1, string field2)> ParseDataFile(string content)
    {
        using var reader = new System.IO.StringReader(content);
        string line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;

            var hash = line.IndexOf('#');
            if (hash >= 0)
                line = line.Substring(0, hash);

            line = line.Trim();
            if (line.Length == 0)
                continue;

            var semi = line.IndexOf(';');
            if (semi < 0)
                continue;

            var field1 = line.Substring(0, semi).Trim();
            var field2 = line.Substring(semi + 1).Trim();

            if (field1.Length > 0 && field2.Length > 0)
                yield return (lineNumber, field1, field2);
        }
    }

    private bool TryParseRange(string rangePart, out int start, out int end)
    {
        start = end = 0;

        var dots = rangePart.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0)
            return int.TryParse(rangePart.Substring(0, dots), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                       out start)
                   && int.TryParse(rangePart.Substring(dots + 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                       out end)
                   && start >= 0 && end >= start;

        if (int.TryParse(rangePart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out start))
        {
            end = start;
            return start >= 0;
        }

        return false;
    }

    private bool TryParseScript(string name, out UnicodeScript script)
    {
        var sb = new StringBuilder();
        foreach (var part in name.Split('_'))
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part.Substring(1).ToLowerInvariant());
        }

        return Enum.TryParse(sb.ToString(), true, out script);
    }

    private bool TryParseCodepoints(string input, out int[] codepoints)
    {
        var list = new List<int>();
        foreach (var hex in input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
            {
                codepoints = Array.Empty<int>();
                return false;
            }

            list.Add(cp);
        }

        codepoints = list.ToArray();
        return true;
    }

    private bool TryParseScriptList(string input, out UnicodeScript[] scripts)
    {
        var list = new List<UnicodeScript>();
        foreach (var name in input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseScript(name, out var script))
            {
                scripts = Array.Empty<UnicodeScript>();
                return false;
            }

            list.Add(script);
        }

        scripts = list.ToArray();
        return true;
    }

    private IEnumerable<int> GetSamplePoints(int start, int end)
    {
        var size = end - start + 1;

        if (size <= 10)
        {
            for (var i = start; i <= end; i++)
                yield return i;
            yield break;
        }

        yield return start;
        yield return end;
        yield return start + 1;
        yield return end - 1;
        yield return start + size / 2;

        var step = size / 5;
        for (var i = 1; i < 5; i++)
            yield return start + i * step;
    }

    #endregion

    #region Backward Compatibility

    public ScriptConformanceSummary RunTests(string scriptsFileContent, int maxFailuresToLog = 20)
    {
        return RunDataTests(scriptsFileContent, maxFailuresToLog);
    }

    #endregion
}

#region Summary Types

public struct ScriptConformanceSummary
{
    public int totalEvaluatedTests;
    public int passedTests;
    public int failedTests;
    public int skippedTests;
    public string sampleFailures;
}

public struct ScriptAnalyzerTestSummary
{
    public int totalTests;
    public int passedTests;
    public int failedTests;
    public string sampleFailures;
}

#endregion