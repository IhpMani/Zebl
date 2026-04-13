using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Zebl.Application.Options;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using Zebl.Api.Configuration;
using Zebl.Api.Middleware;
using Zebl.Api.Services;
using Zebl.Infrastructure.Persistence.Context;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

#region Logging (Serilog)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
#endregion

#region Configuration
var jwtSettings =
    builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? new JwtSettings();

builder.Services.AddSingleton(jwtSettings);

builder.Services.Configure<AuditTrailOptions>(builder.Configuration.GetSection("AuditTrail"));
builder.Services.PostConfigure<AuditTrailOptions>(opts =>
{
    if (string.IsNullOrWhiteSpace(opts.IntegritySecret))
        opts.IntegritySecret = jwtSettings.SecretKey ?? string.Empty;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { errorCode = "RATE_LIMIT_EXCEEDED", message = "Too many requests. Try again later." },
            token);
    };
    options.AddPolicy("auth-security", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(ip))
        {
            var fwd = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            ip = string.IsNullOrEmpty(fwd) ? "unknown" : fwd.Split(',')[0].Trim();
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var corsOrigins =
    builder.Configuration.GetSection("CorsSettings:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length == 0)
    corsOrigins = new[] { "http://localhost:4200", "https://localhost:4200" };

if (builder.Environment.IsProduction())
{
    if (corsOrigins.Any(o => string.IsNullOrWhiteSpace(o) || o.Contains('*', StringComparison.Ordinal)))
        throw new InvalidOperationException("Production CORS requires CorsSettings:AllowedOrigins with explicit origins (no wildcards).");
}
#endregion

#region Database
void ConfigureDbContextOptions(DbContextOptionsBuilder options)
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(
        conn,
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
}

builder.Services.AddDbContextFactory<ZeblDbContext>(ConfigureDbContextOptions);
builder.Services.AddDbContext<ZeblDbContext>(
    ConfigureDbContextOptions,
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);
#endregion

#region Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        tags: new[] { "db", "sql" });

builder.Services
    .AddHealthChecksUI(options =>
    {
        options.SetEvaluationTimeInSeconds(10);
        options.MaximumHistoryEntriesPerEndpoint(50);
    })
    .AddInMemoryStorage();
#endregion

#region Rate Limiting
builder.Services.AddMemoryCache();

builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;

    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = builder.Configuration.GetValue<bool>("RateLimiting:EnableRateLimiting")
                ? builder.Configuration.GetValue<int>("RateLimiting:PermitLimit")
                : 1000
        }
    };
});

builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();
#endregion

#region CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
#endregion

#region Authentication (JWT)
builder.Services.AddHttpContextAccessor();

if (!string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
            };
        });
}
#endregion

#region Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAuth", policy => policy.RequireAuthenticatedUser());

    options.AddPolicy("RequireAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("IsAdmin", "true");
    });

    options.AddPolicy("SuperAdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("isSuperAdmin", "true");
    });
});
#endregion

#region Services
builder.Services.AddScoped<Zebl.Application.Abstractions.ICurrentUserContext, Zebl.Api.Services.JwtCurrentUserContext>();
builder.Services.AddSingleton<Zebl.Api.Services.IJwtTokenIssuer, Zebl.Api.Services.JwtTokenIssuer>();
builder.Services.AddScoped<Zebl.Application.Abstractions.ITenantContext, Zebl.Api.Services.JwtValidatedTenantContext>();
builder.Services.AddScoped<Zebl.Application.Abstractions.ICurrentContext, Zebl.Api.Services.HeaderCurrentContext>();
builder.Services.AddScoped<Zebl.Application.Abstractions.IInboundContext, Zebl.Api.Services.HeaderInboundContext>();
builder.Services.AddSingleton<Zebl.Api.Services.IAdminUserService, Zebl.Api.Services.AdminUserService>();
builder.Services.AddScoped<Zebl.Api.Services.Hl7ParserService>();
builder.Services.AddScoped<Zebl.Api.Services.Hl7ImportService>();
builder.Services.AddScoped<Zebl.Application.Abstractions.IClaimAuditService, Zebl.Infrastructure.Services.ClaimAuditService>();
builder.Services.AddScoped<Zebl.Application.Abstractions.IAuditTrail, Zebl.Infrastructure.Services.AuditTrailService>();
builder.Services.AddScoped<Zebl.Api.Services.EntityMetadataService>();
builder.Services.AddScoped<Zebl.Infrastructure.Services.ProgramSettingsService>();
builder.Services.AddScoped<Zebl.Infrastructure.Services.ClaimInitialStatusProvider>();

