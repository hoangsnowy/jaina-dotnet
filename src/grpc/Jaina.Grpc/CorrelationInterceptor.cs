using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Jaina.Grpc;

/// <summary>
/// Reads <c>x-correlation-id</c> from incoming gRPC metadata and pushes it onto a logger
/// scope so downstream logs include it. Generates one if the caller didn't supply it.
/// Echoes the value back in response metadata.
/// </summary>
public sealed class CorrelationInterceptor : Interceptor
{
    public const string MetadataKey = "x-correlation-id";

    private readonly ILogger<CorrelationInterceptor> _logger;
    public CorrelationInterceptor(ILogger<CorrelationInterceptor> logger) => _logger = logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = context.RequestHeaders.GetValue(MetadataKey)
                         ?? Guid.NewGuid().ToString();

        await context.WriteResponseHeadersAsync(new Metadata
        {
            { MetadataKey, correlationId },
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            return await continuation(request, context);
        }
    }
}
