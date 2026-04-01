using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyllabusAI.DTOs;
using SyllabusAI.Services;

namespace SyllabusAI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly IRolesService _roles;

    public RolesController(IRolesService roles)
    {
        _roles = roles;
    }

    /// <summary>
    /// Tüm rolleri listeler (ör. Student, Instructor, Admin).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetAll(CancellationToken ct)
    {
        return Ok(await _roles.GetAllAsync(ct));
    }

    /// <summary>
    /// Rol yoksa ekler; varsa mevcut kaydı döner. Swagger'dan öğrenci için Name: Student, eğitmen için Name: Instructor gönderin (JWT ile uyumlu).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name gerekli. Örnek: Student veya Instructor." });

        var (created, role) = await _roles.EnsureRoleAsync(request, ct);
        return Ok(new { created, role });
    }
}
