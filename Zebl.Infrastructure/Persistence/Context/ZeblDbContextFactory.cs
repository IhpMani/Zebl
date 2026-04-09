using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zebl.Infrastructure.Persistence.Context;

/// <summary>
/// Design-time migrations: no <see cref="Zebl.Application.Abstractions.ICurrentContext"/>; tenant/facility query filters are off.
/// </summary>
public sealed class ZeblDbContextFactory : IDesignTimeDbContextFactory<ZeblDbContext>
{
    public ZeblDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ZEBL_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ZeblDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<ZeblDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ZeblDbContext(options);
    }
}
