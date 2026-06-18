namespace SyllabusAI.Services;

public class QuestionCategoryHintService
{
    public static bool IsGradingQuestion(string? question) =>
        PredictCategory(question) == SyllabusCategories.GradingPolicy;

    public string Predict(string? question) => PredictCategory(question);

    private static string PredictCategory(string? question)
    {
        var q = (question ?? string.Empty).ToLowerInvariant();

        if (HasAny(q,
                "grade", "grading", "percent", "percentage", "yuzde", "yüzde", "midterm", "final", "quiz", "project", "weight",
                "notu", "notum", "notumu", "notun", "ders notu", "harf notu", "genel not", "ortalama", "başarı notu", "basari notu",
                "puan", "ağırlık", "agirlik", "değerlendirme", "degerlendirme", "sınav", "sinav", "vize", "bütünleme", "butunleme",
                "etkiliyor", "kaç puan", "kac puan", "geçme notu", "gecme notu", "kopya", "intihal"))
            return SyllabusCategories.GradingPolicy;
        if (HasAny(q, "week", "schedule", "calendar", "which week", "hangi hafta", "topic", "plan", "haftalık", "haftalik", "takvim"))
            return SyllabusCategories.WeeklySchedule;
        if (HasAny(q, "late", "deadline", "due", "submit", "submission", "upload", "homework", "assignment", "ödev", "odev", "teslim"))
            return SyllabusCategories.AssignmentPolicy;
        if (HasAny(q, "attendance", "devam", "absence", "absent", "excused", "participation", "yoklama", "devamsızlık", "devamsizlik"))
            return SyllabusCategories.AttendancePolicy;
        if (HasAny(q, "office", "email", "mail", "instructor", "lecturer", "teacher", "contact", "ofis saati", "e-posta", "eposta", "hoca", "öğretim üyesi", "ogretim uyesi"))
            return SyllabusCategories.InstructorInfo;
        if (HasAny(q, "plagiarism", "cheating", "integrity", "ethic", "intihal", "kopya"))
            return SyllabusCategories.AcademicIntegrity;
        if (HasAny(q, "syllabus", "müfredat", "mufredat", "ders içeriği", "ders icerigi", "learning outcome", "kazanım", "kazanim"))
            return SyllabusCategories.WeeklySchedule;
        if (HasAny(q, "ne zaman", "nasıl", "nasil", "zorunlu", "gerekli", "şart", "sart"))
            return SyllabusCategories.AssignmentPolicy;

        return SyllabusCategories.Unknown;
    }

    private static bool HasAny(string source, params string[] keys) => keys.Any(source.Contains);
}
