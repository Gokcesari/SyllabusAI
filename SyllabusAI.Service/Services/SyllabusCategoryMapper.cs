namespace SyllabusAI.Services;

public class SyllabusCategoryMapper
{
    public string Map(string? sectionTitle, string chunkText)
    {
        var title = (sectionTitle ?? string.Empty).ToLowerInvariant();
        var text = (chunkText ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(title, "grading", "evaluation", "assessment", "score", "weight", "midterm", "final", "quiz"))
            return SyllabusCategories.GradingPolicy;
        if (ContainsAny(title, "weekly", "calendar", "schedule", "topics", "course plan", "week"))
            return SyllabusCategories.WeeklySchedule;
        if (ContainsAny(title, "attendance", "continuity"))
            return SyllabusCategories.AttendancePolicy;
        if (ContainsAny(title, "assignment", "submission", "deadline", "project", "homework", "late"))
            return SyllabusCategories.AssignmentPolicy;
        if (ContainsAny(title, "instructor", "lecturer", "office", "contact", "email"))
            return SyllabusCategories.InstructorInfo;
        if (ContainsAny(title, "objective", "aim"))
            return SyllabusCategories.Objectives;
        if (ContainsAny(title, "outcome", "learning"))
            return SyllabusCategories.LearningOutcomes;
        if (ContainsAny(title, "resource", "reading", "textbook", "reference"))
            return SyllabusCategories.Resources;
        if (ContainsAny(title, "integrity", "plagiarism", "cheating", "ethic"))
            return SyllabusCategories.AcademicIntegrity;
        if (ContainsAny(title, "communication", "teams", "announcement"))
            return SyllabusCategories.CommunicationPolicy;
        if (ContainsAny(title, "phone", "device", "digital", "mobile"))
            return SyllabusCategories.DevicePolicy;
        if (ContainsAny(title, "make-up", "make up", "retake"))
            return SyllabusCategories.MakeUpPolicy;

        if (ContainsAny(text, "%", "midterm", "final exam", "quiz", "grading", "weight"))
            return SyllabusCategories.GradingPolicy;
        if (ContainsAny(text, "week 1", "week 2", "week 3", "course topic", "course calendar", "weekly"))
            return SyllabusCategories.WeeklySchedule;
        if (ContainsAny(text, "late submission", "reduced by", "not accepted after", "must submit", "deadline", "homework"))
            return SyllabusCategories.AssignmentPolicy;
        if (ContainsAny(text, "attendance", "expected to attend", "70% attendance"))
            return SyllabusCategories.AttendancePolicy;
        if (ContainsAny(text, "plagiarism", "disciplinary", "academic integrity", "cheating"))
            return SyllabusCategories.AcademicIntegrity;
        if (ContainsAny(text, "office", "e-mail", "email", "instructor"))
            return SyllabusCategories.InstructorInfo;

        return SyllabusCategories.Unknown;
    }

    private static bool ContainsAny(string source, params string[] keys) => keys.Any(source.Contains);
}
