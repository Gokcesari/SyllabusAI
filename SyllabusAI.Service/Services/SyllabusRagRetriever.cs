using System.Text.RegularExpressions;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public sealed class SyllabusRagRetriever
{
    private const int DefaultTopK = 8;

    public RetrievalResult Retrieve(IReadOnlyList<SyllabusChunk> chunks, string question, string categoryHint, bool useEmbeddings, float[]? queryEmbedding)
    {
        var qTokens = Tokenize(question);
        var hint = categoryHint;

        if (hint == SyllabusCategories.GradingPolicy)
            return RetrieveGrading(chunks, qTokens);

        var pool = BuildPool(chunks, hint);
        var takeK = hint == SyllabusCategories.WeeklySchedule ? 12 : DefaultTopK;
        var weekBoost = WeekScheduleAnswer.TryExtractWeekNumber(question);

        if (useEmbeddings && queryEmbedding is { Length: > 0 } && pool.All(c => !string.IsNullOrWhiteSpace(c.EmbeddingJson)))
        {
            var ranked = pool
                .Select(c => (Chunk: c, Score: CosineSimilarity(queryEmbedding, ParseEmbedding(c.EmbeddingJson!)) + WeekMatchBoost(c, weekBoost)))
                .OrderByDescending(x => x.Score)
                .Take(takeK)
                .ToList();
            return new RetrievalResult(ranked, "embedding");
        }

        var lexical = RankLexical(pool, qTokens, takeK)
            .Select(x => (Chunk: x.Chunk, Score: x.Score + WeekMatchBoost(x.Chunk, weekBoost)))
            .OrderByDescending(x => x.Score)
            .Take(takeK)
            .ToList();
        return new RetrievalResult(lexical, "lexical");
    }

    private static double WeekMatchBoost(SyllabusChunk chunk, int? week)
    {
        if (week is null or < 1) return 0;
        var n = week.Value;
        var label = $"W{n}";
        var weekLabel = $"Week {n}";
        var title = chunk.OriginalSectionTitle ?? string.Empty;
        if (title.Contains($"- {label}", StringComparison.OrdinalIgnoreCase)
            || title.Contains($"- {weekLabel}", StringComparison.OrdinalIgnoreCase)
            || title.EndsWith(label, StringComparison.OrdinalIgnoreCase)
            || title.EndsWith(weekLabel, StringComparison.OrdinalIgnoreCase))
            return 10.0;
        if (Regex.IsMatch(chunk.Text, $@"(?im)(?:^|\n)\s*{n}\s+[A-ZÇĞİÖŞÜ]"))
            return 9.0;
        return Regex.IsMatch(chunk.Text, $@"(?i)\b{Regex.Escape(label)}\b") ? 8.0 : 0;
    }

    private static RetrievalResult RetrieveGrading(IReadOnlyList<SyllabusChunk> chunks, HashSet<string> qTokens)
    {
        var candidates = chunks
            .Where(c =>
                c.NormalizedCategory == SyllabusCategories.GradingPolicy
                || SectionTitleLooksGrading(c.OriginalSectionTitle)
                || SyllabusCategoryMapper.LooksLikeGradingTable(c.Text))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = chunks
                .Where(c => ContainsGradingSignals(c.Text.ToLowerInvariant()))
                .ToList();
        }

        if (candidates.Count == 0)
            candidates = chunks.ToList();

        var ranked = RankLexical(candidates, qTokens, 12);
        // Tablo parçalarına ek ağırlık
        var boosted = ranked
            .Select(x => (
                Chunk: x.Chunk,
                Score: x.Score
                + (SyllabusCategoryMapper.LooksLikeGradingTable(x.Chunk.Text) ? 5.0 : 0)
                + (SectionTitleLooksGrading(x.Chunk.OriginalSectionTitle) ? 3.0 : 0)))
            .OrderByDescending(x => x.Score)
            .Take(12)
            .ToList();

        return new RetrievalResult(boosted, "grading-lexical");
    }

    private static List<SyllabusChunk> BuildPool(IReadOnlyList<SyllabusChunk> chunks, string hint)
    {
        if (hint == SyllabusCategories.Unknown)
            return chunks.ToList();

        var category = chunks.Where(c => c.NormalizedCategory == hint).ToList();
        var signal = chunks.Where(c => MatchesHintSignals(c, hint)).ToList();
        var merged = category.Concat(signal).GroupBy(c => c.Id).Select(g => g.First()).ToList();
        return merged.Count > 0 ? merged : chunks.ToList();
    }

    private static bool MatchesHintSignals(SyllabusChunk chunk, string hint)
    {
        var t = chunk.Text.ToLowerInvariant();
        return hint switch
        {
            var h when h == SyllabusCategories.AttendancePolicy => ContainsAny(t, "attendance", "devam", "yoklama"),
            var h when h == SyllabusCategories.AssignmentPolicy => ContainsAny(t, "deadline", "homework", "ödev", "odev", "teslim", "late submission"),
            var h when h == SyllabusCategories.WeeklySchedule => ContainsAny(t, "week ", "w1", "w2", "w3", "w4", "w5", "w6", "w7", "w8", "w9", "w10", "w11", "w12", "w13", "w14", "w15", "calendar", "topics covered", "chapter"),
            var h when h == SyllabusCategories.InstructorInfo => ContainsAny(t, "instructor", "office", "e-mail", "email", "contact"),
            var h when h == SyllabusCategories.LearningOutcomes => ContainsAny(t, "outcome", "learning", "objective"),
            var h when h == SyllabusCategories.AcademicIntegrity => ContainsAny(t, "integrity", "plagiarism", "cheating"),
            _ => false
        };
    }

    private static bool SectionTitleLooksGrading(string? title)
    {
        var t = (title ?? string.Empty).ToLowerInvariant();
        return ContainsAny(t, "grading", "evaluation", "assessment", "not ", "değerlendirme", "degerlendirme");
    }

    private static bool ContainsGradingSignals(string textLower) =>
        ContainsAny(textLower, "%", "midterm", "final exam", "final ", "quiz", "weight (%)", "weight(%)", "grading", "assessment", "evaluation",
            "ara sınav", "vize", "değerlendirme", "yüzde", "ağırlık", "agirlik", "scoring");

    private static List<(SyllabusChunk Chunk, double Score)> RankLexical(List<SyllabusChunk> chunks, HashSet<string> qTokens, int takeK)
    {
        return chunks
            .Select(c => (Chunk: c, Score: LexicalScore(qTokens, c.Text.ToLowerInvariant())))
            .OrderByDescending(x => x.Score)
            .Take(takeK)
            .ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var parts = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9çğıöşü]+", RegexOptions.CultureInvariant);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in parts)
            if (p.Length >= 2) set.Add(p);
        return set;
    }

    private static double LexicalScore(HashSet<string> qTokens, string chunkLower)
    {
        double score = 0;
        foreach (var t in qTokens)
            if (chunkLower.Contains(t, StringComparison.Ordinal)) score += 1;
        return score;
    }

    private static float[] ParseEmbedding(string json) => System.Text.Json.JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var d = Math.Sqrt(na) * Math.Sqrt(nb);
        return d < 1e-9 ? 0 : dot / d;
    }

    private static bool ContainsAny(string source, params string[] keys) => keys.Any(source.Contains);
}

public sealed record RetrievalResult(IReadOnlyList<(SyllabusChunk Chunk, double Score)> Ranked, string Method);
