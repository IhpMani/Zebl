using System.Text;
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

var corsOrigins =
    builder.Configuration.GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

var requireAuth = jwtSettings.RequireAuthentication;
#endregion

#region Database
builder.Services.AddDbContext<ZeblDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});
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
            .AllowAnyOrigin()
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
    options.AddPolicy("RequireAuth", policy =>
    {
        if (requireAuth)
            policy.RequireAuthenticatedUser();
        else
            policy.RequireAssertion(_ => true);
    });

    options.AddPolicy("RequireAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("IsAdmin", "true");
    });
});
#endregion

#region Services
builder.Services.AddScoped<Zebl.Application.Abstractions.ICurrentUserContext, Zebl.Api.Services.JwtCurrentUserContext>();
builder.Services.AddSingleton<Zebl.Api.Services.IAdminUserService, Zebl.Api.Services.AdminUserService>();
builder.Services.AddScoped<Zebl.Api.Services.Hl7ParserService>();
builder.Services.AddScoped<Zebl.Api.Services.Hl7ImportService>();
builder.Services.AddScoped<Zebl.Application.Abstractions.IClaimAuditService, Zebl.Infrastructure.Services.ClaimAuditService>();
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

#region Database startup — migrations only in development (production: dotnet ef database update)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ZeblDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Development migration failed. Apply migrations manually if needed.");
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
app.UseAuthorization();
#endregion

#region Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

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
