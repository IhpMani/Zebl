using Zebl.Application.Services;

namespace Zebl.Api.Middleware;

public sealed class CorrelationEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationEnforcementMiddleware> _logger;

    public CorrelationEnforcementMiddleware(RequestDelegate next, ILogger<CorrelationEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requested = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(requested) ? context.TraceIdentifier : requested!.Trim();
        context.TraceIdentifier = correlationId;
        using var _ambient = CorrelationContext.Push(correlationId);
        using var _scope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        await _next(context).ConfigureAwait(false);
    }
}

