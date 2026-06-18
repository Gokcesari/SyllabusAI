using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class AiService : IAiService
{
    private const int TopK = 14;
    private const int MaxContextChars = 22000;
    private const int MaxHistoryCharsPerMessage = 500;
    private const double DefaultEmbeddingRelevanceThreshold = 0.12;
    private const double DefaultLexicalRelevanceThreshold = 0.5;
    private const string DefaultOpenAiUnavailableMessage =
        "The AI service is not configured. Please try again later or contact your instructor.";

    private static readonly string[] OutOfScopeResponses =
    {
        "This question does not appear to be directly related to the course syllabus. Please contact your instructor for the most accurate information.",
        "This topic is outside the syllabus scope. Please contact your instructor for details.",
        "I cannot answer this from the syllabus text. Please ask your instructor."
    };

    private readonly ApplicationDbContext _db;
    private readonly ISyllabusRagIndexService _ragIndex;
    private readonly IOpenAiSyllabusClient _openAi;
    private readonly IConfiguration _config;
    private readonly QuestionCategoryHintService _questionHint;
    private readonly SyllabusRagRetriever _retriever;

    public AiService(
        ApplicationDbContext db,
        ISyllabusRagIndexService ragIndex,
        IOpenAiSyllabusClient openAi,
        IConfiguration config,
        QuestionCategoryHintService questionHint,
        SyllabusRagRetriever retriever)
    {
        _db = db;
        _ragIndex = ragIndex;
        _openAi = openAi;
        _config = config;
        _questionHint = questionHint;
        _retriever = retriever;
    }

    public async Task<ChatResponse> AskAsync(int userId, ChatRequest request, CancellationToken ct = default)
    {
        var question = (request.Question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(question))
            return new ChatResponse { Answer = "Please enter a question.", IsOutOfScope = true, FallbackTriggered = true };

        var allowed = await _db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == request.CourseId, ct);
        if (!allowed)
            return new ChatResponse { Answer = "You do not have access to this course.", IsOutOfScope = true, FallbackTriggered = true };

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CourseId, ct);
        if (course == null)
            return new ChatResponse { Answer = "Course not found.", IsOutOfScope = true, FallbackTriggered = true };

        var syllabusText = await ResolveSyllabusTextAsync(course.Id, course.SyllabusContent, ct);
        var hasChunksAlready = await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (string.IsNullOrWhiteSpace(syllabusText) && !hasChunksAlready)
            return new ChatResponse { Answer = "No syllabus has been uploaded for this course yet.", IsOutOfScope = true, FallbackTriggered = true };

        var session = await EnsureSessionAsync(userId, request.CourseId, request.SessionId, ct);

        var courseIdentity = TryAnswerCourseIdentity(course, session.Id, question);
        if (courseIdentity != null)
        {
            await LogExchangeAsync(session.Id, question, courseIdentity, Array.Empty<SyllabusChunk>(), ct);
            return courseIdentity;
        }

        var hasChunks = hasChunksAlready;
        if (!hasChunks && !string.IsNullOrWhiteSpace(syllabusText))
            await _ragIndex.ReindexCourseAsync(course.Id, syllabusText, ct);
        else if (hasChunks && !string.IsNullOrWhiteSpace(syllabusText)
                 && !await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId && c.EmbeddingJson != null, ct))
            await _ragIndex.ReindexCourseAsync(course.Id, syllabusText, ct);
        else if (hasChunks && !string.IsNullOrWhiteSpace(syllabusText))
        {
            var maxChunkLen = await _db.SyllabusChunks.AsNoTracking()
                .Where(c => c.CourseId == request.CourseId)
                .MaxAsync(c => (int?)c.Text.Length, ct) ?? 0;
            var usesLegacyChunkFormat = await _db.SyllabusChunks.AsNoTracking()
                .AnyAsync(c => c.CourseId == request.CourseId
                               && !c.Text.StartsWith("[Section:"), ct);
            if (maxChunkLen > SyllabusTextChunker.MaxChunkChars + 200 || usesLegacyChunkFormat)
                await _ragIndex.ReindexCourseAsync(course.Id, syllabusText, ct);
        }

        var chunks = await _db.SyllabusChunks.AsNoTracking()
            .Where(c => c.CourseId == request.CourseId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(syllabusText)
            && WeekScheduleAnswer.ChunksMissingCalendarWeeks(chunks, syllabusText))
        {
            await _ragIndex.ReindexCourseAsync(course.Id, syllabusText, ct);
            chunks = await _db.SyllabusChunks.AsNoTracking()
                .Where(c => c.CourseId == request.CourseId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(ct);
        }

        if (chunks.Count == 0)
        {
            var denyNoChunks = new ChatResponse
            {
                SessionId = session.Id,
                Answer = "Syllabus text could not be processed.",
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
            await LogExchangeAsync(session.Id, question, denyNoChunks, Array.Empty<SyllabusChunk>(), ct);
            return denyNoChunks;
        }

        var syllabusDocAnswer = TryAnswerSyllabusDocumentMeta(course, session.Id, question, syllabusText, chunks);
        if (syllabusDocAnswer != null)
        {
            await LogExchangeAsync(session.Id, question, syllabusDocAnswer, Array.Empty<SyllabusChunk>(), ct);
            return syllabusDocAnswer;
        }

        var gradingDirect = GradingTableAnswer.TryAnswer(question, syllabusText, chunks, session.Id, course);
        if (gradingDirect != null)
        {
            await LogExchangeAsync(session.Id, question, gradingDirect, chunks.Where(c =>
                SyllabusCategoryMapper.LooksLikeGradingTable(c.Text) || c.NormalizedCategory == SyllabusCategories.GradingPolicy).Take(3).ToList(), ct);
            return gradingDirect;
        }

        var weekDirect = WeekScheduleAnswer.TryAnswer(question, chunks, session.Id, course, syllabusText);
        if (weekDirect != null)
        {
            var weekNum = WeekScheduleAnswer.TryExtractWeekNumber(question);
            await LogExchangeAsync(session.Id, question, weekDirect, SelectWeekSourceChunks(chunks, weekNum).Take(1).ToList(), ct);
            return weekDirect;
        }

        if (!_openAi.IsConfigured)
        {
            var unavailableMessage = _config["AiGuard:OpenAiUnavailableMessage"];
            var deny = new ChatResponse
            {
                SessionId = session.Id,
                Answer = string.IsNullOrWhiteSpace(unavailableMessage) ? DefaultOpenAiUnavailableMessage : unavailableMessage,
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
            await LogExchangeAsync(session.Id, question, deny, Array.Empty<SyllabusChunk>(), ct);
            return deny;
        }

        if (!await EvaluateQuestionScopeAsync(course, question, ct))
        {
            var deny = new ChatResponse
            {
                SessionId = session.Id,
                Answer = PickOutOfScopeResponse(question),
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
            await LogExchangeAsync(session.Id, question, deny, Array.Empty<SyllabusChunk>(), ct);
            return deny;
        }

        var hint = _questionHint.Predict(question);

        if (QuestionCategoryHintService.IsGradingQuestion(question)
            && !chunks.Any(c => SyllabusCategoryMapper.LooksLikeGradingTable(c.Text))
            && !string.IsNullOrWhiteSpace(syllabusText))
        {
            await _ragIndex.ReindexCourseAsync(course.Id, syllabusText, ct);
            chunks = await _db.SyllabusChunks.AsNoTracking()
                .Where(c => c.CourseId == request.CourseId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(ct);
            var retryGrading = GradingTableAnswer.TryAnswer(question, syllabusText, chunks, session.Id, course);
            if (retryGrading != null)
            {
                await LogExchangeAsync(session.Id, question, retryGrading, chunks.Take(3).ToList(), ct);
                return retryGrading;
            }
        }

        float[]? qVec = null;
        if (_openAi.IsConfigured && hint != SyllabusCategories.GradingPolicy)
            qVec = await _openAi.EmbedOneAsync(question, ct);

        var retrieval = _retriever.Retrieve(
            chunks,
            question,
            hint,
            useEmbeddings: hint != SyllabusCategories.GradingPolicy,
            queryEmbedding: qVec);

        var ranked = retrieval.Ranked.ToList();
        var method = retrieval.Method;
        var qTokens = Tokenize(question);

        var bestScore = ranked.Count > 0 ? ranked[0].Score : 0;
        var embeddingThreshold = _config.GetValue<double?>("AiGuard:EmbeddingRelevanceThreshold") ?? DefaultEmbeddingRelevanceThreshold;
        var lexicalThreshold = _config.GetValue<double?>("AiGuard:LexicalRelevanceThreshold") ?? DefaultLexicalRelevanceThreshold;
        var weakByMethod =
            (method == "embedding" && bestScore < embeddingThreshold)
            || (method == "lexical" && bestScore < lexicalThreshold);

        // Zayıf skor: yine de en iyi parçalarla cevap dene; yalnızca hiç eşleşme yoksa reddet.
        if (weakByMethod && hint == SyllabusCategories.Unknown && (ranked.Count == 0 || ranked.All(x => x.Score <= 0)))
        {
            var deny = new ChatResponse
            {
                SessionId = session.Id,
                Answer = "I could not find clear information on this in the syllabus.",
                FromSyllabus = false,
                RetrievalMethod = method,
                FallbackTriggered = true,
                IsOutOfScope = true
            };
            await LogExchangeAsync(session.Id, question, deny, ranked.Select(x => x.Chunk).ToList(), ct);
            return deny;
        }

        var selected = ranked.Select(x => x.Chunk).ToList();
        if (selected.Count == 0)
            selected = chunks.Take(TopK).ToList();

        var context = BuildBoundedContext(selected);
        var historyText = await BuildSessionHistoryAsync(session.Id, 6, ct);
        var replyLanguage = QuestionLanguage.IsEnglish(question) ? "English" : "Turkish";
        var system =
            "You are a helpful university teaching assistant. Answer only from the syllabus excerpts provided. " +
            $"The current question is in {replyLanguage}. You MUST reply entirely in {replyLanguage}, even if earlier messages in this session used another language. " +
            "Give a complete, thorough answer: include all relevant numbers, dates, percentages, weights, and rules from the excerpts. " +
            "For grade or exam weight questions, list every assessment component and its percentage if present in the excerpts. " +
            "Use short paragraphs or bullet lists when they improve clarity. Do not stop mid-thought or mid-sentence. " +
            "Only say information is missing if no excerpt mentions grades, exams, or percentages. " +
            "End with one short line listing which syllabus section(s) you used.";
        var userMsg = string.IsNullOrWhiteSpace(historyText)
            ? $"Course: {course.CourseCode} - {course.Title}\n\nSyllabus context:\n{context}\n\nQuestion:\n{question}"
            : $"Course: {course.CourseCode} - {course.Title}\n\nEarlier in this session (for continuity; policies must still match the syllabus context below):\n{historyText}\n\nSyllabus context:\n{context}\n\nCurrent question:\n{question}";
        var answer = await _openAi.ChatAsync(system, userMsg, ct, temperature: 0.35, maxTokens: 4096);

        if (string.IsNullOrWhiteSpace(answer))
            answer = "I could not find clear information on this in the syllabus.";

        var uiSources = SelectUiSources(selected, qTokens);
        var response = new ChatResponse
        {
            SessionId = session.Id,
            Answer = answer.Trim(),
            FromSyllabus = true,
            RetrievalMethod = method,
            SourceSnippets = uiSources.Select(x => TruncateForUi(x.Text, 180)).ToList(),
            SourceSections = uiSources.Select(GetUiSectionName).Distinct().ToList(),
            FallbackTriggered = false,
            IsOutOfScope = false
        };

        await LogExchangeAsync(session.Id, question, response, selected, ct);
        return response;
    }

    public async Task<ChatResponse> AskInstructorAsync(int instructorUserId, ChatRequest request, CancellationToken ct = default)
    {
        var prompt = (request.Question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new ChatResponse
            {
                SessionId = 0,
                Answer = "Please enter a question.",
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = false
            };
        }

        var ownsAnyCourse = await _db.Courses.AnyAsync(c => c.InstructorId == instructorUserId, ct);
        if (!ownsAnyCourse)
        {
            return new ChatResponse
            {
                SessionId = 0,
                Answer = "You do not have access to instructor AI yet.",
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
        }

        if (!_openAi.IsConfigured)
        {
            var unavailableMessage = _config["AiGuard:OpenAiUnavailableMessage"];
            return new ChatResponse
            {
                SessionId = 0,
                Answer = string.IsNullOrWhiteSpace(unavailableMessage) ? DefaultOpenAiUnavailableMessage : unavailableMessage,
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
        }

        var system =
            "You are an assistant for university instructors, in the same helpful, natural style as ChatGPT. " +
            "Give full, well-organized answers in plain language (paragraphs, bullets, or short lists when useful). " +
            "Do not reveal secrets, private student data, or internal system details.";
        var historyText = FormatExternalHistory(request.History, 20);
        var userBody = string.IsNullOrWhiteSpace(historyText)
            ? prompt
            : $"Earlier in this chat:\n{historyText}\n\nCurrent message:\n{prompt}";
        var answer = await _openAi.ChatAsync(system, userBody, ct);
        if (string.IsNullOrWhiteSpace(answer))
            answer = "I could not generate an answer right now. Please try again.";

        return new ChatResponse
        {
            SessionId = 0,
            Answer = answer.Trim(),
            FromSyllabus = false,
            RetrievalMethod = "none",
            FallbackTriggered = false,
            IsOutOfScope = false
        };
    }

    public async Task<ChatCourseAnalyticsDto?> GetCourseAnalyticsAsync(int instructorUserId, int courseId, CancellationToken ct = default)
    {
        var owns = await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == instructorUserId, ct);
        if (!owns) return null;

        var assistantMessages = await _db.ChatMessages
            .Where(m => m.Role == "assistant" && m.ChatSession.CourseId == courseId)
            .ToListAsync(ct);

        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var msg in assistantMessages)
        {
            if (string.IsNullOrWhiteSpace(msg.RetrievedCategoriesJson)) continue;
            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(msg.RetrievedCategoriesJson) ?? new List<string>();
                foreach (var i in items.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    categoryCounts.TryGetValue(i, out var c);
                    categoryCounts[i] = c + 1;
                }
            }
            catch
            {
                // ignore malformed history row
            }
        }

        return new ChatCourseAnalyticsDto
        {
            CourseId = courseId,
            TotalQuestions = assistantMessages.Count,
            OutOfScopeQuestions = assistantMessages.Count(x => x.IsOutOfScope),
            CategoryCounts = categoryCounts
        };
    }

    public async Task<RagEvalSummaryDto> EvaluateAsync(int instructorUserId, IReadOnlyList<RagEvalCaseDto> cases, CancellationToken ct = default)
    {
        var result = new RagEvalSummaryDto();
        if (cases.Count == 0) return result;

        foreach (var item in cases)
        {
            var owns = await _db.Courses.AnyAsync(c => c.Id == item.CourseId && c.InstructorId == instructorUserId, ct);
            if (!owns) continue;

            var studentId = await _db.Enrollments
                .Where(e => e.CourseId == item.CourseId)
                .Select(e => (int?)e.UserId)
                .FirstOrDefaultAsync(ct);
            if (studentId == null) continue;

            var response = await AskAsync(studentId.Value, new ChatRequest { CourseId = item.CourseId, Question = item.Question }, ct);
            var pass = !string.IsNullOrWhiteSpace(item.ExpectedKeyword)
                && response.Answer.Contains(item.ExpectedKeyword, StringComparison.OrdinalIgnoreCase);

            result.Results.Add(new RagEvalResultItemDto
            {
                Question = item.Question,
                ExpectedKeyword = item.ExpectedKeyword,
                Passed = pass,
                Answer = response.Answer
            });
        }

        result.Total = result.Results.Count;
        result.Passed = result.Results.Count(x => x.Passed);
        return result;
    }

    private async Task<ChatSession> EnsureSessionAsync(int studentId, int courseId, int? sessionId, CancellationToken ct)
    {
        if (sessionId is > 0)
        {
            var existing = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentUserId == studentId && s.CourseId == courseId, ct);
            if (existing != null) return existing;
        }

        var session = new ChatSession { StudentUserId = studentId, CourseId = courseId, CreatedAtUtc = DateTime.UtcNow };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    private static ChatResponse? TryAnswerCourseIdentity(Course course, int sessionId, string question)
    {
        var q = question.Trim().ToLowerInvariant();
        var asksName = ContainsAny(q,
            "what is the name of this course", "course name", "name of this course",
            "dersin adi", "dersin adı", "dersin ismi", "course title");
        var asksCode = ContainsAny(q,
            "course code", "what is the code", "ders kodu", "dersin kodu");

        if (!asksName && !asksCode) return null;

        var answer = asksName && asksCode
            ? $"Course: {course.Title} (code: {course.CourseCode})."
            : asksName
                ? $"Course name: {course.Title} (code: {course.CourseCode})."
                : $"Course code: {course.CourseCode} ({course.Title}).";

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = answer,
            FromSyllabus = true,
            IsOutOfScope = false,
            RetrievalMethod = "course-metadata",
            SourceSnippets = new List<string> { $"{course.CourseCode} - {course.Title}" },
            SourceSections = new List<string> { "Course metadata" },
            FallbackTriggered = false
        };
    }

    /// <summary>
    /// Answers cover/footer style questions (who prepared, dates) from raw syllabus text so typos and short queries still work.
    /// </summary>
    private async Task<string> ResolveSyllabusTextAsync(int courseId, string? courseSyllabusContent, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(courseSyllabusContent))
            return courseSyllabusContent.Trim();

        var fromUpload = await _db.SyllabusPdfUploads.AsNoTracking()
            .Where(u => u.CourseId == courseId && u.IsActive && u.ExtractedText != null && u.ExtractedText != "")
            .OrderByDescending(u => u.UploadedAtUtc)
            .Select(u => u.ExtractedText)
            .FirstOrDefaultAsync(ct);

        return fromUpload?.Trim() ?? string.Empty;
    }

    private static ChatResponse? TryAnswerSyllabusDocumentMeta(Course course, int sessionId, string question, string syllabusText, IReadOnlyList<SyllabusChunk> chunks)
    {
        var q = question.Trim().ToLowerInvariant();
        if (!AsksSyllabusDocumentMeta(q)) return null;

        var extracted = TryExtractPreparedByBlock(syllabusText);
        if (string.IsNullOrWhiteSpace(extracted))
            extracted = TryExtractPreparedByFromChunks(chunks);
        if (string.IsNullOrWhiteSpace(extracted)) return null;

        extracted = extracted.Trim();
        if (extracted.Length > 900) extracted = extracted[..900] + "...";

        var answerText = FormatPreparedByAnswer(question, extracted);

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = answerText,
            FromSyllabus = true,
            IsOutOfScope = false,
            RetrievalMethod = "syllabus-document-extract",
            SourceSnippets = new List<string> { TruncateForUi(extracted.Replace('\n', ' '), 180) },
            SourceSections = new List<string> { "Document information" },
            FallbackTriggered = false
        };
    }

    private static bool AsksSyllabusDocumentMeta(string q)
    {
        if (ContainsAny(q,
                "prepared by", "who prepared", "who wrote", "who made", "who authored", "who created",
                "date of preparation", "name surname",
                "kim hazırladı", "kim hazirladi", "kim hazırlamış", "kim hazirlamis", "kim hazırladi",
                "hazırlayan", "hazirlayan", "hazırlamış", "hazirlamis", "hazırlayan kim",
                "belgeyi kim", "belge kim", "kim yazdı", "kim yazdi", "kim yaptı", "kim yapti",
                "revision date", "version of the syllabus", "version of syllabus", "document date"))
            return true;
        if (ContainsAny(q, "kim", "who") && ContainsAny(q, "hazır", "hazir", "belge", "döküman", "dokuman", "syllabus", "müfredat", "mufredat"))
            return true;
        if (q.Contains("syllabus") && ContainsAny(q, "who", "whom", "whose", "kim"))
            return true;
        if ((q.Contains("prap") || q.Contains("prep")) && q.Contains("syllabus"))
            return true;
        return false;
    }

    private static bool IsTurkishQuestion(string question)
    {
        var q = (question ?? string.Empty).ToLowerInvariant();
        return ContainsAny(q, "kim", "ne ", "nasıl", "nasil", "kaç", "kac", "belge", "müfredat", "mufredat", "ders", "sınav", "sinav", "hazır", "hazir");
    }

    private static string FormatPreparedByAnswer(string question, string extracted)
    {
        if (!IsTurkishQuestion(question))
            return "From the syllabus document:\n\n" + extracted;

        var names = ExtractPersonNamesFromPreparedBlock(extracted);
        if (names.Count > 0)
            return "Müfredat belgesine göre hazırlayan: " + string.Join(", ", names) + ".\n\n" + extracted;

        return "Müfredat belgesine göre:\n\n" + extracted;
    }

    private static List<string> ExtractPersonNamesFromPreparedBlock(string text)
    {
        var names = new List<string>();
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var t = line.Trim();
            if (t.Length < 4 || t.Length > 80) continue;
            if (ContainsAny(t.ToLowerInvariant(), "prepared", "date", "preparation", "syllabus", "name", "surname", "hazır", "hazir"))
                continue;
            if (Regex.IsMatch(t, @"^\d{1,2}[./]\d{1,2}[./]\d{2,4}$")) continue;
            if (Regex.IsMatch(t, @"^[A-ZÇĞİÖŞÜ][A-Za-zÇĞİÖŞÜçğıöşü]+(\s+[A-ZÇĞİÖŞÜ][A-Za-zÇĞİÖŞÜçğıöşü]+)+$"))
                names.Add(t);
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }

    private static string? TryExtractPreparedByFromChunks(IReadOnlyList<SyllabusChunk> chunks)
    {
        if (chunks.Count == 0) return null;

        foreach (var c in chunks.OrderBy(x => x.ChunkIndex))
        {
            var hit = TryExtractPreparedByBlock(c.Text);
            if (!string.IsNullOrWhiteSpace(hit)) return hit;
        }

        var preparedChunk = chunks.FirstOrDefault(c =>
            c.Text.Contains("Prepared by", StringComparison.OrdinalIgnoreCase)
            || c.Text.Contains("Date of Preparation", StringComparison.OrdinalIgnoreCase));
        if (preparedChunk == null) return null;

        var text = preparedChunk.Text;
        var idx = text.IndexOf("Prepared by", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            idx = text.IndexOf("Date of Preparation", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var slice = text[idx..Math.Min(text.Length, idx + 600)].Trim();
        return slice.Length > 8 ? slice : null;
    }

    private static string? TryExtractPreparedByBlock(string text)
    {
        var patterns = new[]
        {
            @"(?is)Prepared\s+by\s+Name\s+Surname\s+and\s+Date\s+of\s+Preparation\s*:[\s\S]{0,500}",
            @"(?is)Prepared\s+by\s*[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,12}",
            @"(?is)Prepared\s+by\s*:?\s*[\s\S]{0,900}",
            @"(?is)Date\s+of\s+Preparation\s*:[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,10}",
            @"(?is)Haz[iı]rlayan\s*:[^\n\r]*[\r\n]+(?:[^\n\r]+[\r\n]+){0,10}",
        };
        foreach (var pat in patterns)
        {
            var m = Regex.Match(text, pat);
            if (m.Success && m.Value.Trim().Length > 8)
                return m.Value.Trim();
        }

        foreach (var key in new[] { "Prepared by", "Date of Preparation", "Hazırlayan", "Hazirlayan" })
        {
            var idx = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var end = Math.Min(text.Length, idx + 900);
            var slice = text[idx..end];
            var doubleNl = slice.IndexOf("\n\n", StringComparison.Ordinal);
            if (doubleNl is > 40 and < 700) slice = slice[..doubleNl];
            return slice.Trim();
        }

        return null;
    }

    private static List<SyllabusChunk> SelectWeekSourceChunks(IReadOnlyList<SyllabusChunk> chunks, int? weekNum)
    {
        if (weekNum is not > 0) return chunks.Take(1).ToList();
        var n = weekNum.Value;
        var wLabel = $"W{n}";
        var weekLabel = $"Week {n}";
        var hits = chunks.Where(c =>
            c.OriginalSectionTitle?.Contains(wLabel, StringComparison.OrdinalIgnoreCase) == true
            || c.OriginalSectionTitle?.Contains(weekLabel, StringComparison.OrdinalIgnoreCase) == true
            || Regex.IsMatch(c.Text, $@"(?im)(?:^|\n)\s*{n}\s+[A-ZÇĞİÖŞÜ]")).ToList();
        return hits.Count > 0 ? hits : chunks.Take(1).ToList();
    }

    private static List<SyllabusChunk> SelectUiSources(List<SyllabusChunk> selected, HashSet<string> qTokens)
    {
        return selected.Take(2).ToList();
    }

    private static string GetUiSectionName(SyllabusChunk chunk)
    {
        var section = (chunk.OriginalSectionTitle ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("General", StringComparison.OrdinalIgnoreCase))
            return section;

        return chunk.NormalizedCategory switch
        {
            var x when x == SyllabusCategories.AssignmentPolicy => "Assignments and deadlines",
            var x when x == SyllabusCategories.GradingPolicy => "Grading and evaluation",
            var x when x == SyllabusCategories.AttendancePolicy => "Attendance",
            var x when x == SyllabusCategories.WeeklySchedule => "Weekly schedule",
            var x when x == SyllabusCategories.AcademicIntegrity => "Academic integrity",
            var x when x == SyllabusCategories.InstructorInfo => "Instructor information",
            _ => "General"
        };
    }

    private async Task LogExchangeAsync(int sessionId, string question, ChatResponse response, IReadOnlyList<SyllabusChunk> usedChunks, CancellationToken ct)
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "user",
            Content = question,
            CreatedAtUtc = DateTime.UtcNow
        });

        _db.ChatMessages.Add(new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = response.Answer,
            IsOutOfScope = response.IsOutOfScope,
            RetrievedChunkIdsJson = JsonSerializer.Serialize(usedChunks.Select(x => x.Id).ToList()),
            RetrievedCategoriesJson = JsonSerializer.Serialize(usedChunks.Select(x => x.NormalizedCategory).Distinct().ToList()),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> BuildSessionHistoryAsync(int sessionId, int maxTurns, CancellationToken ct)
    {
        var recent = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(Math.Max(2, maxTurns * 2))
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        if (recent.Count == 0) return string.Empty;

        return string.Join(
            "\n",
            recent
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .Select(x =>
                {
                    var body = x.Content.Trim();
                    if (body.Length > MaxHistoryCharsPerMessage)
                        body = body[..MaxHistoryCharsPerMessage] + "…";
                    var role = x.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User";
                    return $"{role}: {body}";
                }));
    }

    private static string BuildBoundedContext(IReadOnlyList<SyllabusChunk> selected)
    {
        var parts = new List<string>();
        var total = 0;
        foreach (var c in selected)
        {
            var block = $"[Section: {c.OriginalSectionTitle ?? "Untitled"}]\n{c.Text}";
            if (total + block.Length > MaxContextChars && parts.Count > 0)
                break;
            if (block.Length > MaxContextChars)
                block = block[..MaxContextChars] + "\n…";
            parts.Add(block);
            total += block.Length;
        }
        return string.Join("\n\n---\n\n", parts);
    }

    private static string FormatExternalHistory(IReadOnlyList<ChatTurnDto>? history, int maxItems)
    {
        if (history is not { Count: > 0 }) return string.Empty;
        var lines = history
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .TakeLast(Math.Max(1, maxItems))
            .Select(x =>
            {
                var role = x.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User";
                return $"{role}: {x.Content.Trim()}";
            })
            .ToList();
        return string.Join("\n", lines);
    }

    private async Task<bool> EvaluateQuestionScopeAsync(Course course, string question, CancellationToken ct)
    {
        question = (question ?? string.Empty).Trim();
        var q = question.ToLowerInvariant();
        if (AsksSyllabusDocumentMeta(q))
            return true;

        // Kategori ipucu (devamsızlık, not, takvim, vb.) zaten ders + müfredat sorusudur; LLM kapısını atla.
        if (_questionHint.Predict(question) != SyllabusCategories.Unknown)
            return true;

        if (HeuristicSyllabusThemedQuestion(question))
            return true;

        var system =
            "Reply with exactly one word: ALLOW or DENY. " +
            "ALLOW if the question is something a student could reasonably expect answered from a course syllabus (policies, schedule, grades, assignments, attendance/absences, exams, contact info, materials, learning outcomes, prerequisites). " +
            "ALLOW even if phrasing is imperfect or the question is in another language, as long as it is about this course. " +
            "DENY only for clearly off-topic questions, non-course chit-chat, requests to reveal secrets, cheating, or harmful content.";
        var user = $"Course: {course.CourseCode} - {course.Title}\nQuestion: {question}";
        var raw = await _openAi.ChatAsync(system, user, ct, 0.2, 120);
        return ParseGateAllows(raw);
    }

    private static bool HeuristicSyllabusThemedQuestion(string question)
    {
        var q = (question ?? string.Empty).ToLowerInvariant();
        return ContainsAny(
            q,
            "syllabus",
            "müfredat",
            "ects",
            "credit",
            "kredi",
            "prerequisite",
            "textbook",
            "reading list",
            "bibliograph",
            "learning outcome",
            "objective",
            "enrollment",
            "withdraw",
            "drop",
            "make-up",
            "makeup",
            "office hour",
            "final exam",
            "midterm",
            "quiz",
            "late policy",
            "tardy",
            "instructor",
            "ta ",
            "teaching assistant",
            "ders notu",
            "final not",
            "vize",
            "bütünleme",
            "butunleme",
            "sınav",
            "sinav",
            "puan",
            "ağırlık",
            "agirlik",
            "etkiliyor",
            "ne kadar",
            "hocam",
            "öğrenci",
            "ogrenci",
            "belge",
            "belgeyi",
            "hazırl",
            "hazir",
            "kim ");
    }

    private static bool ParseGateAllows(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return true;
        var t = response.Trim().ToUpperInvariant();
        var hasAllow = Regex.IsMatch(t, @"\bALLOW\b");
        var hasDeny = Regex.IsMatch(t, @"\bDENY\b");
        if (hasAllow && !hasDeny) return true;
        if (hasDeny && !hasAllow) return false;
        return true;
    }

    private static string PickOutOfScopeResponse(string question)
    {
        var index = Math.Abs(question.GetHashCode()) % OutOfScopeResponses.Length;
        return OutOfScopeResponses[index];
    }

    private static List<SyllabusChunk> BuildRetrievalPool(List<SyllabusChunk> chunks, string hint, string question)
    {
        if (hint == SyllabusCategories.Unknown)
            return chunks;

        var category = chunks.Where(c => c.NormalizedCategory == hint).ToList();
        var signal = chunks.Where(c => ChunkMatchesHintSignals(c, hint)).ToList();
        var merged = category.Concat(signal).GroupBy(c => c.Id).Select(g => g.First()).ToList();
        return merged.Count > 0 ? merged : chunks;
    }

    private static bool ChunkMatchesHintSignals(SyllabusChunk chunk, string hint)
    {
        var t = chunk.Text.ToLowerInvariant();
        return hint switch
        {
            var h when h == SyllabusCategories.GradingPolicy => ContainsGradingSignals(t),
            var h when h == SyllabusCategories.AttendancePolicy => ContainsAny(t, "attendance", "devam", "yoklama", "absence"),
            var h when h == SyllabusCategories.AssignmentPolicy => ContainsAny(t, "assignment", "homework", "deadline", "ödev", "odev", "teslim"),
            _ => false
        };
    }

    private static bool ContainsGradingSignals(string textLower) =>
        ContainsAny(textLower, "%", "midterm", "final", "quiz", "weight", "grading", "assessment", "evaluation",
            "ara sınav", "ara sinav", "vize", "bütünleme", "butunleme", "değerlendirme", "degerlendirme",
            "yüzde", "yuzde", "not", "puan", "ağırlık", "agirlik");

    private static List<(SyllabusChunk Chunk, double Score)> MergeGradingBoost(
        List<(SyllabusChunk Chunk, double Score)> ranked,
        List<SyllabusChunk> pool,
        HashSet<string> qTokens,
        int takeK)
    {
        var gradingHits = pool
            .Where(c => ContainsGradingSignals(c.Text.ToLowerInvariant()))
            .Select(c => (Chunk: c, Score: LexicalScore(qTokens, c.Text.ToLowerInvariant()) + 2.0))
            .OrderByDescending(x => x.Score);

        var merged = ranked
            .Concat(gradingHits)
            .GroupBy(x => x.Chunk.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .Take(takeK)
            .ToList();

        return merged.Count > 0 ? merged : ranked;
    }

    private static List<(SyllabusChunk Chunk, double Score)> RankLexical(List<SyllabusChunk> chunks, HashSet<string> qTokens, int takeK = TopK)
    {
        return chunks
            .Select(c => (c, LexicalScore(qTokens, c.Text.ToLowerInvariant())))
            .OrderByDescending(x => x.Item2)
            .Take(takeK)
            .ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var parts = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9çğıöşü]+", RegexOptions.CultureInvariant);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in parts)
        {
            if (p.Length >= 2) set.Add(p);
        }
        return set;
    }

    private static bool ContainsAny(string source, params string[] keys) => keys.Any(source.Contains);

    private static double LexicalScore(HashSet<string> qTokens, string chunkLower)
    {
        double score = 0;
        foreach (var t in qTokens)
        {
            if (chunkLower.Contains(t, StringComparison.Ordinal)) score += 1;
        }
        return score;
    }

    private static float[] ParseEmbedding(string json) => JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();

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

    private static string TruncateForUi(string text, int max)
    {
        var t = text.Replace('\n', ' ').Trim();
        return t.Length <= max ? t : t[..max] + "...";
    }
}
