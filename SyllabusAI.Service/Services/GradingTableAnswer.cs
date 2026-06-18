using System.Text.RegularExpressions;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

/// <summary>Direct answers from the grading table (skips RAG/LLM).</summary>
public static class GradingTableAnswer
{
    private static readonly (string Label, string[] Aliases)[] KnownComponents =
    {
        ("Midterm", new[] { "midterm", "ara sınav", "ara sinav", "vize" }),
        ("Final Exam", new[] { "final exam", "final", "bütünleme", "butunleme" }),
        ("Quiz", new[] { "quiz", "quizzes" }),
        ("Process", new[] { "process", "süreç", "surec", "participation" }),
        ("Assignments", new[] { "assignments", "assignment", "ödev", "odev", "homework" }),
        ("Presentations", new[] { "presentation", "presentations", "sunum" })
    };

    public static ChatResponse? TryAnswer(string question, string syllabusText, IReadOnlyList<SyllabusChunk> chunks, int sessionId, Course course)
    {
        if (!QuestionCategoryHintService.IsGradingQuestion(question))
            return null;

        var haystack = syllabusText;
        if (string.IsNullOrWhiteSpace(haystack) && chunks.Count > 0)
            haystack = string.Join("\n\n", chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Text));

        if (string.IsNullOrWhiteSpace(haystack))
            return null;

        var gradingText = ExtractGradingTableText(haystack, chunks);
        var rows = ParseWeightRows(gradingText);

        if (rows.Count == 0)
            return null;

