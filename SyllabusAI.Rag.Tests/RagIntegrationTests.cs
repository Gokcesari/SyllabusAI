using SyllabusAI.Services;
using Xunit;

namespace SyllabusAI.Rag.Tests;

public class RagIntegrationTests
{
    private const string SampleGradingTable = """
        Grading and Evaluation
        In this section, you can specify the measurement tools.
        Assignment Description Scoring Weight (%)
        Midterm Midterm exam 30
        Process Class participation 10
        Assignments Homework 10
        Presentations Presentation 10
        Final Exam Final 40
        """;

    [Fact]
    public void CategoryMapper_GradingTable_NotAssignmentPolicy()
    {
        var mapper = new SyllabusCategoryMapper();
        var cat = mapper.Map("Grading and Evaluation", SampleGradingTable);
        Assert.Equal(SyllabusCategories.GradingPolicy, cat);
    }

    private const string NoisyCalendarPlusGrading = """
        W8 CH. 26 Definition of Capacitance, Calculating Capacitance
        Exam dates: Midterm week 7, Final week 15.
        Grading and Evaluation
        Assignment Description Scoring Weight (%)
        Midterm Midterm exam 30
        Process Class participation 10
        Assignments Homework 10
        Presentations Presentation 10
        Final Exam Final 40
        """;

