# -*- coding: utf-8 -*-
from pathlib import Path
p = Path(__file__).parent / "Services" / "AiService.cs"
s = p.read_text(encoding="utf-8")
marker = "    private static List<SyllabusChunk> SelectUiSources(List<SyllabusChunk> selected, HashSet<string> qTokens)"
if "TryAnswerSyllabusDocumentMeta" in s:
    print("already has method")
    raise SystemExit(0)
if marker not in s:
    raise SystemExit("marker not found")
insert = r'''
    /// <summary>
    /// Cover/footer questions (who prepared, dates) from raw syllabus text before RAG/gate.
    /// </summary>
    private static ChatResponse? TryAnswerSyllabusDocumentMeta(Course course, int sessionId, string question)
    {
        var q = question.Trim().ToLowerInvariant();
        if (!AsksSyllabusDocumentMeta(q)) return null;
        var text = course.SyllabusContent;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var extracted = TryExtractPreparedByBlock(text);
        if (string.IsNullOrWhiteSpace(extracted)) return null;

        extracted = extracted.Trim();
        if (extracted.Length > 900) extracted = extracted[..900] + "...";

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = "From the syllabus document:\n\n" + extracted,
            FromSyllabus = true,
            IsOutOfScope = false,
            RetrievalMethod = "syllabus-document-extract",
            SourceSnippets = new List<string> { TruncateForUi(extracted.Replace('\n', ' '), 180) },
            SourceSections = new List<string> { "Document information" },
            FallbackTriggered = false
        };
    }

    private static bool AsksSyllabusDocumentMeta(string q)
    {
        if (ContainsAny(q,
                "prepared by", "who prepared", "who wrote", "who made", "who authored", "who created",
                "kim haz\u0131rlad\u0131", "kim hazirladi", "haz\u0131rlayan", "hazirlayan",
                "revision date", "version of the syllabus", "version of syllabus", "document date"))
            return true;
        if (q.Contains("syllabus") && ContainsAny(q, "who", "whom", "whose", "kim"))
            return true;
        if ((q.Contains("prap") || q.Contains("prep")) && q.Contains("syllabus"))
            return true;
        return false;
    }

    private static string? TryExtractPreparedByBlock(string text)
    {
        var patterns = new[]
        {
            @"(?is)Prepared\s+by\s*[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,12}",
            @"(?is)Prepared\s+by\s*:?\s*[\s\S]{0,900}",
            @"(?is)Date\s+of\s+Preparation\s*:[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,10}",
            @"(?is)Haz[i\u0131]rlayan\s*:[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,10}",
        };
        foreach (var pat in patterns)
        {
            var m = Regex.Match(text, pat);
            if (m.Success && m.Value.Trim().Length > 8)
                return m.Value.Trim();
        }

        foreach (var key in new[] { "Prepared by", "Date of Preparation", "Haz\u0131rlayan", "Hazirlayan" })
        {
            var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var end = Math.Min(text.Length, idx + 900);
            var slice = text[idx..end];
            var doubleNl = slice.IndexOf("\n\n", StringComparison.Ordinal);
            if (doubleNl is > 40 and < 700) slice = slice[..doubleNl];
            return slice.Trim();
        }

        return null;
    }

'''
s = s.replace(marker, insert + marker, 1)
p.write_text(s, encoding="utf-8", newline="\n")
print("inserted", len(insert))
