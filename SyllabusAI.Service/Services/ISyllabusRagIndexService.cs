namespace SyllabusAI.Services;

public interface ISyllabusRagIndexService
{
    Task ReindexCourseAsync(int courseId, string syllabusText, CancellationToken ct = default);
}