// Patient Eligibility (clearinghouse credentials only; password encrypted at rest)
builder.Services.AddDataProtection();
builder.Services.AddScoped<Zebl.Application.Abstractions.IEligibilitySettingsProvider, Zebl.Api.Services.PatientEligibilitySettingsService>();
builder.Services.AddScoped<Zebl.Application.Services.Eligibility271Parser>();
builder.Services.AddScoped<Zebl.Application.Services.IEligibilityService, Zebl.Infrastructure.Services.EligibilityService>();

// Receiver Library
builder.Services.AddScoped<Zebl.Application.Repositories.IReceiverLibraryRepository, Zebl.Infrastructure.Repositories.ReceiverLibraryRepository>();
builder.Services.AddScoped<Zebl.Application.Services.ReceiverLibraryService>();

// EDI Export
builder.Services.AddScoped<Zebl.Application.Repositories.IClaimRepository, Zebl.Infrastructure.Repositories.ClaimRepository>();
builder.Services.AddScoped<Zebl.Application.Services.IEdiExportService, Zebl.Application.Services.EdiExportService>();
builder.Services.AddScoped<Zebl.Application.Services.IClaimExportDataProvider, Zebl.Infrastructure.Services.ClaimExportDataProvider>();
builder.Services.AddScoped<Zebl.Application.Repositories.IScrubRuleRepository, Zebl.Infrastructure.Repositories.ScrubRuleRepository>();
builder.Services.AddScoped<Zebl.Application.Services.IClaimScrubService, Zebl.Infrastructure.Services.ClaimScrubService>();
builder.Services.AddScoped<Zebl.Application.Services.IClaimExportService, Zebl.Application.Services.ClaimExportService>();

// Connection Library
builder.Services.AddHttpClient();
builder.Services.AddScoped<Zebl.Application.Repositories.IConnectionLibraryRepository, Zebl.Infrastructure.Repositories.ConnectionLibraryRepository>();
builder.Services.AddScoped<Zebl.Application.Services.ConnectionLibraryService>();
builder.Services.AddScoped<Zebl.Application.Services.IEncryptionService, Zebl.Infrastructure.Services.AesEncryptionService>();
builder.Services.AddScoped<Zebl.Infrastructure.Services.SftpTransportService>();

// EDI Reports
builder.Services.AddScoped<Zebl.Application.Repositories.IEdiReportRepository, Zebl.Infrastructure.Repositories.EdiReportRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IClaimRejectionRepository, Zebl.Infrastructure.Repositories.ClaimRejectionRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IClaimSubmissionRepository, Zebl.Infrastructure.Repositories.ClaimSubmissionRepository>();
builder.Services.AddScoped<Zebl.Application.Services.Parser999Service>();
builder.Services.AddScoped<Zebl.Application.Services.EdiReportService>();
// IEdiReportFileStore removed - FileContent now stored in database

// Payer Library
builder.Services.AddScoped<Zebl.Application.Repositories.IPayerRepository, Zebl.Infrastructure.Repositories.PayerRepository>();
builder.Services.AddScoped<Zebl.Application.Services.PayerService>();

