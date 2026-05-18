# -*- coding: utf-8 -*-
from pathlib import Path
p = Path(__file__).parent / "Services" / "AiService.cs"
s = p.read_text(encoding="utf-8")

old_early = """        var syllabusDocAnswer = TryAnswerSyllabusDocumentMeta(course, session.Id, question);
        if (syllabusDocAnswer != null)
        {
            await LogExchangeAsync(session.Id, question, syllabusDocAnswer, Array.Empty<SyllabusChunk>(), ct);
            return syllabusDocAnswer;
        }

        if (!_openAi.IsConfigured)"""

new_early = """        var hasChunks = await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId, ct);
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

        if (!_openAi.IsConfigured)"""

if old_early not in s:
    raise SystemExit("old_early not found")
s = s.replace(old_early, new_early, 1)

old_dup = """        var hasChunks = await _db.SyllabusChunks.AnyAsync(c => c.CourseId == request.CourseId, ct);
        if (!hasChunks)
            await _ragIndex.ReindexCourseAsync(course.Id, course.SyllabusContent, ct);

        var chunks = await _db.SyllabusChunks.AsNoTracking()
            .Where(c => c.CourseId == request.CourseId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        if (chunks.Count == 0)
        {
            var deny = new ChatResponse
            {
                SessionId = session.Id,
                Answer = "Syllabus text could not be processed.",
                FromSyllabus = false,
                RetrievalMethod = "none",
                FallbackTriggered = true,
                IsOutOfScope = true
            };
            await LogExchangeAsync(session.Id, question, deny, Array.Empty<SyllabusChunk>(), ct);
            return deny;
        }

        var hint = _questionHint.Predict(question);"""

if old_dup not in s:
    raise SystemExit("dup block not found")
s = s.replace(old_dup, "        var hint = _questionHint.Predict(question);", 1)

# method signature
sig_old = "private static ChatResponse? TryAnswerSyllabusDocumentMeta(Course course, int sessionId, string question)"
sig_new = "private static ChatResponse? TryAnswerSyllabusDocumentMeta(Course course, int sessionId, string question, IReadOnlyList<SyllabusChunk> chunks)"
if sig_old not in s:
    raise SystemExit("sig not found")
s = s.replace(sig_old, sig_new, 1)

body_old = """        var extracted = TryExtractPreparedByBlock(text);
        if (string.IsNullOrWhiteSpace(extracted)) return null;"""
body_new = r"""        var extracted = TryExtractPreparedByBlock(text);
        if (string.IsNullOrWhiteSpace(extracted) && chunks.Count > 0)
        {
            var joined = string.Join("\n\n", chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Text));
            extracted = TryExtractPreparedByBlock(joined);
        }
        if (string.IsNullOrWhiteSpace(extracted)) return null;"""
if body_old not in s:
    raise SystemExit("body not found")
s = s.replace(body_old, body_new, 1)

with p.open("w", encoding="utf-8", newline="\n") as f:
    f.write(s)
print("ok")
