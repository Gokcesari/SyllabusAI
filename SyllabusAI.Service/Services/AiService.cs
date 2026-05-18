using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class AiService : IAiService
{
    private const int TopK = 8;
    private const double DefaultEmbeddingRelevanceThreshold = 0.18;
    private const double DefaultLexicalRelevanceThreshold = 1.0;
    private const string DefaultOpenAiUnavailableMessage =
        "The AI service is not configured. Please try again later or contact your instructor.";

    private static readonly string[] OutOfScopeResponses =
    {
        "This question does not seem directly related to the course syllabus. Please contact your instructor.",
        "This topic is outside the syllabus scope. For the most accurate information, ask your instructor.",
        "I cannot produce a syllabus-based answer to this question. Please contact your instructor."
    };

    private readonly ApplicationDbContext _db;
    private readonly ISyllabusRagIndexService _ragIndex;
    private readonly IOpenAiSyllabusClient _openAi;
    private readonly IConfiguration _config;
    private readonly QuestionCategoryHintService _questionHint;

    public AiService(
        ApplicationDbContext db,
        ISyllabusRagIndexService ragIndex,
        IOpenAiSyllabusClient openAi,
        IConfiguration config,
        QuestionCategoryHintService questionHint)
    {
        _db = db;
        _ragIndex = ragIndex;
        _openAi = openAi;
        _config = config;
        _questionHint = questionHint;
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
        if (course == null || string.IsNullOrWhiteSpace(course.SyllabusContent))
            return new ChatResponse { Answer = "No syllabus has been added for this course yet.", IsOutOfScope = true, FallbackTriggered = true };

        var session = await EnsureSessionAsync(userId, request.CourseId, request.SessionId, ct);

        var courseIdentity = TryAnswerCourseIdentity(course, session.Id, question);
        if (courseIdentity != null)
        {
            await LogExchangeAsync(session.Id, question, courseIdentity, Array.Empty<SyllabusChunk>(), ct);
            return courseIdentity;
        }

        var hasChunks = await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (!hasChunks)
            await _ragIndex.ReindexCourseAsync(course.Id, course.SyllabusContent, ct);

        var chunks = await _db.SyllabusChunks.AsNoTracking()
            .Where(c => c.CourseId == request.CourseId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

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

        var syllabusDocAnswer = TryAnswerSyllabusDocumentMeta(course, session.Id, question, chunks);
        if (syllabusDocAnswer != null)
        {
            await LogExchangeAsync(session.Id, question, syllabusDocAnswer, Array.Empty<SyllabusChunk>(), ct);
            return syllabusDocAnswer;
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
        var narrowed = hint == SyllabusCategories.Unknown
            ? chunks
            : chunks.Where(c => c.NormalizedCategory == hint).ToList();
        if (narrowed.Count == 0) narrowed = chunks;

        var qTokens = Tokenize(question);
        var allHaveEmb = narrowed.All(c => !string.IsNullOrWhiteSpace(c.EmbeddingJson));
        var method = "lexical";

        List<(SyllabusChunk Chunk, double Score)> ranked;
        if (_openAi.IsConfigured && allHaveEmb)
        {
            var qVec = await _openAi.EmbedOneAsync(question, ct);
            if (qVec is { Length: > 0 })
            {
                ranked = narrowed
                    .Select(c => (c, CosineSimilarity(qVec, ParseEmbedding(c.EmbeddingJson!))))
                    .OrderByDescending(x => x.Item2)
                    .Take(TopK)
                    .ToList();
                method = "embedding";
            }
            else
            {
                ranked = RankLexical(narrowed, qTokens);
            }
        }
        else
        {
            ranked = RankLexical(narrowed, qTokens);
        }

        var bestScore = ranked.Count > 0 ? ranked[0].Score : 0;
        var embeddingThreshold = _config.GetValue<double?>("AiGuard:EmbeddingRelevanceThreshold") ?? DefaultEmbeddingRelevanceThreshold;
        var lexicalThreshold = _config.GetValue<double?>("AiGuard:LexicalRelevanceThreshold") ?? DefaultLexicalRelevanceThreshold;
        var weakByMethod =
            (method == "embedding" && bestScore < embeddingThreshold)
            || (method == "lexical" && bestScore < lexicalThreshold);

        // Kategori ipucu varsa (notlandırma, sınav, vb.) soru müfredat kapsamındadır; çok dilli soruda zayıf skorla reddetme.
        if (weakByMethod && hint == SyllabusCategories.Unknown)
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
        var context = string.Join("\n\n---\n\n", selected.Select(c => $"[Section: {c.OriginalSectionTitle ?? "Untitled"}]\n{c.Text}"));
        var historyText = await BuildSessionHistoryAsync(session.Id, 8, ct);
        var system =
            "You are a helpful, articulate teaching assistant in the same general style as ChatGPT. " +
            "Ground every claim in the provided syllabus context only. The excerpts may come from any part of the document (cover page, footer, tables, policies); use all relevant snippets. " +
            "Give complete answers: use short paragraphs, bullets, or numbered steps when they improve clarity. " +
            "If the syllabus does not support an answer, say you could not find that in the syllabus. " +
            "Close with a brief note on which section(s) the answer is based on.";
        var userMsg = string.IsNullOrWhiteSpace(historyText)
            ? $"Course: {course.CourseCode} - {course.Title}\n\nSyllabus context:\n{context}\n\nQuestion:\n{question}"
            : $"Course: {course.CourseCode} - {course.Title}\n\nEarlier in this session (for continuity; policies must still match the syllabus context below):\n{historyText}\n\nSyllabus context:\n{context}\n\nCurrent question:\n{question}";
        var answer = await _openAi.ChatAsync(system, userMsg, ct);

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
    private static ChatResponse? TryAnswerSyllabusDocumentMeta(Course course, int sessionId, string question, IReadOnlyList<SyllabusChunk> chunks)
    {
        var q = question.Trim().ToLowerInvariant();
        if (!AsksSyllabusDocumentMeta(q)) return null;
        var text = course.SyllabusContent;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var extracted = TryExtractPreparedByBlock(text);
        if (string.IsNullOrWhiteSpace(extracted) && chunks.Count > 0)
        {
            var joined = string.Join("\n\n", chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Text));
            extracted = TryExtractPreparedByBlock(joined);
        }
        if (string.IsNullOrWhiteSpace(extracted)) return null;

        extracted = extracted.Trim();
        if (extracted.Length > 900) extracted = extracted[..900] + "...";

        return new ChatResponse
        {
            SessionId = sessionId,
            Answer = "From the syllabus document:\n\n" + extracted,
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
                "kim hazırladı", "kim hazirladi", "hazırlayan", "hazirlayan",
                "revision date", "version of the syllabus", "version of syllabus", "document date"))
            return true;
        if (q.Contains("syllabus") && ContainsAny(q, "who", "whom", "whose", "kim"))
            return true;
        if ((q.Contains("prap") || q.Contains("prep")) && q.Contains("syllabus"))
            return true;
        return false;
    }

    private static string? TryExtractPreparedByBlock(string text)
    {
        var patterns = new[]
        {
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

    private static List<SyllabusChunk> SelectUiSources(List<SyllabusChunk> selected, HashSet<string> qTokens)
    {
        return selected
            .Select(c => new { Chunk = c, Score = LexicalScore(qTokens, c.Text.ToLowerInvariant()) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.ChunkIndex)
            .Take(2)
            .Select(x => x.Chunk)
            .ToList();
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
                .Select(x => $"{(x.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User")}: {x.Content.Trim()}"));
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
            "ogrenci");
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
