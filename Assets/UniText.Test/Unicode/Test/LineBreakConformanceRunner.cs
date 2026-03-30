using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LightSide;


internal sealed class LineBreakConformanceRunner
{
    private readonly LineBreakAlgorithm algorithm;

    public LineBreakConformanceRunner()
    {
        algorithm = new LineBreakAlgorithm();
    }

    public LineBreakConformanceRunner(LineBreakAlgorithm algorithm)
    {
        this.algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
    }


    public LineBreakConformanceSummary RunTests(string fileContent, int maxFailuresToLog = 20)
    {
        var summary = new LineBreakConformanceSummary();

        if (string.IsNullOrEmpty(fileContent))
        {
            summary.sampleFailures = "LineBreakTest content is empty or null.";
            return summary;
        }

        var failures = new List<LineBreakConformanceFailure>();

        using var reader = new System.IO.StringReader(fileContent);
        string line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;

            var hashIndex = line.IndexOf('#');
            if (hashIndex >= 0)
                line = line.Substring(0, hashIndex);

            line = line.Trim();
            if (line.Length == 0)
                continue;

            if (!TryParseTestCase(line, out var codePoints, out var expectedBreaks, out var parseError))
            {
                summary.skippedTests++;
                if (parseError != null)
                    AddFailure(failures, lineNumber, line, parseError, maxFailuresToLog);
                continue;
            }

            summary.totalEvaluatedTests++;

            LineBreakType[] actualBreakTypes;
            try
            {
                actualBreakTypes = algorithm.GetBreakOpportunities(codePoints);
            }
            catch (Exception ex)
            {
                summary.failedTests++;
                AddFailure(failures, lineNumber, line, $"Exception: {ex.Message}", maxFailuresToLog);
                continue;
            }

            if (!CompareBreaks(expectedBreaks, actualBreakTypes, out var errorMessage))
            {
                summary.failedTests++;
                AddFailure(failures, lineNumber, line, errorMessage, maxFailuresToLog);
                continue;
            }

            summary.passedTests++;
        }

        summary.sampleFailures = BuildSampleFailuresText(failures, maxFailuresToLog);
        return summary;
    }


    private bool TryParseTestCase(string line, out int[] codePoints, out bool[] breaks, out string error)
    {
        codePoints = Array.Empty<int>();
        breaks = Array.Empty<bool>();
        error = null;

        var cpList = new List<int>();
        var breakList = new List<bool>();

        var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var expectBreakMarker = true;

        foreach (var token in tokens)
            if (token == "÷" || token == "\u00F7")
            {
                if (!expectBreakMarker)
                {
                    error = "Unexpected ÷ marker";
                    return false;
                }

                breakList.Add(true);
                expectBreakMarker = false;
            }
            else if (token == "×" || token == "\u00D7")
            {
                if (!expectBreakMarker)
                {
                    error = "Unexpected × marker";
                    return false;
                }

                breakList.Add(false);
                expectBreakMarker = false;
            }
            else
            {
                if (expectBreakMarker)
                {
                    error = $"Expected break marker, got '{token}'";
                    return false;
                }

                if (!int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp))
                {
                    error = $"Invalid hex codepoint: '{token}'";
                    return false;
                }

                cpList.Add(cp);
                expectBreakMarker = true;
            }

        if (cpList.Count == 0)
        {
            error = "No codepoints found";
            return false;
        }

        if (breakList.Count != cpList.Count + 1)
        {
            error = $"Break count mismatch: expected {cpList.Count + 1}, got {breakList.Count}";
            return false;
        }

        codePoints = cpList.ToArray();
        breaks = breakList.ToArray();
        return true;
    }

    private bool CompareBreaks(bool[] expected, LineBreakType[] actual, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (expected.Length != actual.Length)
        {
            errorMessage = $"Length mismatch: expected {expected.Length}, actual {actual.Length}";
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            var actualCanBreak = actual[i] != LineBreakType.None;
            if (expected[i] != actualCanBreak)
            {
                var expectedStr = expected[i] ? "÷ (break)" : "× (no break)";
                var actualStr = actualCanBreak ? $"÷ ({actual[i]})" : "× (no break)";
                errorMessage = $"Mismatch at position {i}: expected {expectedStr}, actual {actualStr}";
                return false;
            }
        }

        return true;
    }

    private static void AddFailure(List<LineBreakConformanceFailure> failures, int lineNumber,
        string rawInput, string message, int maxFailuresToLog)
    {
        if (failures.Count >= maxFailuresToLog)
            return;

        failures.Add(new LineBreakConformanceFailure
        {
            lineNumber = lineNumber,
            rawInput = rawInput,
            message = message
        });
    }

    private static string BuildSampleFailuresText(List<LineBreakConformanceFailure> failures, int maxFailuresToLog)
    {
        if (failures == null || failures.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var count = Math.Min(failures.Count, maxFailuresToLog);

        for (var i = 0; i < count; i++)
        {
            var failure = failures[i];
            sb.Append("- Line ").Append(failure.lineNumber).Append(": ").Append(failure.message).AppendLine();
            sb.Append("  Input: ").Append(failure.rawInput).AppendLine();
        }

        return sb.ToString();
    }
}

internal sealed class LineBreakConformanceFailure
{
    public int lineNumber;
    public string rawInput;
    public string message;
}

public struct LineBreakConformanceSummary
{
    public int totalEvaluatedTests;
    public int passedTests;
    public int failedTests;
    public int skippedTests;
    public string sampleFailures;
}