using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zebl.Infrastructure.Persistence.Context;

/// <summary>
/// Design-time migrations: no <see cref="Zebl.Application.Abstractions.ICurrentContext"/>; tenant/facility query filters are off.
/// </summary>
public sealed class ZeblDbContextFactory : IDesignTimeDbContextFactory<ZeblDbContext>
{
    public ZeblDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("DefaultConnection is not configured");
        }

        var options = new DbContextOptionsBuilder<ZeblDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ZeblDbContext(options);
    }
}
