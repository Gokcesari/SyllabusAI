using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SyllabusAI.Data;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Services;

public class RolesService : IRolesService
{
    private readonly ApplicationDbContext _db;
    private readonly IMapper _mapper;

    public RolesService(ApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<List<RoleDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ProjectTo<RoleDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Aynı isimde rol varsa yeni kayıt eklemez, mevcut rolü döner (idempotent).
    /// </summary>
    public async Task<(bool Created, RoleDto Role)> EnsureRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        var existing = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower(), ct);
        if (existing != null)
            return (false, _mapper.Map<RoleDto>(existing));

        var role = new Role { Name = name };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return (true, _mapper.Map<RoleDto>(role));
    }
}
