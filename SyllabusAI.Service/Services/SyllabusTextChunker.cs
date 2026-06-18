using System.Text;
using System.Text.RegularExpressions;

namespace SyllabusAI.Services;

public sealed class ChunkDraft
{
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OriginalSectionTitle { get; set; } = "Untitled Section";
    public string NormalizedCategory { get; set; } = SyllabusCategories.Unknown;
    public int? PageStart { get; set; }
    public int? PageEnd { get; set; }
}

public static class SyllabusTextChunker
{
    /// <summary>Small chunks so each heading / table row is indexed separately.</summary>
    public const int MaxChunkChars = 900;
    public const int MinChunkChars = 25;

    private static readonly string[] KnownHeadingHints =
    {
        "course information", "course description", "instructor", "contact", "office hours",
        "course objectives", "aims", "learning outcomes", "outcomes", "course structure",
        "communication", "digital tools", "technology", "assignment", "assignments", "deadline",
        "late submission", "attendance", "participation", "resources", "textbook", "references",
        "grading", "evaluation", "assessment", "course calendar", "weekly schedule", "course plan",
        "weekly topics", "topics covered", "matters needing attention", "academic integrity",
        "plagiarism", "cheating", "grading and evaluation", "course policies", "examination",
        "make-up", "make up", "prerequisites", "co-requisites", "syllabus", "prepared by",
        "date of preparation", "course code", "credit hours", "ects"
    };

