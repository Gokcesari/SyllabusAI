using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _users.CreateUserAsync(request, ct);
        if (!result.Ok)
            return BadRequest(new { message = result.Error });
        return Ok(result.User);
    }

    [HttpPut("password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdateUserPasswordRequest request, CancellationToken ct)
    {
        var result = await _users.UpdatePasswordByEmailAsync(request, ct);
        if (!result.Ok)
            return BadRequest(new { message = result.Error });
        return Ok(new { message = "Şifre güncellendi." });
    }
}
