using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAiService _aiService;

    public ChatController(IAiService aiService) => _aiService = aiService;

    [HttpPost("ask")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var response = await _aiService.AskAsync(userId, request, ct);
        return Ok(response);
    }

    [HttpPost("instructor-ask")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> InstructorAsk([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var response = await _aiService.AskInstructorAsync(userId, request, ct);
        return Ok(response);
    }

    [HttpGet("analytics/{courseId:int}")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> CourseAnalytics(int courseId, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var data = await _aiService.GetCourseAnalyticsAsync(userId, courseId, ct);
        if (data == null) return NotFound(new { message = "Course not found." });
        return Ok(data);
    }

    [HttpPost("evaluate")]
    [Authorize(Roles = "Instructor")]
    public async Task<IActionResult> Evaluate([FromBody] List<RagEvalCaseDto> cases, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _aiService.EvaluateAsync(userId, cases, ct);
        return Ok(result);
    }
}
