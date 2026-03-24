using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Student")]
public class ChatController : ControllerBase
{
    private readonly IAiService _aiService;

    public ChatController(IAiService aiService) => _aiService = aiService;

    /// <summary>Öğrenci: Müfredat ile ilgili soru sorar; AI basit bir dille cevaplar.</summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var response = await _aiService.AskAsync(userId, request, ct);
        return Ok(response);
    }
}
