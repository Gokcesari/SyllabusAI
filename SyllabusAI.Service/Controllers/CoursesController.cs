using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IWeeklyFeedbackService _weeklyFeedbackService;
    private readonly IConfiguration _config;

    public CoursesController(ICourseService courseService, IWeeklyFeedbackService weeklyFeedbackService, IConfiguration config)
    {
        _courseService = courseService;
        _weeklyFeedbackService = weeklyFeedbackService;
        _config = config;
    }

    private int? UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpPost]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var course = await _courseService.CreateCourseAsync(UserId.Value, request, ct);
        if (course == null) return BadRequest(new { message = "This course code is already registered under your account." });
        return Ok(course);
    }

    [HttpGet("instructor")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetInstructorCourses(CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetInstructorCoursesAsync(UserId.Value, ct);
        return Ok(list);
    }

    [HttpDelete("instructor/{courseId:int}")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> DeleteInstructorCourse(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _courseService.DeleteCourseAsync(UserId.Value, courseId, ct);
        if (!ok) return NotFound(new { message = err ?? "Course not found." });
        return Ok(new { message = "Course deleted." });
    }

    [HttpPost("enroll")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> EnrollByCode([FromBody] EnrollByCodeRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var result = await _courseService.EnrollByCourseCodeAsync(UserId.Value, request.CourseCode, ct);
        if (result == EnrollResult.CourseNotFound)
            return NotFound(new { message = "Course code not found." });
        if (result == EnrollResult.AlreadyEnrolled)
            return Ok(new { message = "You are already enrolled in this course.", enrolled = true });
        return Ok(new { message = "Course added.", enrolled = true });
    }

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetMyEnrolledCoursesAsync(UserId.Value, ct);
        return Ok(list);
    }

    [HttpGet("feedback-questions")]
    [Authorize(Roles = "Student,Instructor")]
    public async Task<IActionResult> GetFeedbackQuestions(CancellationToken ct)
    {
        var list = await _courseService.GetFeedbackQuestionsAsync(ct);
        return Ok(list);
    }

    [HttpGet("feedback-questions-weekly")]
    [Authorize(Roles = "Student,Instructor")]
    public async Task<IActionResult> GetWeeklyFeedbackQuestions(CancellationToken ct)
    {
        var list = await _weeklyFeedbackService.GetWeeklyFeedbackQuestionsAsync(ct);
        return Ok(list);
    }

    [HttpDelete("my/{courseId:int}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Unenroll(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var ok = await _courseService.UnenrollAsync(UserId.Value, courseId, ct);
        if (!ok) return NotFound(new { message = "This course is not in your list." });
        return Ok(new { message = "Course removed from your list." });
    }

    [HttpGet("{courseId:int}/feedback-status")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetFeedbackStatus(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var status = await _courseService.GetFeedbackStatusForStudentAsync(UserId.Value, courseId, ct);
        if (status == null) return NotFound();
        return Ok(status);
    }

    [HttpGet("{courseId:int}/weekly-feedback-status")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetWeeklyFeedbackStatus(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var status = await _weeklyFeedbackService.GetWeeklyFeedbackStatusForStudentAsync(UserId.Value, courseId, ct);
        if (status == null) return NotFound();
        return Ok(status);
    }

    [HttpPost("{courseId:int}/feedback")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitFeedback(int courseId, [FromBody] SubmitCourseFeedbackRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _courseService.SubmitCourseFeedbackAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Your feedback has been saved." });
    }

    [HttpPost("{courseId:int}/weekly-feedback")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitWeeklyFeedback(int courseId, [FromBody] SubmitCourseFeedbackRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _weeklyFeedbackService.SubmitCourseWeeklyFeedbackAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Your weekly feedback has been saved." });
    }

    [HttpPut("{courseId:int}/feedback-window")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> SetFeedbackWindow(int courseId, [FromBody] SetCourseFeedbackWindowRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _courseService.SetCourseFeedbackWindowAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Feedback window updated." });
    }

    [HttpPut("{courseId:int}/weekly-feedback-window")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> SetWeeklyFeedbackWindow(int courseId, [FromBody] SetCourseFeedbackWindowRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _weeklyFeedbackService.SetCourseWeeklyFeedbackWindowAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Weekly feedback window updated." });
    }

    [HttpGet("{courseId:int}/feedbacks")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetFeedbacks(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetCourseFeedbacksForInstructorAsync(UserId.Value, courseId, ct);
        return Ok(list);
    }

    [HttpGet("{courseId:int}/weekly-feedbacks")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetWeeklyFeedbacks(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _weeklyFeedbackService.GetCourseWeeklyFeedbacksForInstructorAsync(UserId.Value, courseId, ct);
        return Ok(list);
    }

    [HttpGet("{courseId:int}/feedbacks/{feedbackId:int}")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetFeedbackDetail(int courseId, int feedbackId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var item = await _courseService.GetCourseFeedbackDetailForInstructorAsync(UserId.Value, courseId, feedbackId, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("{courseId:int}/weekly-feedbacks/{feedbackId:int}")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetWeeklyFeedbackDetail(int courseId, int feedbackId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var item = await _weeklyFeedbackService.GetCourseWeeklyFeedbackDetailForInstructorAsync(UserId.Value, courseId, feedbackId, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("{courseId:int}/feedback-summary")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetFeedbackSummary(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var summary = await _courseService.GetCourseFeedbackSummaryForInstructorAsync(UserId.Value, courseId, ct);
        if (summary == null) return NotFound();
        return Ok(summary);
    }

    [HttpGet("{courseId:int}/weekly-feedback-summary")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetWeeklyFeedbackSummary(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var summary = await _weeklyFeedbackService.GetCourseWeeklyFeedbackSummaryForInstructorAsync(UserId.Value, courseId, ct);
        if (summary == null) return NotFound();
        return Ok(summary);
    }

    [HttpGet("{courseId:int}/syllabus")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetSyllabus(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var syllabus = await _courseService.GetSyllabusForStudentAsync(UserId.Value, courseId, ct);
        if (syllabus == null) return NotFound();
        return Ok(syllabus);
    }

    [HttpGet("{courseId:int}/syllabus-file")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetSyllabusFile(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var file = await _courseService.GetSyllabusFileForStudentAsync(UserId.Value, courseId, ct);
        if (file == null) return NotFound();
        return File(file.Bytes, file.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("{courseId:int}/syllabus-upload")]
    [HttpPost("{courseId:int}/syllabus-pdf")]
    [Authorize(Roles = "Instructor")]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadSyllabusFile(int courseId, IFormFile? file, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Select a PDF syllabus file." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new { message = "Only PDF files are accepted for syllabus upload." });

        await using var stream = file.OpenReadStream();
        var result = await _courseService.UploadSyllabusFileAsync(UserId.Value, courseId, stream, file.FileName, ct);
        if (result == null)
            return NotFound(new { message = "Course not found, not owned by you, or file could not be read." });
        return Ok(result);
    }
}
