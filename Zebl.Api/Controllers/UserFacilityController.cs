using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/user-facilities")]
[Authorize(Policy = "RequireAdmin")]
public class UserFacilityController : ControllerBase
{
    private readonly ZeblDbContext _db;

    public UserFacilityController(ZeblDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetFacilitiesForUser(Guid userId, CancellationToken cancellationToken)
    {
        var userExists = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(u => u.UserGuid == userId, cancellationToken);
        if (!userExists)
            return NotFound(new { error = "User not found." });

        var facilityIds = await _db.UserFacilities
            .AsNoTracking()
            .Where(uf => uf.UserId == userId)
            .OrderBy(uf => uf.FacilityId)
            .Select(uf => uf.FacilityId)
            .ToListAsync(cancellationToken);

        return Ok(facilityIds);
    }

    [HttpPost]
    public async Task<IActionResult> AddMapping([FromBody] UserFacilityRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.UserId == Guid.Empty || request.FacilityId <= 0)
            return BadRequest(new { error = "userId and facilityId are required." });

        var userExists = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(u => u.UserGuid == request.UserId, cancellationToken);
        if (!userExists)
            return NotFound(new { error = "User not found." });

        var facilityExists = await _db.FacilityScopes
            .AsNoTracking()
            .AnyAsync(f => f.FacilityId == request.FacilityId && f.IsActive, cancellationToken);
        if (!facilityExists)
            return NotFound(new { error = "Active facility not found." });

        var exists = await _db.UserFacilities
            .AsNoTracking()
            .AnyAsync(
                uf => uf.UserId == request.UserId && uf.FacilityId == request.FacilityId,
                cancellationToken);
        if (exists)
            return BadRequest(new { error = "Mapping already exists." });

        await _db.UserFacilities.AddAsync(
            new UserFacility
            {
                UserId = request.UserId,
                FacilityId = request.FacilityId
            },
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Mapping added." });
    }

    [HttpDelete]
    public async Task<IActionResult> RemoveMapping([FromBody] UserFacilityRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.UserId == Guid.Empty || request.FacilityId <= 0)
            return BadRequest(new { error = "userId and facilityId are required." });

        var userExists = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(u => u.UserGuid == request.UserId, cancellationToken);
        if (!userExists)
            return NotFound(new { error = "User not found." });

        var facilityExists = await _db.FacilityScopes
            .AsNoTracking()
            .AnyAsync(f => f.FacilityId == request.FacilityId && f.IsActive, cancellationToken);
        if (!facilityExists)
            return NotFound(new { error = "Active facility not found." });

        var mapping = await _db.UserFacilities
            .FirstOrDefaultAsync(
                uf => uf.UserId == request.UserId && uf.FacilityId == request.FacilityId,
                cancellationToken);
        if (mapping == null)
            return NotFound(new { error = "Mapping not found." });

        _db.UserFacilities.Remove(mapping);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Mapping removed." });
    }
}

public sealed class UserFacilityRequest
{
    public Guid UserId { get; set; }
    public int FacilityId { get; set; }
}
