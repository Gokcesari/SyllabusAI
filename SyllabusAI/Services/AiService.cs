using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

/// <summary>
/// RAG: chunk tablosundan ilgili parçaları seçer; OpenAI anahtarı varsa özet cevap, yoksa parçaları doğrudan sunar.
/// </summary>
public class AiService : IAiService
{
    private const int TopK = 4;

    private readonly ApplicationDbContext _db;
    private readonly ISyllabusRagIndexService _ragIndex;
    private readonly IOpenAiSyllabusClient _openAi;

    public AiService(ApplicationDbContext db, ISyllabusRagIndexService ragIndex, IOpenAiSyllabusClient openAi)
    {
        _db = db;
        _ragIndex = ragIndex;
        _openAi = openAi;
    }

    public async Task<ChatResponse> AskAsync(int userId, ChatRequest request, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == request.CourseId, ct);
        if (!allowed)
            return Deny("Bu derse erişim yetkiniz yok.");

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct);
        if (course == null || string.IsNullOrWhiteSpace(course.SyllabusContent))
            return Deny("Bu ders için müfredat henüz eklenmemiş.");

        var question = (request.Question ?? "").Trim();
        if (string.IsNullOrEmpty(question))
            return Deny("Lütfen bir soru yazın.");

        var hasChunks = await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (!hasChunks)
            await _ragIndex.ReindexCourseAsync(course.Id, course.SyllabusContent, ct);

        var chunks = await _db.SyllabusChunks.AsNoTracking()
            .Where(c => c.CourseId == request.CourseId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        if (chunks.Count == 0)
            return Deny("Müfredat metni işlenemedi (parça oluşturulamadı).");

        var qTokens = Tokenize(question);

        var allHaveEmb = chunks.All(c => !string.IsNullOrEmpty(c.EmbeddingJson));
        List<(SyllabusChunk Chunk, double Score)> ranked;
        var method = "lexical";
        var fallback = false;

        if (_openAi.IsConfigured && allHaveEmb)
        {
            var qVec = await _openAi.EmbedOneAsync(question, ct);
            if (qVec is { Length: > 0 })
            {
                ranked = chunks
                    .Select(c => (c, CosineSimilarity(qVec, ParseEmbedding(c.EmbeddingJson!))))
                    .OrderByDescending(x => x.Item2)
                    .Take(TopK)
                    .ToList();
                method = "embedding";
            }
            else
                ranked = RankLexical(chunks, qTokens);
        }
        else
            ranked = RankLexical(chunks, qTokens);

        if (ranked.Count == 0 || ranked.All(x => x.Score <= 0))
        {
            ranked = chunks.Take(TopK).Select(c => (c, 0.001)).ToList();
            fallback = true;
        }

        var context = string.Join("\n\n---\n\n", ranked.Select(x => $"[Parça {x.Chunk.ChunkIndex + 1}]\n{x.Chunk.Text}"));
        var sourceSnippets = ranked.Select(x => TruncateForUi(x.Chunk.Text, 180)).ToList();

        string answer;
        if (_openAi.IsConfigured)
        {
            const string system = """
                Sen üniversite ders asistanısın. Yalnızca verilen müfredat parçalarına dayanarak Türkçe, kısa ve net cevap ver.
                Parçalarda cevap yoksa tam olarak: "Müfredatta bunun için net bilgi bulamadım." deyip hangi konuda arama yapılabileceğini tek cümleyle öner.
                Sayı, tarih, yüzde verirken metindeki ifadeleri olduğu gibi kullan; uydurma.
                """;
            var userMsg = $"Bağlam parçaları:\n{context}\n\nÖğrenci sorusu:\n{question}";
            answer = await _openAi.ChatAsync(system, userMsg, ct) ?? BuildFallbackAnswer(ranked, question);
            if (string.IsNullOrWhiteSpace(answer))
                answer = BuildFallbackAnswer(ranked, question);
        }
        else
            answer = BuildFallbackAnswer(ranked, question);

        return new ChatResponse
        {
            Answer = answer,
            FromSyllabus = true,
            RetrievalMethod = method,
            SourceSnippets = sourceSnippets,
            FallbackTriggered = fallback
        };
    }

    private static ChatResponse Deny(string msg) => new()
    {
        Answer = msg,
        FromSyllabus = false,
        RetrievalMethod = "none",
        FallbackTriggered = true
    };

    private static List<(SyllabusChunk Chunk, double Score)> RankLexical(List<SyllabusChunk> chunks, HashSet<string> qTokens)
    {
        return chunks
            .Select(c => (c, LexicalScore(qTokens, c.Text.ToLowerInvariant())))
            .OrderByDescending(x => x.Item2)
            .Take(TopK)
            .ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var parts = Regex.Split(text.ToLowerInvariant(), @"[^a-zçğıöşü0-9]+", RegexOptions.CultureInvariant);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in parts)
        {
            if (p.Length >= 2)
                set.Add(p);
        }
        return set;
    }

    private static double LexicalScore(HashSet<string> qTokens, string chunkLower)
    {
        double s = 0;
        foreach (var t in qTokens)
        {
            if (t.Length < 2) continue;
            if (chunkLower.Contains(t, StringComparison.Ordinal))
                s += 1;
        }
        return s;
    }

    private static float[] ParseEmbedding(string json) =>
        JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();

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

    private static string BuildFallbackAnswer(List<(SyllabusChunk Chunk, double Score)> ranked, string question)
    {
        var intro = "Müfredattan seçilen ilgili bölümler (OpenAI anahtarı yoksa veya model yanıt vermediyse ham metin gösterilir):\n\n";
        var body = string.Join("\n\n…\n\n", ranked.Take(3).Select(x => x.Chunk.Text.Trim()));
        if (body.Length > 2000)
            body = body[..2000] + "…";
        return intro + body;
    }

    private static string TruncateForUi(string text, int max)
    {
        text = text.Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }
}
