using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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
                    Message = GetErrorMessage(ex),
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

        private static string GetErrorCode(Exception ex) =>
            ex switch
            {
                ArgumentNullException => "NULL_ARGUMENT",
                ArgumentException => "INVALID_ARGUMENT",
                UnauthorizedAccessException => "UNAUTHORIZED",
                KeyNotFoundException => "NOT_FOUND",
                DbUpdateException => "DATABASE_ERROR",
                SqlException sqlEx when sqlEx.Number == -2 || sqlEx.Number == 2 => "QUERY_TIMEOUT",
                _ => "INTERNAL_ERROR"
            };

        private static string GetErrorMessage(Exception ex) =>
            ex switch
            {
                ArgumentNullException => "Required argument is missing",
                ArgumentException => "Invalid argument",
                UnauthorizedAccessException => "Unauthorized",
                KeyNotFoundException => "Resource not found",
                SqlException sqlEx when sqlEx.Number == -2 || sqlEx.Number == 2 => "The query took too long to execute. Please try with filters to narrow down the results.",
                _ => "Unexpected error"
            };

        private static int GetStatusCode(Exception ex) =>
            ex switch
            {
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                DbUpdateException => (int)HttpStatusCode.InternalServerError,
                SqlException sqlEx when sqlEx.Number == -2 || sqlEx.Number == 2 => (int)HttpStatusCode.ServiceUnavailable,
                _ => (int)HttpStatusCode.InternalServerError
            };
    }
}
