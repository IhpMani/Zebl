using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Zebl.Api.Configuration;
using Zebl.Api.Services;
using Zebl.Infrastructure.Persistence.Context;
using Zebl.Infrastructure.Persistence.Entities;
using SecurityClaim = System.Security.Claims.Claim;

namespace Zebl.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ZeblDbContext _db;
    private readonly JwtSettings _jwtSettings;
    private readonly IAdminUserService _adminUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ZeblDbContext db,
        JwtSettings jwtSettings,
        IAdminUserService adminUserService,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwtSettings = jwtSettings;
        _adminUserService = adminUserService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Validate username + password, reject inactive users, issue JWT with UserGuid and UserName.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "UserName and Password are required." });
        }

        var user = await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName.Trim(), cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found. UserName={UserName}", request.UserName);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login rejected: user inactive. UserGuid={UserGuid}", user.UserGuid);
            return Unauthorized(new { error = "Account is inactive." });
        }

        if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("Login failed: invalid password. UserName={UserName}", request.UserName);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        if (string.IsNullOrWhiteSpace(_jwtSettings.SecretKey))
        {
            _logger.LogError("JWT SecretKey is not configured.");
            return StatusCode(500, new { error = "Authentication is not configured." });
        }

        var isAdmin = _adminUserService.IsAdminUser(user.UserName);
        var token = BuildJwt(user.UserGuid, user.UserName, isAdmin);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        _logger.LogInformation("Login succeeded. UserGuid={UserGuid}, UserName={UserName}", user.UserGuid, user.UserName);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAt,
            UserGuid = user.UserGuid,
            UserName = user.UserName,
            IsAdmin = isAdmin
        });
    }

    // TEMP – REMOVE AFTER SETUP
    // Dev-only helper endpoint to set initial passwords without migrations/Identity.
    [HttpPost("set-password")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (request == null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "UserName and Password are required." });

        var userName = request.UserName.Trim();

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user == null)
        {
            // Create user if missing (dev convenience)
            user = new AppUser
            {
                UserGuid = Guid.NewGuid(),
                UserName = userName,
                Email = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _db.AppUsers.AddAsync(user, cancellationToken);
        }

        var (hash, salt) = PasswordHasher.HashPassword(request.Password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string BuildJwt(Guid userGuid, string userName, bool isAdmin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new SecurityClaim(JwtRegisteredClaimNames.Sub, userGuid.ToString()),
            new SecurityClaim(JwtRegisteredClaimNames.UniqueName, userName),
            new SecurityClaim("UserGuid", userGuid.ToString()),
            new SecurityClaim("UserName", userName),
            new SecurityClaim("IsAdmin", isAdmin ? "true" : "false")
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SetPasswordRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public Guid UserGuid { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