    [Fact]
    public void GradingTableAnswer_IgnoresCalendarWeekNumbers()
    {
        var rows = GradingTableAnswer.ParseWeightRows(NoisyCalendarPlusGrading);
        var mid = rows.FirstOrDefault(r => r.Component.Contains("Midterm", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mid);
        Assert.Equal(30, mid!.Weight);
        Assert.DoesNotContain(rows, r => r.Weight == 8 || r.Weight == 7);
        Assert.Equal(100, rows.Sum(r => r.Weight));
    }

    [Fact]
    public void GradingTableAnswer_ParsesMidterm30()
    {
        var rows = GradingTableAnswer.ParseWeightRows(SampleGradingTable);
        var mid = rows.FirstOrDefault(r => r.Component.Contains("Midterm", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(mid);
        Assert.Equal(30, mid!.Weight);
    }

    [Fact]
    public void GradingQuestion_TurkishMidtermHint()
    {
        var hint = new QuestionCategoryHintService().Predict("midterm ne kadar etkiliyor scorumu");
        Assert.Equal(SyllabusCategories.GradingPolicy, hint);
    }

    [Fact]
    public void AfterPreprocess_StillHasGradingLine()
    {
        var lines = SyllabusTextChunker.PreviewLines(NoisyCalendarPlusGrading);
        Assert.Contains(lines, l => l.StartsWith("Grading and Evaluation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Chunker_SplitsSectionsGradingRowsAndWeeks()
    {
        var mapper = new SyllabusCategoryMapper();
        var text = NoisyCalendarPlusGrading + "\n\nAttendance Policy\nStudents must attend at least 70% of sessions.";
        var chunks = SyllabusTextChunker.SplitWithSections(text, mapper);

        Assert.True(chunks.Count >= 5, $"chunk count={chunks.Count}; titles={string.Join(" | ", chunks.Select(c => c.OriginalSectionTitle))}");
        Assert.All(chunks, c => Assert.StartsWith("[Section:", c.Text));
        Assert.Contains(chunks, c => c.NormalizedCategory == SyllabusCategories.AttendancePolicy);
        Assert.Contains(chunks, c => c.OriginalSectionTitle.Contains("Midterm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chunks, c => c.OriginalSectionTitle.Contains("W8", StringComparison.OrdinalIgnoreCase)
                                   || c.Text.Contains("W8", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GradingTableAnswer_UsesOnlyGradingSectionText()
    {
        var rows = GradingTableAnswer.ParseWeightRows(
            "W8 CH. 26 Capacitance\nGrading and Evaluation\nMidterm Midterm exam 30\nFinal Exam Final 40\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(30, rows.First(r => r.Component == "Midterm").Weight);
    }

    [Fact]
    public void WeekScheduleAnswer_ReturnsWeek3Topic()
    {
        var mapper = new SyllabusCategoryMapper();
        var chunk = new SyllabusAI.Models.SyllabusChunk
        {
            Id = 9,
            CourseId = 25,
            ChunkIndex = 9,
            Text = "[Section: Course Calendar - W3]\nW3   ON CH-24:   Gauss`s Law Electric Flux, Gauss, s Law",
            OriginalSectionTitle = "Course Calendar  Week/Place Course Topic To Do Assignments  Deadline* - W3",
            NormalizedCategory = SyllabusCategories.WeeklySchedule
        };
        var course = new SyllabusAI.Models.Course { CourseCode = "PHY1002", Title = "PHYSICS 1002" };

        var en = WeekScheduleAnswer.TryAnswer("what is the topic of week 3", new[] { chunk }, 1, course);
        Assert.NotNull(en);
        Assert.Contains("Week 3", en!.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gauss", en.Answer, StringComparison.OrdinalIgnoreCase);

        var tr = WeekScheduleAnswer.TryAnswer("3. haftanın topici ne", new[] { chunk }, 1, course);
        Assert.NotNull(tr);
        Assert.Contains("3. hafta", tr!.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gauss", tr.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retriever_WeekQuestion_BoostsMatchingWeekChunk()
    {
        var w3 = new SyllabusAI.Models.SyllabusChunk
        {
            Id = 3,
            CourseId = 1,
            ChunkIndex = 3,
            Text = "[Section: Course Calendar - W3]\nW3 ON CH-24 Gauss Law",
            OriginalSectionTitle = "Course Calendar - W3",
            NormalizedCategory = SyllabusCategories.WeeklySchedule,
            EmbeddingJson = "[]"
        };
        var w11 = new SyllabusAI.Models.SyllabusChunk
        {
            Id = 11,
            CourseId = 1,
            ChunkIndex = 11,
            Text = "[Section: Course Calendar - W11]\nW11 ON CH-27 Current",
            OriginalSectionTitle = "Course Calendar - W11",
            NormalizedCategory = SyllabusCategories.WeeklySchedule,
            EmbeddingJson = "[]"
        };

        var retriever = new SyllabusRagRetriever();
        var result = retriever.Retrieve(new[] { w11, w3 }, "what is the topic of week 3", SyllabusCategories.WeeklySchedule, false, null);

        Assert.Equal(3, result.Ranked[0].Chunk.Id);
    }

    [Fact]
    public void WeekScheduleAnswer_ReturnsWeek4_FromNumericCalendarText()
    {
        const string calendar = """
            Course Calendar Week/Place Course Topic To Do Assignments
            1 Introduction to Company Presentation
            4 Navigation Database Systems and Applications Structure and functioning of aeronautical navigation databases. Integration of navigation data into avionic systems. Presentation
            5 Challenges and Future Trends in Aeronautical Navigation Data Current challenges in navigation data management. Presentation
            """;
        var course = new SyllabusAI.Models.Course { CourseCode = "COP4803", Title = "KEYVAN AVIATION" };
        var answer = WeekScheduleAnswer.TryAnswer(
            "what is the topic of week 4",
            Array.Empty<SyllabusAI.Models.SyllabusChunk>(),
            1,
            course,
            calendar);
        Assert.NotNull(answer);
        Assert.Contains("Navigation Database", answer!.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Chunker_SplitsNumericCourseCalendarWeeks()
    {
        const string text = """
            Course Calendar Week/Place Course Topic
            1 Introduction Presentation
            4 Navigation Database Systems and Applications Structure and functioning of databases Presentation
            5 Challenges and Future Trends Presentation
            Matters Needing Attention Students must attend.
            """;
        var mapper = new SyllabusCategoryMapper();
        var chunks = SyllabusTextChunker.SplitWithSections(text, mapper);
        Assert.Contains(chunks, c => c.Text.Contains("Navigation Database", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chunks, c => c.OriginalSectionTitle.Contains("Week 4", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Retriever_GradingQuestion_PicksGradingChunk()
    {
        var mapper = new SyllabusCategoryMapper();
        var chunk = new SyllabusAI.Models.SyllabusChunk
        {
            Id = 1,
            CourseId = 1,
            ChunkIndex = 0,
            Text = SampleGradingTable,
            OriginalSectionTitle = "Grading and Evaluation",
            NormalizedCategory = mapper.Map("Grading and Evaluation", SampleGradingTable)
        };
        var attendance = new SyllabusAI.Models.SyllabusChunk
        {
            Id = 2,
            CourseId = 1,
            ChunkIndex = 1,
            Text = "Attendance is mandatory. Deadline for homework is Friday.",
            OriginalSectionTitle = "Attendance",
            NormalizedCategory = SyllabusCategories.AttendancePolicy
        };

        var retriever = new SyllabusRagRetriever();
        var result = retriever.Retrieve(new[] { chunk, attendance }, "midterm ne kadar etkiliyor", SyllabusCategories.GradingPolicy, false, null);

        Assert.Equal("grading-lexical", result.Method);
        Assert.Contains(result.Ranked, r => r.Chunk.Id == 1);
        Assert.Equal(1, result.Ranked[0].Chunk.Id);
    }
}
