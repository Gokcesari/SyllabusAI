using System.Text.RegularExpressions;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

/// <summary>Direct answers for a specific week topic from course calendar chunks or raw syllabus text.</summary>
public static class WeekScheduleAnswer
{
    public static ChatResponse? TryAnswer(
        string question,
        IReadOnlyList<SyllabusChunk> chunks,
        int sessionId,
        Course course,
        string? syllabusText = null)
    {
        var week = TryExtractWeekNumber(question);
        if (week is null or < 1 or > 52)
            return null;

        if (!LooksLikeWeekTopicQuestion(question))
            return null;

        var chunk = FindWeekChunk(chunks, week.Value);
        string? topic = null;
        string sectionTitle = "Course Calendar";

        if (chunk != null)
        {
            topic = ExtractTopicFromWeekChunk(chunk, week.Value);
            sectionTitle = chunk.OriginalSectionTitle ?? sectionTitle;
        }

        if (string.IsNullOrWhiteSpace(topic) && !string.IsNullOrWhiteSpace(syllabusText))
        {
            topic = TryExtractWeekTopicFromCalendarText(syllabusText, week.Value);
            sectionTitle = "Course Calendar";
        }

        if (string.IsNullOrWhiteSpace(topic))
            return null;

        var english = QuestionLanguage.IsEnglish(question);
        var answer = english
            ? $"Week {week.Value} topic ({course.CourseCode}): {topic}"
            : $"{week.Value}. hafta konusu ({course.CourseCode}): {topic}";

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = answer,
            FromSyllabus = true,
            IsOutOfScope = false,
            RetrievalMethod = "week-calendar",
            SourceSections = new List<string> { sectionTitle },
            SourceSnippets = new List<string> { Truncate(topic, 220) },
            FallbackTriggered = false
        };
    }

    public static int? TryExtractWeekNumber(string question)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0) return null;

        var m1 = Regex.Match(q, @"(?i)\bweek\s*#?\s*(\d{1,2})\b");
        if (m1.Success && int.TryParse(m1.Groups[1].Value, out var w1))
            return w1;

        var m2 = Regex.Match(q, @"(?i)\bw(\d{1,2})\b");
        if (m2.Success && int.TryParse(m2.Groups[1].Value, out var w2))
            return w2;

        var m3 = Regex.Match(q, @"(?i)(\d{1,2})\s*[\.\)]?\s*hafta");
        if (m3.Success && int.TryParse(m3.Groups[1].Value, out var w3))
            return w3;

        var m4 = Regex.Match(q, @"(?i)hafta\s*(\d{1,2})");
        if (m4.Success && int.TryParse(m4.Groups[1].Value, out var w4))
            return w4;

        return null;
    }

    public static bool SyllabusHasCalendarWeeks(string? syllabusText) =>
        !string.IsNullOrWhiteSpace(syllabusText)
        && syllabusText.Contains("Course Calendar", StringComparison.OrdinalIgnoreCase);

    public static bool ChunksMissingCalendarWeeks(IReadOnlyList<SyllabusChunk> chunks, string? syllabusText)
    {
        if (!SyllabusHasCalendarWeeks(syllabusText)) return false;
        return !chunks.Any(c =>
            c.NormalizedCategory == SyllabusCategories.WeeklySchedule
            || (c.OriginalSectionTitle?.Contains("Course Calendar", StringComparison.OrdinalIgnoreCase) == true
                && Regex.IsMatch(c.Text, @"(?im)(?:^|\n)\s*(?:W\d{1,2}|\d{1,2})\s+[A-ZÇĞİÖŞÜ]")));
    }

    internal static string? TryExtractWeekTopicFromCalendarText(string text, int week)
    {
        var cal = Regex.Match(text, @"(?is)Course\s+Calendar(.+?)(?=Matters\s+Needing\s+Attention|Date\s+of\s+Preparation|\z)");
        var body = cal.Success ? cal.Groups[1].Value : text;

        var wPat = Regex.Escape(week.ToString());
        var m = Regex.Match(body,
            $@"(?is)(?:^|\s){wPat}\s+(.+?)(?=\s+(?:[1-9]|1[0-5])\s+[A-ZÇĞİÖŞÜ]|\s+Presentation\s+(?:[1-9]|1[0-5])\s+|$)");
        if (!m.Success)
        {
            m = Regex.Match(body, $@"(?is)(?:^|\s){wPat}\s+(.{{8,900}})");
        }

        if (!m.Success) return null;
        return CleanTopic(m.Groups[1].Value);
    }

    private static bool LooksLikeWeekTopicQuestion(string question)
    {
        var q = (question ?? string.Empty).ToLowerInvariant();
        if (TryExtractWeekNumber(question) == null)
            return false;

        return ContainsAny(q,
            "topic", "topici", "konusu", "konu", "ne ", "what", "which", "cover", "covered",
            "işlenecek", "islenecek", "anlat", "ders", "schedule", "calendar", "takvim", "hafta", "subject");
    }

    private static SyllabusChunk? FindWeekChunk(IReadOnlyList<SyllabusChunk> chunks, int week)
    {
        var wLabel = $"W{week}";
        var weekLabel = $"Week {week}";

        var byTitle = chunks.FirstOrDefault(c =>
            c.OriginalSectionTitle?.Contains($"- {wLabel}", StringComparison.OrdinalIgnoreCase) == true
            || c.OriginalSectionTitle?.Contains($"- {weekLabel}", StringComparison.OrdinalIgnoreCase) == true
            || c.OriginalSectionTitle?.EndsWith(wLabel, StringComparison.OrdinalIgnoreCase) == true
            || c.OriginalSectionTitle?.EndsWith(weekLabel, StringComparison.OrdinalIgnoreCase) == true);
        if (byTitle != null) return byTitle;

        var weekNum = week.ToString();
        return chunks.FirstOrDefault(c =>
            Regex.IsMatch(c.Text, $@"(?im)^\s*\[Section:[^\]]*\]\s*\r?\n\s*{Regex.Escape(wLabel)}\b"))
            ?? chunks.FirstOrDefault(c =>
                Regex.IsMatch(c.Text, $@"(?im)^\s*\[Section:[^\]]*\]\s*\r?\n\s*{weekNum}\s+[A-ZÇĞİÖŞÜ]"))
            ?? chunks.FirstOrDefault(c =>
                c.NormalizedCategory == SyllabusCategories.WeeklySchedule
                && Regex.IsMatch(c.Text, $@"(?im)(?:^|\n)\s*{weekNum}\s+[A-ZÇĞİÖŞÜ]"))
            ?? chunks.FirstOrDefault(c => Regex.IsMatch(c.Text, $@"(?i)\b{Regex.Escape(wLabel)}\b"));
    }

    private static string? ExtractTopicFromWeekChunk(SyllabusChunk chunk, int week)
    {
        var text = chunk.Text;
        var nl = text.IndexOf('\n');
        var body = nl >= 0 ? text[(nl + 1)..].Trim() : text.Trim();

        var w = Regex.Match(body, @"(?i)^W\d+\s+(?:ON\s+)?(?:CH[-.]?\s*\d+\s*:?\s*)?(.*?)(?:\s+Quiz\b|\s+NA\b|$)");
        if (w.Success)
            return CleanTopic(w.Groups[1].Value);

        var num = Regex.Match(body, $@"^(?:week\s+)?{week}\s+(.+?)(?:\s+Presentation\s*)?$", RegexOptions.IgnoreCase);
        if (num.Success)
            return CleanTopic(num.Groups[1].Value);

        return CleanTopic(body);
    }

    private static string CleanTopic(string raw)
    {
        var t = Regex.Replace(raw ?? string.Empty, @"\s+", " ").Trim();
        t = Regex.Replace(t, @"\s+Presentation\s*$", "", RegexOptions.IgnoreCase).Trim();
        t = t.TrimEnd('.', ',', ';', ':');
        return t;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";

    private static bool ContainsAny(string source, params string[] keys) => keys.Any(source.Contains);
}

internal static class QuestionLanguage
{
    public static bool IsEnglish(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return true;
        if (Regex.IsMatch(question, @"[çğıöşüÇĞİÖŞÜ]"))
            return false;

        var q = question.ToLowerInvariant();
        if (Regex.IsMatch(q, @"\b(what|when|where|how|which|who|why|is|are|the|of|for|topic|week|tell|explain)\b"))
            return true;
        if (Regex.IsMatch(q, @"\b(kim|ne|nasıl|nasil|kaç|kac|hafta|ders|sınav|sinav|müfredat|mufredat|konusu|topici|var mı|nasıl ya)\b"))
            return false;

        return !Regex.IsMatch(q, @"\b(ve|bir|bu|için|icin|mi|mı|mu|mü)\b");
    }
}
