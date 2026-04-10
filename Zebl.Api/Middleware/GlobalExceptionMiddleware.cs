using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Zebl.Api.Services;
using Zebl.Application.Dtos.Common;

namespace Zebl.Api.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled exception occurred. TraceId: {TraceId}",
                    context.TraceIdentifier);

                var response = new ErrorResponseDto
                {
                    ErrorCode = GetErrorCode(ex),
                    Message = _environment.IsDevelopment() ? ex.Message : GetErrorMessage(ex),
                    TraceId = context.TraceIdentifier
                };

                if (_environment.IsDevelopment())
                {
                    response.Details = ex.ToString();
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = GetStatusCode(ex);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response, options));
            }
        }

        private static SqlException? FindSqlException(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is SqlException se) return se;
            }
            return null;
        }

        private static string GetErrorCode(Exception ex)
        {
            var sql = FindSqlException(ex);
            if (sql != null)
            {
                if (sql.Number == -2 || sql.Number == 2) return "QUERY_TIMEOUT";
                if (sql.Number == 207) return "DATABASE_SCHEMA_MISMATCH";
            }

            return ex switch
            {
                TenantSecurityException t => t.ErrorCode,
                ArgumentNullException => "NULL_ARGUMENT",
                ArgumentException => "INVALID_ARGUMENT",
                UnauthorizedAccessException => "UNAUTHORIZED",
                KeyNotFoundException => "NOT_FOUND",
                DbUpdateException => "DATABASE_ERROR",
                _ => "INTERNAL_ERROR"
            };
        }

        private static string GetErrorMessage(Exception ex)
        {
            var sql = FindSqlException(ex);
            if (sql != null)
            {
                if (sql.Number == -2 || sql.Number == 2)
                    return "The query took too long to execute. Please try with filters to narrow down the results.";
                if (sql.Number == 207)
                    return "The database schema is out of date. Apply EF migrations (e.g. dotnet ef database update) and restart the API.";
            }

            return ex switch
            {
                TenantSecurityException t => t.Message,
                ArgumentNullException => "Required argument is missing",
                ArgumentException => "Invalid argument",
                UnauthorizedAccessException => "Unauthorized",
                KeyNotFoundException => "Resource not found",
                _ => "Unexpected error"
            };
        }

        private static int GetStatusCode(Exception ex)
        {
            var sql = FindSqlException(ex);
            if (sql != null && (sql.Number == -2 || sql.Number == 2 || sql.Number == 207))
                return (int)HttpStatusCode.ServiceUnavailable;

            return ex switch
            {
                TenantSecurityException => (int)HttpStatusCode.Forbidden,
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                DbUpdateException => (int)HttpStatusCode.InternalServerError,
                _ => (int)HttpStatusCode.InternalServerError
            };
        }
    }
}
