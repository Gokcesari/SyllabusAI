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
    public const int MaxChunkChars = 1200;
    public const int MinChunkChars = 80;

    private static readonly string[] KnownHeadingHints =
    {
        "course information", "instructor", "contact", "course objectives", "learning outcomes",
        "course structure", "communication", "digital tools", "assignment", "deadline",
        "attendance", "resources", "grading", "evaluation", "course calendar",
        "weekly", "matters needing attention", "academic integrity", "plagiarism", "cheating"
    };

    public static IReadOnlyList<ChunkDraft> SplitWithSections(string text, SyllabusCategoryMapper mapper)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<ChunkDraft>();

        // Insert safe line breaks before known inline headings to avoid one-big-general chunk.
        text = BreakBeforeKnownHeadings(text);

        var lines = text.Split('\n');
        var sections = new List<(string title, StringBuilder body)>();
        var currentTitle = "General";
        var currentBody = new StringBuilder();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                currentBody.AppendLine();
                continue;
            }

            if (TrySplitInlineHeading(line, out var inlineTitle, out var inlineBody))
            {
                FlushSectionIfNeeded(sections, ref currentTitle, ref currentBody);
                currentTitle = inlineTitle;
                if (!string.IsNullOrWhiteSpace(inlineBody))
                    currentBody.AppendLine(inlineBody);
                continue;
            }

            if (LooksLikeHeading(line))
            {
                FlushSectionIfNeeded(sections, ref currentTitle, ref currentBody);
                currentTitle = line.Trim(':', ' ');
                continue;
            }

            currentBody.AppendLine(line);
        }

        if (currentBody.Length > 0)
            sections.Add((currentTitle, currentBody));

        var chunks = new List<ChunkDraft>();
        var order = 0;

        foreach (var section in sections)
        {
            var title = string.IsNullOrWhiteSpace(section.title) ? "General" : section.title;
            var parts = SplitByLength(section.body.ToString());
            foreach (var part in parts)
            {
                chunks.Add(new ChunkDraft
                {
                    ChunkIndex = order++,
                    Text = part,
                    OriginalSectionTitle = title,
                    NormalizedCategory = mapper.Map(title, part)
                });
            }
        }

        if (chunks.Count == 0)
        {
            chunks.Add(new ChunkDraft
            {
                ChunkIndex = 0,
                Text = text,
                OriginalSectionTitle = "General",
                NormalizedCategory = mapper.Map("General", text)
            });
        }

        return chunks;
    }

    private static void FlushSectionIfNeeded(List<(string title, StringBuilder body)> sections, ref string currentTitle, ref StringBuilder currentBody)
    {
        if (currentBody.Length > 0)
            sections.Add((currentTitle, currentBody));
        currentBody = new StringBuilder();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private static string BreakBeforeKnownHeadings(string text)
    {
        foreach (var heading in KnownHeadingHints)
        {
            var escaped = Regex.Escape(heading);
            text = Regex.Replace(text, $"(?<!\\n)\\s+({escaped})\\s*:", "\n$1:", RegexOptions.IgnoreCase);
        }

        return text;
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
        if (!LooksLikeHeading(left) && !IsKnownHeading(left)) return false;

        title = left;
        body = right;
        return true;
    }

    private static bool IsKnownHeading(string text)
    {
        var t = text.ToLowerInvariant();
        return KnownHeadingHints.Any(h => t.Contains(h, StringComparison.Ordinal));
    }

    private static bool LooksLikeHeading(string line)
    {
        if (line.Length < 3 || line.Length > 100) return false;
        if (Regex.IsMatch(line, "^(week|w\\d+|section)\\b", RegexOptions.IgnoreCase)) return true;
        if (IsKnownHeading(line)) return true;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 12) return false;

        var punctuationHeavy = line.Count(char.IsPunctuation) > 4;
        if (punctuationHeavy && !line.EndsWith(':')) return false;

        var titleCaseWords = words.Count(w => char.IsLetter(w[0]) && char.IsUpper(w[0]));
        return titleCaseWords >= Math.Max(1, words.Length - 3);
    }

    private static IReadOnlyList<string> SplitByLength(string text)
    {
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            var value = current.ToString().Trim();
            if (value.Length >= MinChunkChars)
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
                }
                continue;
            }

            if (current.Length > 0) current.Append("\n\n");
            current.Append(p);
        }

        Flush();
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(text))
            chunks.Add(text.Trim());

        return chunks;
    }
}
