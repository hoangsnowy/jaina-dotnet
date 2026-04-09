using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jaina.Diagnostics;

public class CorrelationIdActionFilter : IAsyncActionFilter
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly ILogger<CorrelationIdActionFilter> _logger;

    public CorrelationIdActionFilter(ILogger<CorrelationIdActionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var correlationId = context.HttpContext.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.HttpContext.Response.Headers[CorrelationIdHeader] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next();
        }
    }
}
