namespace SyllabusAI.Services;

public interface ISyllabusRagIndexService
{
    /// <summary>Eski chunk'ları siler, metni yeniden böler; OpenAI anahtarı varsa embedding yazar.</summary>
    Task ReindexCourseAsync(int courseId, string syllabusText, CancellationToken ct = default);
}