// ERA Posting & Payments
builder.Services.AddScoped<Zebl.Application.Repositories.IPaymentRepository, Zebl.Infrastructure.Repositories.PaymentRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IAdjustmentRepository, Zebl.Infrastructure.Repositories.AdjustmentRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IImportLogRepository, Zebl.Infrastructure.Repositories.ImportLogRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IEraExceptionRepository, Zebl.Infrastructure.Repositories.EraExceptionRepository>();
builder.Services.AddScoped<Zebl.Application.Services.EraExceptionService>();
builder.Services.AddScoped<Zebl.Application.Services.IEraPostingService, Zebl.Application.Services.EraPostingService>();

// Payment Engine (full payment entry, disbursement, claim totals)
builder.Services.AddScoped<Zebl.Application.Repositories.IServiceLineRepository, Zebl.Infrastructure.Repositories.ServiceLineRepository>();
builder.Services.AddScoped<Zebl.Application.Repositories.IDisbursementRepository, Zebl.Infrastructure.Repositories.DisbursementRepository>();
builder.Services.AddScoped<Zebl.Application.Services.IClaimTotalsService, Zebl.Application.Services.ClaimTotalsService>();
builder.Services.AddScoped<Zebl.Application.Abstractions.ITransactionScope, Zebl.Infrastructure.Services.PaymentTransactionScope>();
builder.Services.AddScoped<Zebl.Application.Services.IReconciliationService, Zebl.Infrastructure.Services.ReconciliationService>();
builder.Services.AddScoped<Zebl.Application.Services.IPaymentService, Zebl.Application.Services.PaymentService>();

// Secondary claim trigger (rule-driven, after ERA or manual posting)
builder.Services.AddScoped<Zebl.Application.Repositories.ISecondaryForwardableRulesRepository, Zebl.Infrastructure.Repositories.SecondaryForwardableRulesRepository>();
builder.Services.AddScoped<Zebl.Application.Services.ISecondaryTriggerService, Zebl.Application.Services.SecondaryTriggerService>();

// Procedure Code Library (lookup, fee schedule, charge calculation, NOC 837)
builder.Services.AddScoped<Zebl.Application.Services.IProcedureCodeLookupService, Zebl.Infrastructure.Services.ProcedureCodeLookupService>();
builder.Services.AddScoped<Zebl.Application.Services.IFeeScheduleResolver, Zebl.Infrastructure.Services.FeeScheduleResolver>();
builder.Services.AddScoped<Zebl.Application.Services.IClaimChargeCalculator, Zebl.Infrastructure.Services.ClaimChargeCalculator>();
builder.Services.AddScoped<Zebl.Application.Services.INOC837Formatter, Zebl.Infrastructure.Services.NOC837Formatter>();

// Code Library (diagnosis, modifier, pos, reason, remark)
builder.Services.AddScoped<Zebl.Infrastructure.Repositories.DiagnosisCodeRepository>();
builder.Services.AddScoped<Zebl.Infrastructure.Repositories.ModifierCodeRepository>();
builder.Services.AddScoped<Zebl.Infrastructure.Repositories.PlaceOfServiceRepository>();
builder.Services.AddScoped<Zebl.Infrastructure.Repositories.ReasonCodeRepository>();
builder.Services.AddScoped<Zebl.Infrastructure.Repositories.RemarkCodeRepository>();
builder.Services.AddScoped<Zebl.Application.Services.ICodeLibraryService, Zebl.Infrastructure.Services.CodeLibraryService>();

// Claim Template Library
builder.Services.AddScoped<Zebl.Application.Services.IClaimTemplateService, Zebl.Infrastructure.Services.ClaimTemplateService>();
#endregion

#region Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
#endregion

#region Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Zebl API",
        Version = "v1",
        Description = "API for Zebl Medical Billing System"
    });
    
    // Configure file upload support for Swagger
    c.OperationFilter<Zebl.Api.Configuration.SwaggerFileUploadOperationFilter>();
});
#endregion

var app = builder.Build();

