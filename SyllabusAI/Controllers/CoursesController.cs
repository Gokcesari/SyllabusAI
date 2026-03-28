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

    /// <summary>Öğrenci: Dersi kendi listesinden kaldırır (okul senkronu değil; yalnızca bu uygulamadaki kayıt silinir).</summary>
    [HttpDelete("my/{courseId:int}")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Unenroll(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var ok = await _courseService.UnenrollAsync(UserId.Value, courseId, ct);
        if (!ok) return NotFound(new { message = "Bu ders zaten listede yok." });
        return Ok(new { message = "Ders listeden kaldırıldı." });
    }

    /// <summary>Öğrenci: Geri bildirim penceresi ve kendi gönderimi hakkında bilgi.</summary>
    [HttpGet("{courseId:int}/feedback-status")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetFeedbackStatus(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var status = await _courseService.GetFeedbackStatusForStudentAsync(UserId.Value, courseId, ct);
        if (status == null) return NotFound();
        return Ok(status);
    }

    /// <summary>Öğrenci: Dönem sonu geri bildirimi (yalnızca eğitmenin açtığı UTC tarih aralığında, derse kayıtlıysa).</summary>
    [HttpPost("{courseId:int}/feedback")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitFeedback(int courseId, [FromBody] SubmitCourseFeedbackRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _courseService.SubmitCourseFeedbackAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Geri bildiriminiz kaydedildi." });
    }

    /// <summary>Eğitmen: Geri bildirim penceresini ayarla (UTC). İkisini de null göndermek pencereyi kapatır.</summary>
    [HttpPut("{courseId:int}/feedback-window")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> SetFeedbackWindow(int courseId, [FromBody] SetCourseFeedbackWindowRequest request, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var (ok, err) = await _courseService.SetCourseFeedbackWindowAsync(UserId.Value, courseId, request, ct);
        if (!ok) return BadRequest(new { message = err });
        return Ok(new { message = "Geri bildirim penceresi güncellendi." });
    }

    /// <summary>Eğitmen: Derse gelen geri bildirimleri listele.</summary>
    [HttpGet("{courseId:int}/feedbacks")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> GetFeedbacks(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var list = await _courseService.GetCourseFeedbacksForInstructorAsync(UserId.Value, courseId, ct);
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

    /// <summary>Öğrenci: Derse yüklenen son syllabus dosyası (PDF/Word). PDF tarayıcıda önizlenir; iframe için Bearer ile fetch→blob önerilir.</summary>
    [HttpGet("{courseId:int}/syllabus-file")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetSyllabusFile(int courseId, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        var file = await _courseService.GetSyllabusFileForStudentAsync(UserId.Value, courseId, ct);
        if (file == null) return NotFound();
        return File(file.Bytes, file.ContentType, enableRangeProcessing: true);
    }

    /// <summary>Eğitmen: Derse syllabus dosyası yükler (.pdf veya .docx). Metin çıkarılır, tabloya ve müfredat alanına yazılır.</summary>
    [HttpPost("{courseId:int}/syllabus-upload")]
    [HttpPost("{courseId:int}/syllabus-pdf")]
    [Authorize(Roles = "Instructor")]
    [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadSyllabusFile(int courseId, IFormFile? file, CancellationToken ct)
    {
        if (UserId == null) return Unauthorized();
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Syllabus dosyası seçin (PDF veya Word .docx)." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf" && ext != ".docx")
            return BadRequest(new { message = "Yalnızca .pdf veya .docx kabul edilir." });

        await using var stream = file.OpenReadStream();
        var result = await _courseService.UploadSyllabusFileAsync(UserId.Value, courseId, stream, file.FileName, ct);
        if (result == null)
            return NotFound(new { message = "Ders bulunamadı veya bu ders size ait değil / dosya okunamadı." });
        return Ok(result);
    }
}
