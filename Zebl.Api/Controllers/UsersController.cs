using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "RequireAdmin")]
public class UsersController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly IAdminUserService _adminUserService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ZeblDbContext db, IAdminUserService adminUserService, ILogger<UsersController> logger)
    {
        _db = db;
        _adminUserService = adminUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserListItemDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _db.AppUsers
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserListItemDto
            {
                UserGuid = u.UserGuid,
                UserName = u.UserName,
                Email = u.Email,
                IsActive = u.IsActive,
                IsAdmin = _adminUserService.IsAdminUser(u.UserName),
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "UserName and Password are required." });

        var userName = request.UserName.Trim();

        var exists = await _db.AppUsers.AnyAsync(u => u.UserName == userName, cancellationToken);
        if (exists)
            return Conflict(new { error = "UserName already exists." });

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);

        var user = new AppUser
        {
            UserGuid = Guid.NewGuid(),
            UserName = userName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = hash,
            PasswordSalt = salt
        };

        await _db.AppUsers.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created AppUser. UserGuid={UserGuid}, UserName={UserName}", user.UserGuid, user.UserName);

        // Admin role is config-driven (AdminUsers) to avoid schema changes.
        return CreatedAtAction(nameof(GetUsers), new { }, null);
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserGuid == id, cancellationToken);
        if (user == null) return NotFound();

        user.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserGuid == id, cancellationToken);
        if (user == null) return NotFound();

        user.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed class UserListItemDto
{
    public Guid UserGuid { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CreateUserRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
}

