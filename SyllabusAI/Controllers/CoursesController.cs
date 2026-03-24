using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    private readonly IConfiguration _config;

    public CoursesController(ICourseService courseService, IConfiguration config)
    {
        _courseService = courseService;
        _config = config;
    }

    private int? UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsInstructor => User.IsInRole("Instructor");
    private bool IsStudent => User.IsInRole("Student");

    /// <summary>Eğitmen: Yeni ders ekler (ders kodu, başlık, müfredat metni).</summary>
    [HttpPost]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var course = await _courseService.CreateCourseAsync(UserId.Value, request, ct);
        if (course == null) return BadRequest(new { message = "Bu ders kodu zaten sizin tarafınızdan eklenmiş." });
        return Ok(course);
    }

    /// <summary>Eğitmen: Kendi derslerimi listele.</summary>
    [HttpGet("instructor")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetInstructorCourses(CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetInstructorCoursesAsync(UserId.Value, ct);
        return Ok(list);
    }

    /// <summary>Öğrenci: Ders kodu ile dersi listeme ekle (yıldızla / kayıt).</summary>
    [HttpPost("enroll")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> EnrollByCode([FromBody] EnrollByCodeRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var result = await _courseService.EnrollByCourseCodeAsync(UserId.Value, request.CourseCode, ct);
        if (result == EnrollResult.CourseNotFound)
            return NotFound(new { message = "Bu ders kodu bulunamadı." });
        if (result == EnrollResult.AlreadyEnrolled)
            return Ok(new { message = "Zaten bu derse kayıtlısınız.", enrolled = true });
        return Ok(new { message = "Ders eklendi.", enrolled = true });
    }

    /// <summary>Öğrenci: Kayıtlı olduğum dersleri listele.</summary>
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetMyEnrolledCoursesAsync(UserId.Value, ct);
        return Ok(list);
    }

    /// <summary>Öğrenci: Bir dersin müfredatını getir (inceleme + highlight).</summary>
    [HttpGet("{courseId:int}/syllabus")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetSyllabus(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var syllabus = await _courseService.GetSyllabusForStudentAsync(UserId.Value, courseId, ct);
        if (syllabus == null) return NotFound();
        return Ok(syllabus);
    }
}
