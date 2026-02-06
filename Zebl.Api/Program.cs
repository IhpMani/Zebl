using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
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

var corsOrigins =
    builder.Configuration.GetSection("CorsSettings:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

var requireAuth = jwtSettings.RequireAuthentication;
#endregion

#region Database
builder.Services.AddDbContext<ZeblDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
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
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
#endregion

#region Authentication (Optional JWT)
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
});
#endregion

#region Services
builder.Services.AddScoped<Zebl.Api.Services.Hl7ParserService>();
builder.Services.AddScoped<Zebl.Api.Services.Hl7ImportService>();
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

#region Middleware Pipeline
app.UseSerilogRequestLogging();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (builder.Configuration.GetValue<bool>("RateLimiting:EnableRateLimiting"))
{
    app.UseIpRateLimiting();
}

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
#endregion

#region Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
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
