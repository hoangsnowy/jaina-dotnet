using Microsoft.Extensions.Logging;

namespace Jaina.Diagnostics;

public static class LoggingExtensions
{
    public static IDisposable? WithCorrelationId(this ILogger logger, string correlationId) =>
        logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

    public static IDisposable? WithProperty(this ILogger logger, string key, object value) =>
        logger.BeginScope(new Dictionary<string, object> { [key] = value });

    public static IDisposable? WithProperties(this ILogger logger, IDictionary<string, object> properties) =>
        logger.BeginScope(properties);
}
