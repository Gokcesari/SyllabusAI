using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class SyllabusRagIndexService : ISyllabusRagIndexService
{
    private readonly ApplicationDbContext _db;
    private readonly IOpenAiSyllabusClient _openAi;
    private readonly ILogger<SyllabusRagIndexService> _logger;

    public SyllabusRagIndexService(ApplicationDbContext db, IOpenAiSyllabusClient openAi, ILogger<SyllabusRagIndexService> logger)
    {
        _db = db;
        _openAi = openAi;
        _logger = logger;
    }

    public async Task ReindexCourseAsync(int courseId, string syllabusText, CancellationToken ct = default)
    {
        var old = await _db.SyllabusChunks.Where(c => c.CourseId == courseId).ToListAsync(ct);
        _db.SyllabusChunks.RemoveRange(old);

        var parts = SyllabusTextChunker.Split(syllabusText ?? "");
        for (var i = 0; i < parts.Count; i++)
        {
            _db.SyllabusChunks.Add(new SyllabusChunk
            {
                CourseId = courseId,
                ChunkIndex = i,
                Text = parts[i],
                EmbeddingJson = null
            });
        }

        await _db.SaveChangesAsync(ct);

        if (!_openAi.IsConfigured || parts.Count == 0) return;

        try
        {
            var chunks = await _db.SyllabusChunks.Where(c => c.CourseId == courseId).OrderBy(c => c.ChunkIndex).ToListAsync(ct);
            var texts = chunks.Select(c => c.Text).ToList();
            var embeddings = await _openAi.EmbedManyAsync(texts, ct);
            if (embeddings == null || embeddings.Count != chunks.Count)
            {
                _logger.LogWarning("Embedding sayısı chunk sayısıyla eşleşmedi; lexical RAG kullanılacak.");
                return;
            }

            for (var i = 0; i < chunks.Count; i++)
            {
                var vec = embeddings[i];
                chunks[i].EmbeddingJson = vec is { Length: > 0 }
                    ? JsonSerializer.Serialize(vec)
                    : null;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Syllabus embedding atlandı.");
        }
    }
}