    /// <summary>Exposes preprocessed lines for unit tests.</summary>
    public static string[] PreviewLines(string text)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        text = Regex.Replace(text, @"(?<!\n)(\bGrading\s+and\s+Evaluation\b)", "\n\n$1", RegexOptions.IgnoreCase);
        text = BreakBeforeKnownHeadings(text);
        text = BreakCourseCalendarWeeks(text);
        text = BreakBeforeWeekMarkers(text);
        text = BreakBeforeAllCapsLines(text);
        text = BreakNumberedSections(text);
        return text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
    }

    public static IReadOnlyList<ChunkDraft> SplitWithSections(string text, SyllabusCategoryMapper mapper)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ChunkDraft>();

        text = Regex.Replace(text, @"(?<!\n)(\bGrading\s+and\s+Evaluation\b)", "\n\n$1", RegexOptions.IgnoreCase);
        text = BreakBeforeKnownHeadings(text);
        text = BreakCourseCalendarWeeks(text);
        text = BreakBeforeWeekMarkers(text);
        text = BreakBeforeAllCapsLines(text);
        text = BreakNumberedSections(text);

        var lines = text.Split('\n');
        var sections = new List<(string title, StringBuilder body)>();
        var currentTitle = "General";
        var currentBody = new StringBuilder();
        var inGradingSection = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                currentBody.AppendLine();
                continue;
            }

            if (IsGradingSectionStart(line))
            {
                FlushSection(sections, ref currentTitle, ref currentBody);
                currentTitle = line.Trim(':', ' ');
                inGradingSection = true;
                continue;
            }

            if (TrySplitInlineHeading(line, out var inlineTitle, out var inlineBody))
            {
                FlushSection(sections, ref currentTitle, ref currentBody);
                currentTitle = inlineTitle;
                inGradingSection = IsGradingSectionTitle(currentTitle);
                if (!string.IsNullOrWhiteSpace(inlineBody))
                    currentBody.AppendLine(inlineBody);
                continue;
            }

            if (LooksLikeWeekCalendarRow(line))
            {
                FlushSection(sections, ref currentTitle, ref currentBody);
                var weekTitle = ExtractWeekLabel(line);
                sections.Add(($"{ResolveCalendarParentTitle(currentTitle)} — {weekTitle}", new StringBuilder(line)));
                inGradingSection = false;
                continue;
            }

            if (IsSectionHeading(line) && (!inGradingSection || IsNonGradingSectionHeading(line)))
            {
                FlushSection(sections, ref currentTitle, ref currentBody);
                currentTitle = line.Trim(':', ' ');
                inGradingSection = IsGradingSectionTitle(currentTitle);
                continue;
            }

            if (inGradingSection && LooksLikeGradingTableRow(line))
            {
                FlushSection(sections, ref currentTitle, ref currentBody);
                var rowTitle = $"{currentTitle} — {ExtractGradingRowLabel(line)}";
                sections.Add((rowTitle, new StringBuilder(line)));
                continue;
            }

            currentBody.AppendLine(line);
        }

        FlushSection(sections, ref currentTitle, ref currentBody);

        var chunks = BuildChunkDrafts(sections, mapper);
        if (chunks.Count > 0)
            return chunks;

        foreach (var part in SplitByLength(text))
        {
            chunks.Add(new ChunkDraft
            {
                ChunkIndex = chunks.Count,
                Text = $"[Section: General]\n{part}",
                OriginalSectionTitle = "General",
                NormalizedCategory = mapper.Map("General", part)
            });
        }

        return chunks;
    }

    private static List<ChunkDraft> BuildChunkDrafts(List<(string title, StringBuilder body)> sections, SyllabusCategoryMapper mapper)
    {
        var chunks = new List<ChunkDraft>();
        var order = 0;

        foreach (var section in sections)
        {
            var title = string.IsNullOrWhiteSpace(section.title) ? "General" : section.title.Trim();
            var body = section.body.ToString().Trim();
            if (body.Length == 0) continue;

            var parts = SplitByLength(body);
            foreach (var part in parts)
            {
                var chunkBody = $"[Section: {title}]\n{part}";
                chunks.Add(new ChunkDraft
                {
                    ChunkIndex = order++,
                    Text = chunkBody,
                    OriginalSectionTitle = title,
                    NormalizedCategory = mapper.Map(title, chunkBody)
                });
            }
        }

        return chunks;
    }

    private static void FlushSection(List<(string title, StringBuilder body)> sections, ref string currentTitle, ref StringBuilder currentBody)
    {
        if (currentBody.Length > 0)
            sections.Add((currentTitle, currentBody));
        currentBody = new StringBuilder();
    }

    private static bool IsGradingSectionStart(string line)
    {
        var t = line.Trim().ToLowerInvariant();
        return t.StartsWith("grading and evaluation", StringComparison.Ordinal)
               || t.StartsWith("grading & evaluation", StringComparison.Ordinal);
    }

    private static bool IsNonGradingSectionHeading(string line)
    {
        var t = line.ToLowerInvariant();
        return ContainsAnyLower(t, "attendance", "integrity", "instructor", "calendar", "resources",
            "objectives", "outcomes", "communication", "prepared by", "course information", "weekly",
            "late submission", "academic", "make-up", "make up", "prerequisites");
    }

    private static bool IsGradingSectionTitle(string title)
    {
        var t = (title ?? string.Empty).ToLowerInvariant();
        return IsGradingSectionStart(title ?? string.Empty)
               || ContainsAnyLower(t, "grading and evaluation", "grading & evaluation")
               || (ContainsAnyLower(t, "grading", "evaluation", "assessment") && ContainsAnyLower(t, "weight", "scoring", "%"));
    }

    private static string ResolveCalendarParentTitle(string currentTitle)
    {
        var t = (currentTitle ?? string.Empty).ToLowerInvariant();
        if (ContainsAnyLower(t, "calendar", "weekly", "schedule", "week", "topics", "plan"))
            return string.IsNullOrWhiteSpace(currentTitle) ? "Course Calendar" : currentTitle;
        return "Course Calendar";
    }

    private static string ExtractGradingRowLabel(string line)
    {
        var m = Regex.Match(line, @"^(?i)(midterm|final\s*exam|quiz|process|assignments?|presentations?)");
        return m.Success ? CultureTitle(m.Groups[1].Value) : "Row";
    }

    private static string ExtractWeekLabel(string line)
    {
        var m = Regex.Match(line, @"^(?i)(week\s+\d+|w\d{1,2})");
        if (m.Success) return m.Groups[1].Value.ToUpperInvariant().Replace("WEEK ", "Week ", StringComparison.OrdinalIgnoreCase);

        var num = Regex.Match(line, @"^(\d{1,2})\s+[A-ZÇĞİÖŞÜ]");
        if (num.Success) return $"Week {num.Groups[1].Value}";
        return "Week";
    }

    private static string CultureTitle(string s)
    {
        s = s.Trim();
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static string BreakBeforeKnownHeadings(string text)
    {
        // Single-word hints match inside real headings (e.g. "evaluation" in "Grading and Evaluation") — multi-word only.
        foreach (var heading in KnownHeadingHints.Where(h => h.Contains(' ', StringComparison.Ordinal))
                     .OrderByDescending(h => h.Length))
        {
            var escaped = Regex.Escape(heading);
            text = Regex.Replace(text, $@"(?<!\n)\s+({escaped})\s*:", "\n\n$1:", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"(?<!\n)\n?({escaped})\s*\n", "\n\n$1\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"(?<!\n)({escaped})(?=\s+[A-Z])", "\n\n$1", RegexOptions.IgnoreCase);
        }
        return text;
    }

    private static string BreakBeforeWeekMarkers(string text) =>
        Regex.Replace(text, @"(?<!\n)(\bW\d{1,2}\b\s+)", "\n$1", RegexOptions.IgnoreCase);

    /// <summary>Splits "Course Calendar ... 1 Topic ... 2 Topic" blocks into one line per week.</summary>
    private static string BreakCourseCalendarWeeks(string text)
    {
        var m = Regex.Match(text, @"(?is)(Course\s+Calendar)(.+?)(?=Matters\s+Needing\s+Attention|Date\s+of\s+Preparation|\z)");
        if (!m.Success) return text;

        var header = m.Groups[1].Value;
        var body = m.Groups[2].Value;
        var broken = Regex.Replace(body, @"(?<=\s)(?<!\d)([1-9]|1[0-5])\s+(?=[A-ZÇĞİÖŞÜ])", "\n$1 ");
        var replacement = header + broken;
        return text[..m.Index] + replacement + text[(m.Index + m.Length)..];
    }

    private static string BreakBeforeAllCapsLines(string text) =>
        Regex.Replace(text, @"(?<=\n)([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9\s&\-]{3,55})(?=\n)", "\n\n$1\n", RegexOptions.Multiline);

    private static string BreakNumberedSections(string text) =>
        Regex.Replace(text, @"(?<!\n)(\d+(?:\.\d+){1,3}\s+[A-ZÇĞİÖŞÜ])", "\n\n$1", RegexOptions.IgnoreCase);

    private static bool LooksLikeGradingTableRow(string line)
    {
        if (line.Length < 6 || line.Length > 350) return false;
        if (IsScheduleOrTopicLine(line)) return false;
        return Regex.IsMatch(line, @"^(?i)(midterm|final\s*exam|quiz|process|assignments?|presentations?)\b");
    }

    private static bool IsScheduleOrTopicLine(string line)
    {
        var l = line.ToLowerInvariant();
        if (Regex.IsMatch(l, @"\bW\d{1,2}\b") && !Regex.IsMatch(l, @"weight|scoring|evaluation"))
            return true;
        if (Regex.IsMatch(l, @"\bch\.\s*\d+"))
            return true;
        if (Regex.IsMatch(l, @"\b(midterm|final|vize)\s+week\b"))
            return true;
        return false;
    }

    private static bool LooksLikeWeekCalendarRow(string line)
    {
        if (line.Length < 4 || line.Length > 900) return false;
        if (LooksLikeGradingTableRow(line)) return false;
        if (Regex.IsMatch(line, @"^(?i)(week\s+\d+|w\d{1,2})\b")) return true;
        // Numeric week rows: "4 Navigation Database..." (not "4ICAO" or "100 40")
        return Regex.IsMatch(line, @"^(?:week\s+)?(\d{1,2})\s+[A-ZÇĞİÖŞÜ]");
    }

    private static bool ContainsAnyLower(string s, params string[] keys)
    {
        var t = s.ToLowerInvariant();
        return keys.Any(t.Contains);
    }

    private static bool TrySplitInlineHeading(string line, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;

        var idx = line.IndexOf(':');
        if (idx <= 0 || idx > 70) return false;

        var left = line.Substring(0, idx).Trim();
        var right = line.Substring(idx + 1).Trim();

        if (left.Length < 3 || left.Length > 70) return false;
        if (!IsKnownHeading(left)) return false;

        title = left;
        body = right;
        return true;
    }

    private static bool IsKnownHeading(string text)
    {
        var t = text.ToLowerInvariant();
        return KnownHeadingHints.Any(h => t.Contains(h, StringComparison.Ordinal));
    }

    private static bool IsSectionHeading(string line) =>
        IsKnownHeading(line) || LooksLikeHeading(line);

    private static bool LooksLikeHeading(string line)
    {
        if (line.Length < 3 || line.Length > 100) return false;
        if (Regex.IsMatch(line, @"^(week|w\d+|section)\s*\d*\s*$", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(line, @"^(week|w\d{1,2})\s+\S", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(line, @"^\d+(\.\d+){0,3}\s+\S", RegexOptions.IgnoreCase)) return true;
        if (IsKnownHeading(line)) return true;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 14) return false;

        if (line.Count(c => c == '|' || c == '\t') >= 2) return false;

        var punctuationHeavy = line.Count(char.IsPunctuation) > 4;
        if (punctuationHeavy && !line.EndsWith(':')) return false;

        var titleCaseWords = words.Count(w => char.IsLetter(w[0]) && char.IsUpper(w[0]));
        if (titleCaseWords >= Math.Max(1, words.Length - 2)) return true;

        return line == line.ToUpperInvariant() && line.Any(char.IsLetter) && words.Length <= 8;
    }

    private static IReadOnlyList<string> SplitByLength(string text)
    {
        var paragraphs = text.Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            var value = current.ToString().Trim();
            if (value.Length >= MinChunkChars)
                chunks.Add(value);
            else if (value.Length > 0 && chunks.Count > 0)
                chunks[^1] = chunks[^1] + "\n" + value;
            else if (value.Length > 0)
                chunks.Add(value);
            current.Clear();
        }

        foreach (var p in paragraphs)
        {
            if (current.Length > 0 && current.Length + p.Length + 2 > MaxChunkChars)
                Flush();

            if (p.Length > MaxChunkChars)
            {
                Flush();
                for (var i = 0; i < p.Length; i += MaxChunkChars)
                {
                    var len = Math.Min(MaxChunkChars, p.Length - i);
                    var part = p.Substring(i, len).Trim();
                    if (part.Length >= MinChunkChars)
                        chunks.Add(part);
                    else if (part.Length > 0 && chunks.Count > 0)
                        chunks[^1] += "\n" + part;
                }
                continue;
            }

            if (current.Length > 0) current.Append('\n');
            current.Append(p);
        }

        Flush();
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(text))
            chunks.Add(text.Trim());

        return chunks;
    }
}
