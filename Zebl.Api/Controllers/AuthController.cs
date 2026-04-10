using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Zebl.Api.Configuration;
using Zebl.Api.Services;
using Zebl.Application.Abstractions;
using Zebl.Application.Dtos.Common;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly JwtSettings _jwtSettings;
    private readonly IJwtTokenIssuer _jwtIssuer;
    private readonly IAdminUserService _adminUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditTrail _auditTrail;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ZeblDbContext db,
        JwtSettings jwtSettings,
        IJwtTokenIssuer jwtIssuer,
        IAdminUserService adminUserService,
        IWebHostEnvironment environment,
        IAuditTrail auditTrail,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwtSettings = jwtSettings;
        _jwtIssuer = jwtIssuer;
        _adminUserService = adminUserService;
        _environment = environment;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    /// <summary>
    /// Validate username + password, reject inactive users, issue JWT with UserGuid and UserName.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-security")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "UserName and Password are required." });
        }

        var user = await _db.AppUsers
            .AsNoTracking()
            .Where(u => u.UserName == request.UserName.Trim() && u.IsActive)
            .Select(u => new
            {
                u.UserGuid,
                u.UserName,
                u.TenantId,
                u.FacilityId,
                u.IsSuperAdmin,
                u.PasswordHash,
                u.PasswordSalt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found or inactive. UserName={UserName}", request.UserName);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("Login failed: invalid password. UserName={UserName}", request.UserName);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        int? jwtFacilityId = null;
        if (!user.IsSuperAdmin)
        {
            var operationalTenantId = user.TenantId ?? 0;
            var allowedFacilityIds = await _db.UserFacilities.AsNoTracking()
                .Where(uf => uf.UserId == user.UserGuid)
                .Join(
                    _db.FacilityScopes.AsNoTracking(),
                    uf => uf.FacilityId,
                    f => f.FacilityId,
                    (uf, f) => f)
                .Where(f => operationalTenantId > 0 && f.TenantId == operationalTenantId && f.IsActive)
                .Select(f => f.FacilityId)
                .Distinct()
                .OrderBy(fid => fid)
                .ToListAsync(cancellationToken);

            if (allowedFacilityIds.Count == 0)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    errorCode = "NO_FACILITY_ACCESS",
                    error = "No facility access is assigned. Contact your administrator."
                });
            }

            jwtFacilityId = user.FacilityId;
            if (jwtFacilityId is int jf && jf > 0 && allowedFacilityIds.Contains(jf))
            {
                /* use profile default when it is an explicitly mapped facility */
            }
            else
            {
                jwtFacilityId = allowedFacilityIds[0];
            }
        }

        if (string.IsNullOrWhiteSpace(_jwtSettings.SecretKey))
        {
            _logger.LogError("JWT SecretKey is not configured.");
            return StatusCode(500, new { error = "Authentication is not configured." });
        }

        string? tenantKey = null;
        if (!user.IsSuperAdmin && user.TenantId is int tenantRowId && tenantRowId > 0)
        {
            tenantKey = await _db.Tenants.AsNoTracking()
                .Where(t => t.TenantId == tenantRowId && t.IsActive)
                .Select(t => t.TenantKey)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var isAdmin = _adminUserService.IsAdminUser(user.UserName);
        string token;
        try
        {
            token = user.IsSuperAdmin
                ? _jwtIssuer.IssueSuperAdminToken(user.UserGuid, user.UserName, isAdmin)
                : _jwtIssuer.IssueOperationalToken(
                    user.UserGuid,
                    user.UserName,
                    isAdmin,
                    user.TenantId ?? 0,
                    jwtFacilityId,
                    tenantKey ?? string.Empty,
                    impersonation: false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "JWT issuance failed.");
            return StatusCode(500, new { error = "Authentication is not configured." });
        }

        var expiresAt = _jwtIssuer.GetUtcExpiry();

        _logger.LogInformation("Login succeeded. UserGuid={UserGuid}, UserName={UserName}", user.UserGuid, user.UserName);

        await _auditTrail.WriteAsync(
            user.UserGuid,
            user.TenantId,
            new AuditMetadata
            {
                Action = "Login",
                Actor = user.UserName,
                Target = user.UserGuid.ToString(),
                Context = user.IsSuperAdmin ? "superAdmin" : $"facility:{jwtFacilityId}"
            },
            cancellationToken);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            UserGuid = user.UserGuid,
            UserName = user.UserName,
            IsAdmin = isAdmin,
            IsSuperAdmin = user.IsSuperAdmin,
            TenantId = user.TenantId,
            FacilityId = user.IsSuperAdmin ? null : jwtFacilityId
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-security")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.TenantKey))
        {
            return BadRequest(new { error = "email, password, and tenantKey are required." });
        }

        var email = request.Email.Trim();
        var tenantKeyNorm = request.TenantKey.Trim().ToLowerInvariant();

        var tenantId = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantKey == tenantKeyNorm && t.IsActive)
            .Select(t => t.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantId <= 0)
            return BadRequest(new { error = "Unknown or inactive tenantKey." });

        var existing = await _db.AppUsers.AsNoTracking()
            .AnyAsync(u => u.UserName == email, cancellationToken);
        if (existing)
            return Conflict(new { error = "An account with this email already exists." });

        var newUser = PasswordHelper.CreateUser(email, email, request.Password);
        newUser.TenantId = tenantId;
        await _db.AppUsers.AddAsync(newUser, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered. UserGuid={UserGuid}, UserName={UserName}", newUser.UserGuid, newUser.UserName);

        return CreatedAtAction(nameof(Login), new { userName = newUser.UserName }, new
        {
            newUser.UserGuid,
            userName = newUser.UserName,
            newUser.Email,
            newUser.IsActive,
            newUser.CreatedAt,
            message = "User must be granted explicit facility access before login."
        });
    }

    [HttpPost("set-password")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (request == null ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.TenantKey))
            return BadRequest(new { error = "userName, password, and tenantKey are required." });

        var tenantKeyNorm = request.TenantKey.Trim().ToLowerInvariant();
        var tenantId = await _db.Tenants.AsNoTracking()
            .Where(t => t.TenantKey == tenantKeyNorm && t.IsActive)
            .Select(t => t.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantId <= 0)
            return BadRequest(new { error = "Unknown or inactive tenantKey." });

        var userName = request.UserName.Trim();

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user == null)
        {
            user = new AppUser
            {
                UserGuid = Guid.NewGuid(),
                UserName = userName,
                Email = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId
            };
            await _db.AppUsers.AddAsync(user, cancellationToken);
        }
        else
            user.TenantId = tenantId;

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
}

public class SetPasswordRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantKey { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid UserGuid { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int? TenantId { get; set; }
    public int? FacilityId { get; set; }
}
