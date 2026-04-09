using Microsoft.EntityFrameworkCore;
using Zebl.Infrastructure.Persistence.Context;

namespace Zebl.Api.Services;

/// <summary>Seeds a single platform super-admin if none exists (runs every startup; idempotent).</summary>
public static class SuperAdminDefaultSeed
{
    public const string DefaultUserName = "superadmin@zebl.com";
    public const string DefaultPassword = "Admin@123";

    /// <summary>
    /// Synchronous startup path: same hashing as <see cref="AuthController"/> login (<see cref="PasswordHelper"/> / <see cref="PasswordHasher"/>).
    /// Logs to console; rethrow from caller for loud failure.
    /// </summary>
    public static void EnsureAtStartup(ZeblDbContext db)
    {
        var exists = db.AppUsers.AsNoTracking().Any(u => u.IsSuperAdmin);
        if (exists)
        {
            Console.WriteLine("⚠️ Super admin already exists");
            return;
        }

        var user = PasswordHelper.CreateUser(DefaultUserName, DefaultUserName, DefaultPassword);
        user.IsSuperAdmin = true;
        user.TenantId = null;
        user.FacilityId = null;

        db.AppUsers.Add(user);
        db.SaveChanges();

        Console.WriteLine("🔥 Super admin seeded successfully");
    }

    /// <summary>Async wrapper for tests or other callers.</summary>
    public static Task EnsureAsync(ZeblDbContext db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAtStartup(db);
        return Task.CompletedTask;
    }
}