#region Database startup — apply EF migrations only (no ad-hoc schema SQL)
if (builder.Configuration.GetValue("Database:ApplyMigrationsAtStartup", true))
{
    var csBootstrap = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(csBootstrap))
    {
        // Pooling=false avoids connection-pool session swaps that can break EF's __EFMigrationsLock
        // (sp_getapplock / sp_releaseapplock are per-session; SQL error 1223 on release otherwise).
        var csb = new SqlConnectionStringBuilder(csBootstrap) { Pooling = false };
        var bootstrapOptions = new DbContextOptionsBuilder<ZeblDbContext>()
            .UseSqlServer(
                csb.ConnectionString,
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null))
            .Options;
        try
        {
            using var db = new ZeblDbContext(bootstrapOptions);
            db.Database.Migrate();
        }
        catch (SqlException ex) when (ex.Number == 1223)
        {
            using var verifyDb = new ZeblDbContext(bootstrapOptions);
            if (verifyDb.Database.GetPendingMigrations().Any())
            {
                Log.Error(ex, "Database migration lock release failed (1223) and migrations are still pending.");
                if (app.Environment.IsDevelopment())
                    throw;
            }
            else
            {
                Log.Warning(ex,
                    "EF migration lock release failed (SQL 1223) but the database has no pending migrations; continuing startup.");
            }
        }
        catch (Exception ex)
        {
            var sql = ex as SqlException ?? ex.InnerException as SqlException;
            if (sql != null)
            {
                Log.Error(
                    ex,
                    "Database migration at startup failed (SQL {SqlNumber}). The server in ConnectionStrings:DefaultConnection is unreachable or the name is wrong. For local dev, use appsettings.Development.json with a reachable SQL instance (e.g. LocalDB), or set Database:ApplyMigrationsAtStartup to false.",
                    sql.Number);
            }
            else
            {
                Log.Error(ex, "Database migration at startup failed.");
            }

            if (app.Environment.IsDevelopment())
                throw;
        }
    }
}
#endregion

#region Middleware Pipeline
app.UseSerilogRequestLogging();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (builder.Configuration.GetValue<bool>("RateLimiting:EnableRateLimiting"))
{
    app.UseIpRateLimiting();
}

app.UseCors("cors"); // FIXED POLICY NAME

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<SessionValidationMiddleware>();
// Before authorization so handlers/controllers that use ZeblDbContext always see a validated facility (when required).
app.UseMiddleware<FacilityContextValidationMiddleware>();
app.UseAuthorization();
#endregion

#region Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zebl API v1");
    options.RoutePrefix = "swagger";
});

#endregion

#region Health Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
});
#endregion

app.MapControllers();

try
{
    Log.Information("Zebl API starting");

    if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
    {
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var db = services.GetRequiredService<ZeblDbContext>();
                // Intentionally NOT using EnsureCreated(): this app uses Migrate() in Development;
                // EnsureCreated() conflicts with migrations-based databases.
                SuperAdminDefaultSeed.EnsureAtStartup(db);
            }
            catch (Exception ex)
            {
                /* -2 = connection / query timeout; default Connect Timeout is 15s — see ConnectionStrings:DefaultConnection */
                if (ex is Microsoft.Data.SqlClient.SqlException sqlEx &&
                    sqlEx.Number == -2 &&
                    app.Environment.IsDevelopment())
                {
                    Log.Warning(sqlEx,
                        "Super admin seed skipped: SQL connection timed out. " +
                        "Confirm SQL Server is running and DefaultConnection is correct; Connect Timeout can be increased in appsettings.");
                    Console.WriteLine(
                        "⚠️ Super admin seed skipped (SQL timeout, Development). " +
                        "Start SQL Server (e.g. IHPOFFICE\\SQLEXPRESS) and restart the API.");
                }
                else
                {
                    Console.WriteLine("❌ SUPER ADMIN SEED ERROR:");
                    Console.WriteLine(ex.ToString());
                    Log.Fatal(ex, "Super admin startup seed failed.");
                    throw;
                }
            }
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Zebl API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
