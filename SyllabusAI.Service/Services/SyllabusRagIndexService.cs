using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class SyllabusRagIndexService : ISyllabusRagIndexService
{
    private readonly ApplicationDbContext _db;
    private readonly IOpenAiSyllabusClient _openAi;
    private readonly SyllabusCategoryMapper _categoryMapper;
    private readonly ILogger<SyllabusRagIndexService> _logger;

    public SyllabusRagIndexService(
        ApplicationDbContext db,
        IOpenAiSyllabusClient openAi,
        SyllabusCategoryMapper categoryMapper,
        ILogger<SyllabusRagIndexService> logger)
    {
        _db = db;
        _openAi = openAi;
        _categoryMapper = categoryMapper;
        _logger = logger;
    }

    public async Task ReindexCourseAsync(int courseId, string syllabusText, CancellationToken ct = default)
    {
        var old = await _db.SyllabusChunks.Where(c => c.CourseId == courseId).ToListAsync(ct);
        _db.SyllabusChunks.RemoveRange(old);

        var drafts = SyllabusTextChunker.SplitWithSections(syllabusText ?? string.Empty, _categoryMapper);
        foreach (var draft in drafts)
        {
            _db.SyllabusChunks.Add(new SyllabusChunk
            {
                CourseId = courseId,
                ChunkIndex = draft.ChunkIndex,
                Text = draft.Text,
                OriginalSectionTitle = draft.OriginalSectionTitle,
                NormalizedCategory = draft.NormalizedCategory,
                PageStart = draft.PageStart,
                PageEnd = draft.PageEnd,
                EmbeddingJson = null
            });
        }

        await _db.SaveChangesAsync(ct);

        if (!_openAi.IsConfigured || drafts.Count == 0) return;

        try
        {
            var chunks = await _db.SyllabusChunks
                .Where(c => c.CourseId == courseId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(ct);

            var embeddings = await _openAi.EmbedManyAsync(chunks.Select(c => c.Text).ToList(), ct);
            if (embeddings == null || embeddings.Count != chunks.Count)
            {
                _logger.LogWarning("Embedding count mismatch, falling back to lexical RAG for course {CourseId}", courseId);
                return;
            }

            for (var i = 0; i < chunks.Count; i++)
            {
                var vec = embeddings[i];
                chunks[i].EmbeddingJson = vec is { Length: > 0 } ? JsonSerializer.Serialize(vec) : null;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for course {CourseId}", courseId);
        }
    }
}
