using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;

namespace SyllabusAI.Services;

/// <summary>
/// Müfredat içeriğine göre öğrenci sorusunu basit bir dille cevaplar.
/// Gerçek AI entegrasyonu (OpenAI/Azure) eklenene kadar müfredat metninden anahtar eşleştirme ile örnek cevap üretir.
/// </summary>
public class AiService : IAiService
{
    private readonly ApplicationDbContext _db;

    public AiService(ApplicationDbContext db) => _db = db;

    public async Task<ChatResponse> AskAsync(int userId, ChatRequest request, CancellationToken ct = default)
    {
        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == request.CourseId, ct);
        if (!allowed)
            return new ChatResponse { Answer = "Bu derse erişim yetkiniz yok.", FromSyllabus = false };

        var course = await _db.Courses.FindAsync(new object[] { request.CourseId }, ct);
        if (course == null || string.IsNullOrWhiteSpace(course.SyllabusContent))
            return new ChatResponse { Answer = "Bu ders için müfredat henüz eklenmemiş.", FromSyllabus = false };

        var syllabus = course.SyllabusContent;
        var question = (request.Question ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(question))
            return new ChatResponse { Answer = "Lütfen bir soru yazın.", FromSyllabus = false };

        // Basit anahtar eşleştirme: soruda geçen kelimelere göre müfredattan ilgili cümleleri bul, basitleştirilmiş cevap ver
        var words = question.Split(new[] { ' ', '?', '.', ',', ':' }, StringSplitOptions.RemoveEmptyEntries);
        var syllabusLower = syllabus.ToLowerInvariant();
        var relevantParts = new List<string>();
        foreach (var word in words)
        {
            if (word.Length < 3) continue;
            var idx = syllabusLower.IndexOf(word, StringComparison.Ordinal);
            if (idx < 0) continue;
            var start = Math.Max(0, idx - 30);
            var len = Math.Min(200, syllabus.Length - start);
            var snippet = syllabus.Substring(start, len).Trim();
            if (snippet.Length > 20 && !relevantParts.Contains(snippet))
                relevantParts.Add(snippet);
        }

        if (relevantParts.Count > 0)
        {
            var combined = string.Join(" … ", relevantParts.Take(2));
            if (combined.Length > 300) combined = combined.Substring(0, 297) + "...";
            return new ChatResponse
            {
                Answer = "Müfredata göre: " + combined,
                FromSyllabus = true
            };
        }

        // Müfredatta eşleşme yoksa genel yanıt (rapor: safe fallback)
        return new ChatResponse
        {
            Answer = "Bu soru için müfredatta net bir ifade bulamadım. Lütfen müfredat metnindeki ilgili bölüme bakın veya sorunuzu farklı kelimelerle tekrar deneyin.",
            FromSyllabus = false
        };
    }
}