        var q = question.ToLowerInvariant();
        var answer = BuildAnswer(q, rows, course);
        if (string.IsNullOrWhiteSpace(answer))
            return null;

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = answer,
            FromSyllabus = true,
            IsOutOfScope = false,
            RetrievalMethod = "grading-table",
            SourceSections = new List<string> { "Grading and Evaluation" },
            SourceSnippets = rows.Take(5).Select(r => $"{r.Component}: {r.Weight}%").ToList(),
            FallbackTriggered = false
        };
    }

    private static string BuildAnswer(string q, List<WeightRow> rows, Course course)
    {
        var courseLabel = $"{course.CourseCode} - {course.Title}".Trim(' ', '-');

        if (AsksComponent(q, "midterm", "ara sınav", "ara sinav", "vize"))
        {
            var row = FindRow(rows, "midterm", "ara sınav", "ara sinav", "vize");
            if (row != null)
                return $"Per the syllabus ({courseLabel}): **{row.Component}** is weighted at **{row.Weight}%** of your grade.";
        }

        if (AsksComponent(q, "final", "bütünleme", "butunleme"))
        {
            var row = FindRow(rows, "final", "bütünleme", "butunleme");
            if (row != null)
                return $"Per the syllabus ({courseLabel}): **{row.Component}** is weighted at **{row.Weight}%** of your grade.";
        }

        var lines = rows.Select(r => $"- {r.Component}: {r.Weight}%").ToList();
        return $"Grading breakdown from the syllabus ({courseLabel}):\n\n" + string.Join("\n", lines)
               + "\n\n(Source: Grading and Evaluation table)";
    }

    private static bool AsksComponent(string q, params string[] keys) => keys.Any(q.Contains);

    private static WeightRow? FindRow(List<WeightRow> rows, params string[] keys)
    {
        foreach (var row in rows)
        {
            var c = row.Component.ToLowerInvariant();
            if (keys.Any(k => c.Contains(k, StringComparison.Ordinal)))
                return row;
        }
        return null;
    }

    private static string ExtractGradingTableText(string fullText, IReadOnlyList<SyllabusChunk> chunks)
    {
        if (chunks.Count > 0)
        {
            var fromChunks = chunks
                .Where(c => c.NormalizedCategory == SyllabusCategories.GradingPolicy
                            || SyllabusCategoryMapper.LooksLikeGradingTable(c.Text))
                .OrderBy(c => c.ChunkIndex)
                .Select(c => c.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
            if (fromChunks.Count > 0)
                return string.Join("\n\n", fromChunks);
        }

        var m = Regex.Match(
            fullText,
            @"(?is)\b(grading\s+and\s+evaluation|grading\s*&\s*evaluation|assessment\s+and\s+grading|course\s+assessment)\b[\s\S]{0,4500}");
        return m.Success ? m.Value : string.Empty;
    }

    public static List<WeightRow> ParseWeightRows(string text)
    {
        var results = new List<WeightRow>();
        if (string.IsNullOrWhiteSpace(text))
            return results;

        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 4 || IsScheduleOrTopicLine(trimmed))
                continue;

            foreach (var (label, aliases) in KnownComponents)
            {
                if (results.Any(r => r.Component.Equals(label, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (!LineStartsWithComponent(trimmed, aliases))
                    continue;

                var weight = ExtractRowWeight(trimmed);
                if (weight.HasValue)
                    results.Add(new WeightRow(label, weight.Value));
            }
        }

        if (results.Count >= 2 && results.Sum(r => r.Weight) is >= 80 and <= 105)
            return results;

        // Compact prose: "Grading: Midterm 40%, Final 60%"
        if (results.Count == 0)
        {
            foreach (var (label, aliases) in KnownComponents)
            {
                var w = TryFindWeightInProse(text, aliases);
                if (w.HasValue)
                    results.Add(new WeightRow(label, w.Value));
            }
        }

        return DeduplicateAndValidate(results);
    }

    private static List<WeightRow> DeduplicateAndValidate(List<WeightRow> rows)
    {
        var distinct = rows
            .GroupBy(r => r.Component, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (distinct.Count >= 2)
        {
            var sum = distinct.Sum(r => r.Weight);
            if (sum is < 50 or > 110)
                return distinct.Where(r => r.Weight is <= 50).ToList();
        }

        return distinct;
    }

    private static bool IsScheduleOrTopicLine(string line)
    {
        var l = line.ToLowerInvariant();
        if (Regex.IsMatch(l, @"\bW\d{1,2}\b"))
            return true;
        if (Regex.IsMatch(l, @"\bch\.\s*\d+"))
            return true;
        if (Regex.IsMatch(l, @"\b(midterm|final|vize)\s+week\b"))
            return true;
        if (Regex.IsMatch(l, @"\bweek\s+\d{1,2}\b") && !ContainsAny(l, "weight", "scoring", "evaluation", "assessment", "%"))
            return true;
        if (Regex.IsMatch(l, @"^\s*W\d+\s+") || Regex.IsMatch(l, @"^\s*week\s+\d+\s+"))
            return true;
        return false;
    }

    private static bool LineStartsWithComponent(string line, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (alias.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(line, @"^(?i)final\s+exam\b"))
                    return true;
                if (Regex.IsMatch(line, @"^(?i)final\b") && !Regex.IsMatch(line, @"^(?i)final\s+week\b"))
                    return true;
                continue;
            }

            if (Regex.IsMatch(line, $@"^(?i)\s*{Regex.Escape(alias)}\b"))
                return true;
        }
        return false;
    }

    private static int? ExtractRowWeight(string line)
    {
        var explicitPct = Regex.Matches(line, @"(?<![Ww])(\d{1,3})\s*%");
        foreach (Match m in explicitPct)
        {
            if (int.TryParse(m.Groups[1].Value, out var pct) && pct is > 0 and <= 100)
                return pct;
        }

        var numbers = Regex.Matches(line, @"(?<![Ww.\-])(\d{1,3})\b")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(n => n is > 0 and <= 100)
            .ToList();

        if (numbers.Count == 0)
            return null;

        return numbers[^1];
    }

    private static int? TryFindWeightInProse(string text, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (alias.Equals("final", StringComparison.OrdinalIgnoreCase))
                continue;

            var pattern = $@"(?is)\b{Regex.Escape(alias)}\b[^.\n\r%]{{0,40}}?(\d{{1,3}})\s*%";
            var m = Regex.Match(text, pattern);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var pct) && pct is > 0 and <= 100)
                return pct;
        }
        return null;
    }

    private static bool ContainsAny(string source, params string[] keys) =>
        keys.Any(k => source.Contains(k, StringComparison.OrdinalIgnoreCase));

    public sealed record WeightRow(string Component, int Weight);
}
