using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Middleware;

/// <summary>
/// Ensures JWT sessionStamp matches AppUser.SessionStamp (single active session per user).
/// </summary>
public sealed class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ZeblDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sub = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst("UserGuid")?.Value;
        if (!Guid.TryParse(sub, out var userGuid) || userGuid == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var claimStamp = context.User.FindFirst("sessionStamp")?.Value;

        var dbStamp = await db.AppUsers.AsNoTracking()
            .Where(u => u.UserGuid == userGuid)
            .Select(u => u.SessionStamp)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (string.IsNullOrEmpty(dbStamp) ||
            string.IsNullOrEmpty(claimStamp) ||
            !string.Equals(dbStamp, claimStamp, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
